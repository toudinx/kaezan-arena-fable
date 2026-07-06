# Design — Evolução Kaezan Arena Fable: idle-first, performático e com identidade visual própria

**Data:** 2026-07-06
**Status:** Aprovado pelo usuário (brainstorming em 2026-07-05/06)
**Decisões de escopo:** evoluir ESTE repo (sem repo novo); loop idle = runs encadeadas infinitas;
assets = híbrido packs CC0 + geração AI; form factor = aba normal do navegador (formatos novos
ficam para depois); prioridade nº 1 = performance.

## Visão

O jogo continua sendo o que já é (gacha + roguelike autoplay-first, backend autoritativo,
determinístico), mas evolui para um **idle de verdade**: runs encadeadas infinitas que rodam
sozinhas numa aba do navegador enquanto o jogador trabalha, com performance estável, mapas com
profundidade e, gradualmente, assets próprios (packs CC0 + geração AI) substituindo o material
da CipSoft. Tudo neste repo, protegido pelo replay-check (FF-01) como gate de qualquer mudança
de engine.

**Referência de experiência:** Task Bar Hero — o jogador configura estratégia, assiste de canto
de olho enquanto trabalha/estuda, e só faz ajustes pontuais (nova build, nova Kaeli, novo tier).

## Por que evoluir este repo (e não reescrever)

- O engine determinístico + replay bit-perfect (FF-01) é exatamente o seguro que torna refactors
  grandes seguros. Repo novo joga esse seguro fora.
- A meta completa (gacha, admin editor, skins, maestria, equipamento, afinidade, persistência
  MySQL) já funciona; reescrever custaria meses sem valor novo.
- Os problemas relatados (stutter, mapas rasos, FX falhando) são **localizados**: renderer,
  `DungeonGenerator` e pipeline de eventos — nenhum é problema de fundação.
- A migração PixiJS/WebGL fica como **decisão adiada**, travada atrás do profiling da Onda 1.

## Diagnóstico que ancora o design (verificado no código)

- **FX perdidos:** eventos (`EventDto`) viajam **dentro do snapshot** (`RunManager.cs` envia um
  snapshot por tick via SignalR; `renderer.ts` ingere `snap.events`). Snapshot perdido ou tab
  throttled = FX daquele tick some para sempre. Não há sequência nem reentrega.
- **Hitch de geração:** `BuildFloors` gera todos os andares de uma vez na criação da run
  (`GameWorld.cs:440`), de forma síncrona no caminho da conexão.
- **Medição contaminada:** o backend costuma rodar como exe em **Debug**, não Release.
- **Mapas rasos:** a arena é um retângulo erodido por autômato celular (`ErodeArena`) — lê como
  "quadradão"; a heurística de parede por vizinhança-8 deixa "dentes" e buracos visuais.
- `GameWorld.cs` tem ~5.100 linhas (monólito; FF-02 é o refactor sistemático, fora deste escopo).

---

## Onda 1 — Performance e confiabilidade (fundação)

**Objetivo:** eliminar stutter, hitch de geração e FX perdidos. Nada de otimização às cegas:
primeiro instrumentar, depois corrigir o que os números apontarem.

1. **Instrumentação.** Overlay de debug no cliente (toggle): frame time (p50/p95), idade do
   snapshot, contagem de eventos ingeridos/descartados, tempo gasto por camada de render.
   No backend: log de tempo de tick (p95) e de `BuildFloors`. Ferramenta permanente.
2. **Backend em Release.** Corrigir o fluxo de build/execução documentado antes de medir
   qualquer outra coisa (medição em Debug é inválida).
3. **FX confiável por design.** Cada `EventDto` ganha número de sequência; o snapshot carrega a
   janela dos últimos N ticks de eventos; o cliente deduplica pelo seq. Um snapshot perdido não
   perde mais FX (reentrega automática dentro da janela). Determinismo intocado (eventos são
   saída, não estado).
4. **Hitch de geração.** Criar a run (geração de andares + populate) fora do caminho da conexão,
   com estado de "loading" explícito no cliente. A geração continua determinística e em ordem
   (mesma seed, mesmo Rng) — muda *quando* acontece, não *o quê*.
