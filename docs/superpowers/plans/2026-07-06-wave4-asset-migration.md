# Onda 4 — Migração de Assets: packs CC0 + ComfyUI + Codex imagegen (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Tasks marcadas como **processo** têm gate de aprovação do usuário — não são autônomas.

**Spec:** `docs/superpowers/specs/2026-07-06-kaezan-idle-evolution-design.md` (Onda 4).

**Goal:** Sair dos assets CipSoft categoria por categoria — FX/mísseis → tiles dos 5 biomas → monstros comuns → itens — com identidade preservada onde importa (bosses/assinaturas via ComfyUI) e gate de regressão visual por categoria. Trilha paralela: roda junto das Ondas 2-3, com uma única dependência dura (tiles ← Onda 2 Task 4).

**Architecture:** O caminho de override já existe no cliente: `AssetsService` funde um manifest autoral opcional (`/assets/kaezan-outfits/manifest.json`) sobre o atlas Tibia, com prioridade por id nas 4 categorias (`outfits`, `objects`, `effects`, `missiles`). A onda (1) generaliza esse caminho para N packs autorais com fallback automático, (2) constrói o backlog real via auditoria (`tools/AssetAudit` referencia o projeto da Api, como o BalanceSim faz, e cruza ids usados × cobertura de manifest), e (3) alimenta os packs por três fontes com papéis distintos. Troca de asset commodity é **client-side por id** (mesmo ushort id, arte nova via manifest) — engine, golden e replay intocados; a única exceção é o WallSet de 47 casos (Task 5), que registra ids novos no backend e rebaselina o golden explicitamente, com a mesma disciplina da Onda 2 Task 6.

**Tech Stack:** .NET 8 (`tools/AssetAudit` console), Angular 21 + Vitest (`AssetsService`), Codex + plugin game-studio 0.1.2 (skill `imagegen` built-in gpt-image-2 + skill `sprite-pipeline`), ComfyUI local (`tools/comfyui_batch.py`, `pack_kaeli_outfits.py`).

## Fontes de asset e quando usar cada uma

| Fonte | Para quê | Por quê |
|---|---|---|
| **Packs CC0/CC-BY** (Kenney, OpenGameArt, itch.io) | terrenos, paredes, props, FX genéricos | qualidade pronta, custo zero; só CC0/CC-BY, atribuição em `CREDITS.md` — nada de "free for personal use" |
| **ComfyUI (rig local)** | bosses, monstros-assinatura, relíquias, itens icônicos | identidade/consistência via img2img + style guide como referência; pipeline validado (chibi 900101+); sem risco de censura |
| **Codex imagegen (plugin game-studio)** | FX/mísseis sem pack bom, itens comuns, monstros comuns | `image_gen` built-in (gpt-image-2, **sem** API key), transparência via chroma-key + `remove_chroma_key.py`, strips de animação via skill `sprite-pipeline` (seed frame → strip inteira em 1 request → `normalize_sprite_strip.py` → preview sheet). Validado como instalado em 2026-07-06 (plugin 0.1.2 em `~/.codex/plugins/`); o primeiro asset da Task 4 é o smoke test vivo |

## Modelos & quando usar

> Rubrica completa no plano da Onda 1 (`2026-07-06-wave1-performance-reliability.md`).
> Nenhuma task desta onda é Fable: não há risco cross-cutting de engine — o valor caro aqui é
> curadoria visual, que é gate do USUÁRIO, não de modelo.

| Task | Modelo | Effort |
|---|---|---|
| 1 — AssetAudit (backlog real) | Opus 4.8 | medium |
| 2 — Sprite style guide | **processo:** usuário + Opus 4.8 | medium |
| 3 — Override multi-pack + CREDITS | Codex | medium |
| 4 — Categoria FX/mísseis | Codex (no app Codex, plugin game-studio) | medium |
| 5 — Tiles dos 5 biomas + WallSet 47 | Opus 4.8 | high |
| 6 — Bosses/assinaturas via ComfyUI | **processo:** usuário + Claude (PC) · Codex integra | high |
| 7 — Monstros comuns + itens (cauda longa) | Codex (imagegen batch + integração) | medium |

## Global Constraints