5. **Otimizações de render guiadas pelo profiling**, e só então a decisão adiada: se o Canvas 2D
   for gargalo comprovado, a migração PixiJS/WebGL vira sub-projeto próprio; senão, otimiza-se o
   que existe (batching de atlas, camadas estáticas em offscreen canvas, cull de partículas).

**Gate de saída:** replay-check verde; p95 de frame time < 16 ms na cena de horda típica;
zero FX perdido em teste de tab throttled.

## Onda 2 — Geração de mapas v2 (profundidade + tiles corretos)

**Objetivo:** matar o "quadradão raso" sem quebrar determinismo nem o golden.

1. **Macro-forma antes da erosão.** Gerar primeiro uma silhueta orgânica (2-4 lóbulos
   sobrepostos, deterministicamente posicionados) e só então aplicar erosão. Chokepoints e
   bolsões viram consequência da forma, não ruído.
2. **Features intencionais.** Passe de pontos de interesse espaciais: clusters de pilares que
   criam cover, câmaras laterais (reativando `RoomsFloorN > 1` com conexões orgânicas), arena de
   boss com formato próprio (anfiteatro, não quadrado menor).
3. **Autotiling correto.** Substituir a heurística de vizinhança-8 por autotiling blob/Wang de
   47 casos — resolve os "dentes" e buracos na raiz, para qualquer bioma.
4. **Profundidade visual barata.** Camada de borda (sombra interna nas paredes, transição
   chão→parede), variação de tile de chão por ruído determinístico, clusters de decor maiores —
   tudo client-side ou na camada de decoração, sem tocar colisão.
5. **Validação.** Check interno de conectividade no gerador (BFS entrada→saída→POIs) que falha
   ruidosamente; goldens rebaselinados **explicitamente e por último**.

**Gate de saída:** replay-check + golden novo; screenshots lado-a-lado por bioma; sweep do
BalanceSim sem run inacabável.

## Onda 3 — Orquestração idle (runs encadeadas infinitas)

**Objetivo:** o jogador define a estratégia, aperta play e o jogo roda para sempre; ele só
assiste e ajusta.

1. **Session Plan (server-side).** Substituir o encadeamento client-side atual (seletor de
   Tentativas 1-5) por um orquestrador no backend: o jogador define estratégia (tier,
   Kaeli/rotação, regras de parada — "parar se perder 3 seguidas", "subir de tier ao vencer X",
   orçamento de energia) e o `RunManager` encadeia runs sozinho, aplicando recompensas entre
   elas. Aba fechada não interrompe: a sessão continua até a regra de parada, com janela de
   retenção.
2. **Run Journal.** Registro resumido por run (resultado, loot notável, cards, mortes, duração)
   + agregados da sessão ("nas últimas 2h: 34 runs, 31 vitórias, +12k ouro, 2 relíquias").
3. **UI espectador.** Modo passivo por padrão na tela de run: feed de journal, próxima ação da
   estratégia visível ("Run 35 — Tier 3 — Lunara — motivo: rotação"), controles manuais
   recolhidos. Interferir manualmente pausa a orquestração até soltar.
4. **Energia/economia idle.** Rebalancear para sessões longas (energia regenerativa como
   limitador de ritmo, caps de offline revisados) — constantes novas em `GameConfig`, tuning
   via BalanceSim.

**Gate de saída:** sessão de 1h+ sem interação rodando estável; reconexão retoma espectador no
meio da run; replay-check verde (a orquestração fica **fora** do engine — `GameWorld` não muda).

## Onda 4 — Migração de assets (packs CC0 + ComfyUI + Codex imagegen) — trilha paralela

**Objetivo:** sair dos assets CipSoft por categoria, com identidade preservada onde importa.
Pode rodar em paralelo às ondas 2-3 (toca pipeline/manifests, não engine), com uma exceção:
o item de tiles depende do slot `WallSet` criado na Onda 2 (Task 4 do plano da Onda 2).
Plano executável: `docs/superpowers/plans/2026-07-06-wave4-asset-migration.md`.

**Atualização 2026-07-06 — terceira fonte validada:** o Codex desta máquina tem o plugin
**game-studio 0.1.2** (skills `imagegen` — gpt-image-2 built-in, sem API key, transparência via
chroma-key — e `sprite-pipeline` — strip de animação inteira a partir de 1 frame seed +
normalização com âncora compartilhada). Divisão de papéis: ComfyUI continua dono da IDENTIDADE
(bosses, assinaturas — consistência img2img, sem risco de censura, cf. IMG-08); Codex imagegen
cobre COMMODITY gerada (FX/mísseis sem pack bom, itens, monstros comuns); packs CC0/CC-BY
cobrem commodity pronta. O primeiro asset da categoria FX é o smoke test vivo da ferramenta.

1. **Style guide de sprite primeiro** (condição do usuário: imagens de referência bem
   definidas). Doc + folha de referência canônica: resolução por tipo de asset, paleta,
   proporção, ângulo de câmera, regra de outline/luz. Todo asset — de pack ou de AI — passa por
   esse crivo antes de entrar.
2. **Auditoria de inventário.** Script que varre manifests e conteúdo seedado e lista o que do
   Tibia está **em uso** (tiles por bioma, outfits de monstro, itens, FX, mísseis) com
   contagens — o backlog real da migração.
3. **Commodity via packs CC0/CC-BY** (Kenney, OpenGameArt, itch.io): terrenos, paredes, props,
   FX genéricos. Entram pelo caminho que já existe para assets autorais (manifests tipo
   `kaezan-outfits` que sobrevivem ao extractor), com prioridade sobre o atlas Tibia — troca
   categoria por categoria atrás de manifest, com fallback automático.
4. **Identidade via AI** (rig ComfyUI + pipeline `pack_kaeli_outfits.py`, já validado com os
   chibi 900101+): bosses, monstros-assinatura, relíquias e itens icônicos, seguindo o style
   guide como imagem de referência do img2img.
5. **Ordem de troca:** FX/mísseis (poucos, alto impacto nas skills) → tiles dos 5 biomas
   (sincronizado com a Onda 2) → monstros comuns → itens. Kaelis já são autorais.

**Gate de saída por categoria:** categoria 100% servida por manifest autoral; screenshot de
regressão por bioma; nenhuma referência a atlas Tibia no caminho daquela categoria.

## Dependências e sequência

Onda 1 → Onda 2 → Onda 3 (sequenciais: medição antes de mexer no gerador; gerador estável antes
de sessões infinitas). Onda 4 é trilha paralela desde já (auditoria, style guide e infra de
packs podem começar hoje), com o item de tiles dependendo da Onda 2 Task 4 (slot `WallSet`).
Os planos executáveis vivem em `docs/superpowers/plans/2026-07-06-wave{1..4}-*.md`; cada task
declara **Modelo · Effort** (GPT-5.5 Codex executa o bem-especificado; Opus 4.8 integra o que
toca engine/golden; Fable 5 só nas 2 tasks de risco cross-cutting — baseline/decisão de render
da Onda 1 e orquestrador/session runs da Onda 3).

## Riscos principais

- **Refactor do `GameWorld` (5,1k linhas) com escopo-creep:** as ondas só extraem o que precisam
  tocar (pipeline de eventos, criação de run); FF-02 continua sendo o refactor sistemático,
  protegido pelo replay-check.
- **Golden/replay quebrando silenciosamente na Onda 2:** rebaseline é passo explícito e final de
  cada mudança no gerador.
- **Migração PixiJS prematura:** decisão travada atrás do profiling da Onda 1 — não entra no
  plano até haver número.
- **Licenças de packs:** só CC0/CC-BY com atribuição registrada em `CREDITS.md`; nada de "free
  for personal use".

## Invariantes que este design NÃO toca

- Backend autoritativo; frontend só renderiza.
- Determinismo do engine (Rng xorshift da run; replay-check como gate).
- Todas as constantes em `Domain/GameConfig.cs`.
- IDs estáveis (`waifu:*`, `card:*`, `banner:*`, `monster:*`).
- Idioma: jogo e código em inglês; docs em PT.