- **Engine intocado, exceto Task 5:** nenhuma outra task edita `Engine/`, `Domain/` ou qualquer
  coisa que mude golden/replay. Gate por task: `--golden-check` e `--replay-check` verdes SEM
  rebaseline (Task 5 é a exceção documentada, com rebaseline explícito e por último).
- **IDs estáveis:** troca de arte commodity NUNCA muda o id — o manifest autoral mapeia o MESMO
  id (ushort/lookType) para arquivo novo. Ids novos só existem no WallSet (Task 5).
- **Sprites só via `AssetsService`** — nenhum componente aponta para arquivo de pack direto.
- **Licenças:** só CC0/CC-BY; toda entrada de pack registrada em `CREDITS.md` ANTES do commit
  do asset. AI-gerado (imagegen/ComfyUI) registrado como "generated, project-owned".
- **Style guide é pré-condição:** nenhum asset entra (pack, ComfyUI ou imagegen) antes do
  style guide aprovado pelo usuário (Task 2) — condição registrada no spec.
- **Idioma:** código/comentários em inglês; este doc e o style guide podem ser PT.
- Ao final de cada task: `dotnet build` limpo; tasks com frontend: `npx ng build` limpo.

## Dependências e paralelismo

```
T1 (audit) ──┐
T2 (style)  ─┼─→ T4 (FX/mísseis) ─→ T7 (monstros comuns + itens)
T3 (packs)  ─┘        ↑
Onda 2 T4 ───────→ T5 (tiles + WallSet)      T6 (assinaturas) → T7
```

- T1, T2, T3 podem começar **hoje**, em paralelo entre si e com as Ondas 2-3.
- T4 exige T2+T3 (T1 fornece o checklist de cobertura).
- T5 exige T2+T3 **e a Onda 2 Task 4** (slot `BiomeDef.WallSet`); idealmente após a Onda 2
  inteira, para rebaselinar golden uma vez só sobre o gerador novo.
- T6 exige T2 (+T3 para entrega); independe de T4/T5.
- T7 por último — só depois do fluxo validado nas categorias anteriores.

---

### Task 1: `tools/AssetAudit` — o backlog real da migração

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium — precisa descobrir os registries reais (MonsterRegistry, ItemRegistry, Biomes, Waifus) e o formato do manifest

**Files:**
- Create: `tools/AssetAudit/AssetAudit.csproj` (console, referencia `KaezanArenaFable.Api.csproj` — mesmo padrão do BalanceSim)
- Create: `tools/AssetAudit/Program.cs`
- Create (saída, commitada): `docs/balance/asset_inventory.md`

**Interfaces:**
- Consumes: registries do backend (monstros → lookTypes; itens → sprite ids; `Biomes` → ids de ground/wall/decor/accent por tier; skills de `Waifus` → effect/missile ids; `GameConfig` → ids fixos como `ChestId`, `SanctuaryId`, `LadderDownId`), e os manifests do frontend (`frontend/public/assets/tibia/manifest.json` + packs autorais).
- Produces: relatório por categoria (`outfits`, `objects`, `effects`, `missiles`) com: total de ids EM USO, quantos servidos por manifest autoral, quantos ainda no atlas Tibia, e a lista dos pendentes. Re-rodável a qualquer momento — é o medidor de progresso da onda inteira (Tasks 4-7 usam como gate).

- [ ] **Step 1: Criar o projeto** (`dotnet new console -o tools/AssetAudit` + reference à Api, espelhando o `.csproj` do BalanceSim).
- [ ] **Step 2: Enumerar ids em uso.** Instanciar/carregar os registries como o BalanceSim faz e coletar, por categoria, o conjunto de ids que o jogo pode pedir ao `AssetsService`. Ordem estável (ids ordenados) para o diff do relatório ser legível.
- [ ] **Step 3: Cruzar com os manifests.** Ler `frontend/public/assets/tibia/manifest.json` e cada pack autoral presente (Task 3 define o index; na ausência dele, o `kaezan-outfits` legado se existir). Um id está "migrado" quando um pack autoral o cobre.
- [ ] **Step 4: Emitir `docs/balance/asset_inventory.md`** com a tabela-resumo + listas de pendentes por categoria, e imprimir o resumo no stdout.
- [ ] **Step 5: Verificação.** `dotnet run --project tools/AssetAudit` gera o arquivo; contagens > 0 em todas as categorias; `dotnet build` da solução limpo. Commit: `feat(tools): asset audit reports Tibia-asset usage vs authored coverage`.

---

### Task 2: Sprite style guide (processo — gate do usuário)

- **Modelo:** processo — usuário + Claude Code Opus 4.8 · **Effort:** medium. Bloqueia TODA task de asset (condição do spec: imagens de referência bem definidas antes de qualquer geração).

**Files:**
- Create: `docs/design/sprite_style_guide.md`
- Create: `docs/design/sprite_style_ref/` (folha de referência canônica — imagens aprovadas pelo usuário)

**Interfaces:**
- Produces: (a) regras mensuráveis por tipo de asset — resolução (tile 32px? monstro 64px? FX?), paleta base, ângulo de câmera (top-down oblíquo compatível com o atlas atual), regra de outline, direção de luz, legibilidade em escala de jogo; (b) o **bloco de identidade de prompt** para imagegen (mesma técnica das skills `kaeli-asset-prompts`); (c) a(s) imagem(ns) de referência canônica(s) para o img2img do ComfyUI. Tasks 4-7 consomem os três.

- [ ] **Step 1: Levantar o baseline visual.** Screenshots do jogo atual por bioma + amostras dos sprites Tibia mais visíveis (via relatório da Task 1) — o guide nasce do que já lê bem em tela.
- [ ] **Step 2: Rascunho do guide** (doc + 2-3 candidatos de folha de referência gerados com o próprio pipeline — serve de teste do bloco de identidade).
- [ ] **Step 3: GATE DO USUÁRIO.** Revisão e aprovação explícita das imagens de referência. Iterar até aprovar — nada downstream começa antes.
- [ ] **Step 4: Commit** do guide + refs aprovadas. `docs: sprite style guide and canonical reference sheet (wave 4 gate)`.

---

### Task 3: Override multi-pack no `AssetsService` + `CREDITS.md`

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium — a lógica de merge já existe para 1 pack; é generalização com teste

**Files:**
- Modify: `frontend/src/app/core/assets.service.ts` (~linhas 55-171)
- Create: `frontend/src/app/core/assets.service.spec.ts` (merge precedence, se não existir)
- Create: `frontend/public/assets/packs/index.json` (lista ordenada de packs; começa vazia `[]`)
- Create: `CREDITS.md` (raiz)

**Interfaces:**
- Consumes: `TibiaManifest` atual (categorias `outfits/objects/effects/missiles` + `semantic` + `objectNames`).
- Produces: carregamento de N packs — `/assets/packs/index.json` → para cada nome, `/assets/packs/<name>/manifest.json` com raiz de asset própria; merge em ordem (Tibia ← packs na ordem do index ← `kaezan-outfits` legado se existir), id repetido = último vence; pack ausente/quebrado é ignorado com `console.warn` (fallback automático). `CREDITS.md` com uma seção por pack: nome, fonte/URL, licença, o que cobre.

- [ ] **Step 1: Teste Vitest que falha** — merge de 2 packs + tibia: precedência por ordem, raiz de URL por pack, pack 404 ignorado.
- [ ] **Step 2: Implementar.** Corrigir de quebra o root hardcoded de `loadOptionalManifest` (hoje ignora a URL recebida e fixa `/assets/kaezan-outfits/` — com N packs isso vira bug real). Manter compat com o `kaezan-outfits` legado.
- [ ] **Step 3: `CREDITS.md`** com o formato de atribuição e a regra (CC0/CC-BY only) no topo.
- [ ] **Step 4: Verificação.** `npx vitest run` + `npx ng build` limpos; jogo abre e renderiza igual com `index.json` vazio (fallback 100% Tibia). Commit: `feat(assets): ordered multi-pack manifest override with automatic fallback`.

---

### Task 4: Categoria FX/mísseis — a primeira troca completa

- **Modelo:** GPT-5.5 (Codex) **no app Codex** (plugin game-studio: skills `imagegen` + `sprite-pipeline`) · **Effort:** medium. Depende de T2 e T3; usa o relatório da T1 como checklist.

**Files:**
- Create: `frontend/public/assets/packs/fx-v1/` (spritesheets + `manifest.json`)
- Modify: `frontend/public/assets/packs/index.json` (+ `"fx-v1"`)
- Modify: `CREDITS.md`

**Interfaces:**
- Consumes: lista de effect/missile ids em uso (T1), bloco de identidade do style guide (T2), caminho de override (T3).
- Produces: categoria `effects` + `missiles` 100% servida por pack autoral. FX são a categoria piloto de propósito: poucos ids, alto impacto visual (skills), e frames de animação pequenos — o melhor custo/benefício para validar o fluxo inteiro.

- [ ] **Step 1: Triagem por fonte.** Para cada id pendente: existe FX equivalente em pack CC0 (Kenney etc.)? → usa pack. Senão → gerar via imagegen.
- [ ] **Step 2: SMOKE TEST da ferramenta (primeiro asset).** No Codex: gerar 1 FX animado com `image_gen` (prompt = bloco de identidade + chroma-key `#ff00ff` para FX com verde/glow), remover fundo com `remove_chroma_key.py`, normalizar frames com `normalize_sprite_strip.py` (frame-size compatível com o manifest atual), montar preview sheet. **Gate do usuário** no preview antes de gerar o lote — este passo é a validação viva da capacidade que checamos estaticamente em 2026-07-06.
- [ ] **Step 3: Lote.** Gerar/adaptar o restante (uma chamada `image_gen` por asset, não `n`-variants), sempre: mesmo bloco de identidade, frame count exato do FX substituído, fundo chroma-key, sem cenário/texto.
- [ ] **Step 4: Montar o pack** `fx-v1` (spritesheets + manifest mapeando os MESMOS ids) e registrar em `CREDITS.md` (packs → atribuição; gerados → "project-owned").
- [ ] **Step 5: Gate da categoria.** `dotnet run --project tools/AssetAudit` → effects/missiles 100% autoral; screenshots de skills nos 5 biomas comparados com o antes; `--golden-check`/`--replay-check` verdes SEM rebaseline (nada de backend mudou); aceite visual do usuário. Commit: `feat(assets): authored fx/missile pack replaces Tibia category`.

---

### Task 5: Tiles dos 5 biomas + WallSet blob de 47 casos

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high — única task da onda que toca backend e rebaselina golden. Depende de T2, T3 e **Onda 2 Task 4** (slot `BiomeDef.WallSet`); rode idealmente após a Onda 2 fechar.

**Files:**
- Create: `frontend/public/assets/packs/tiles-v1/` (por bioma: ground/decor/accent com MESMOS ids; paredes com ids NOVOS do blob 47)
- Modify: `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` (preencher `WallSet` por bioma)
- Modify: `docs/balance/golden_dungeon.txt` (rebaseline explícito, último passo)
- Modify: `frontend/public/assets/packs/index.json`, `CREDITS.md`

**Interfaces:**
- Consumes: `WallAutotile.Resolve` (Onda 2 Task 4 — authored WallSet vence, fallback 4-peças), ids de tile por bioma (T1), style guide (T2).
- Produces: parte (a) **client-only**: ground/decor/accent trocados por override de id — golden intocado; parte (b) **backend**: 47 sprites de parede por bioma (CC0 adaptado ou imagegen com template blob), ids novos registrados no pack e no `WallSet` do bioma — muda hashes de `Wall` → rebaseline.

- [ ] **Step 1: Parte (a) — ground/decor por override.** Pack `tiles-v1` com os mesmos ids; verificar `--golden-check` VERDE (prova de que a troca é só visual).
- [ ] **Step 2: Screenshots lado-a-lado por bioma** (T1-T5) da parte (a); gate do usuário.
- [ ] **Step 3: Parte (b) — folhas blob 47 por bioma.** Gerar/adaptar seguindo o layout canônico de máscara do `WallAutotile` (bits 0..7, diagonais canônicas); registrar ids novos no manifest do pack.
- [ ] **Step 4: Preencher `BiomeDef.WallSet`** por bioma (dicionário máscara→id). `dotnet test` (os testes de autotile da Onda 2 cobrem o fallback de slot ausente).
- [ ] **Step 5: Rebaseline explícito e por último** — mesma liturgia da Onda 2 Task 6: `--golden` + revisão do diff (só hashes de `Wall`/`Decor` mudam) + `--golden-check` verde + limpar/regravar replays + `--replay-check` verde + sweep BalanceSim.
- [ ] **Step 6: Gate da categoria** (AssetAudit, screenshots finais, aceite do usuário) e commit: `feat(assets): authored biome tilesets with 47-case blob wall sets`.

---

### Task 6: Bosses e monstros-assinatura via ComfyUI (identidade)

- **Modelo:** processo — usuário + Claude no PC (ComfyUI vivo, `tools/comfyui_batch.py` + `pack_kaeli_outfits.py`) para geração; GPT-5.5 (Codex) integra o pack · **Effort:** high. Depende de T2 (e T3 para entrega); independe de T4/T5.

**Files:**
- Create: `frontend/public/assets/packs/signature-v1/` (+ index, CREDITS)

**Interfaces:**
- Consumes: lookTypes dos bosses/assinaturas (T1), imagem de referência canônica do style guide como entrada do img2img (T2), pipeline validado dos chibi 900101+.
- Produces: bosses e monstros-assinatura com arte própria, mesmos lookTypes (zero mudança de backend). Aqui NÃO se usa imagegen: identidade exige a consistência do rig local (img2img + controle fino), e evita o risco de censura já visto no GPT (IMG-08).

- [ ] **Step 1: Lista fechada com o usuário** (quais monstros são "assinatura" — sugerido: 1 boss por tier + 2-3 icônicos).
- [ ] **Step 2: Geração por monstro** no rig (style guide como ref do img2img; receitas em `docs/KNOWLEDGE_wan_idle_bust.md`/memória do skinvar valem como ponto de partida de parâmetros), com **gate do usuário por monstro** (preview em jogo).
- [ ] **Step 3: Empacotar** via `pack_kaeli_outfits.py` → `signature-v1`, mesmos lookTypes.
- [ ] **Step 4: Gate.** AssetAudit mostra os lookTypes cobertos; screenshot de cada um em combate; `--golden-check`/`--replay-check` verdes sem rebaseline. Commit: `feat(assets): signature monster pack (ComfyUI, style-guide driven)`.

---

### Task 7: Monstros comuns + itens — a cauda longa

- **Modelo:** GPT-5.5 (Codex) no app Codex (imagegen batch + sprite-pipeline) + integração · **Effort:** medium por lote. SÓ depois de T4-T6 validarem o fluxo. Lotes de ~10 assets com aceite do usuário por lote.

**Files:**
- Create: `frontend/public/assets/packs/creatures-v1/`, `frontend/public/assets/packs/items-v1/` (+ index, CREDITS)

**Interfaces:**
- Consumes: pendências do AssetAudit (o medidor), bloco de identidade (T2), sprite-pipeline para walk frames (seed frame por direção → strip → normalize com âncora bottom-center, preservando o walk-stride que o renderer espera).
- Produces: categorias `outfits` (monstros comuns) e `objects` (itens) migrando por lotes até o AssetAudit marcar 100%.

- [ ] **Step 1: Priorizar por visibilidade** (kills por monstro via bestiário/relatório T1 — o que o jogador mais vê migra primeiro).
- [ ] **Step 2: Lote de ~10** (itens: 1 imagem estática cada; monstros: seed frame aprovado → strips por direção via sprite-pipeline).
- [ ] **Step 3: Integrar + gate por lote** (AssetAudit, preview em jogo, aceite do usuário; CREDITS quando houver base CC0).
- [ ] **Step 4: Repetir** até 100% ou até o usuário declarar "bom o suficiente" para a categoria (o fallback continua funcionando para o resto — a migração pode parar em qualquer ponto sem estado quebrado).

---

## Gate de saída da Onda 4 (por categoria — a onda pode fechar parcial)

- [ ] Categoria X: AssetAudit reporta 100% servida por pack autoral (ou corte consciente registrado no relatório).
- [ ] Zero referência ao atlas Tibia no caminho da categoria (fallback nunca acionado em jogo normal — verificável por log/warn do `AssetsService`).
- [ ] Screenshots de regressão por bioma revisados pelo usuário.
- [ ] `CREDITS.md` cobre todo pack externo usado.
- [ ] `--golden-check` e `--replay-check` verdes (rebaseline só na Task 5, explícito).
- [ ] `dotnet build` + `npx ng build` + `npx vitest run` verdes; README atualizado (seção de assets: 3 fontes, override multi-pack, como rodar o AssetAudit).
