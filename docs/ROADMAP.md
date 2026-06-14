# ROADMAP — Kaezan Arena Fable

> **Para o agente (Codex/Claude) que vai executar:** este documento é a fila de trabalho do
> projeto. Cada task é auto-contida: contexto, arquivos, instruções, critérios de aceite e
> armadilhas. Execute **uma task por sessão**, na ordem de prioridade, salvo instrução contrária.

---

## 0. Regras de trabalho (leia antes de qualquer task)

1. Leia `README.md` e `CLAUDE.md` primeiro. `docs/GDD.md` dá o contexto de design.
2. **Invariantes**: backend autoritativo; determinismo do engine (só `Rng` da run dentro do
   `GameWorld`, nunca `Random`/`DateTime.Now`); todas as constantes em `Domain/GameConfig.cs`;
   IDs estáveis (`waifu:*`, `card:*`, `banner:*`, nomes de espécie) não mudam.
3. O repo `C:\Kaezan\kaezan` (Canary/OTClient + docs em `mapping/`) é **somente leitura** —
   fonte de assets e referência técnica.
4. Verificação mínima ao final de toda task: `dotnet build` (backend) e `npx ng build`
   (frontend) verdes. Para tasks de gameplay, rode os dois servidores
   (backend `dotnet run --urls http://localhost:5210`; frontend `npx ng serve`) e jogue uma run
   no tier 1. Para verificar o canvas em ambiente headless, use o hook `window.__kaezanRenderer`
   (rAF não roda em aba invisível): `__kaezanRenderer.draw(performance.now())` e capture via
   `canvas.toDataURL(...)`.
5. Ao concluir: atualize `README.md` se o comportamento visível mudou, e marque a task aqui
   como `[x]` com uma linha de resumo.
6. Se uma task se revelar maior do que o descrito, **pare e registre** o que falta na própria
   task em vez de entregar pela metade.

Prioridades: **P0** = fundação/feel, **P1** = valor alto, **P2** = quando sobrar tempo.
Esforço: S (< 1 sessão), M (1 sessão), L (1 sessão cheia, escopo apertado).

---

## 0.5 Modelo de owners (qual modelo implementa o quê)

Cada task/feature tem um **owner** sugerido. A regra é **escalar o modelo com a complexidade**:
tarefa mecânica não justifica modelo caro; feature determinismo-crítica não deve ir para o
modelo mais barato. O gradiente:

| Owner | Sweet spot | Reasoning |
|---|---|---|
| **Sonnet 4.6** | Mecânico, bounded, baixo risco: copiar/fiar assets, thumbnails, UI simples, polish, limpeza de dívida. | Rápido e barato. Tarefas sem ambiguidade de design não pagam um modelo premium. |
| **Codex (GPT 5.5)** | Implementação de spec fechada, tamanho médio, sistema isolado: conteúdo, endpoints, juice, features com instruções claras. | Forte em executar specs bem-escritas de ponta a ponta. É o owner default do ROADMAP. |
| **Opus 4.8** | Cross-cutting com **julgamento de design/balance**, mas escopo definível: refatorar sistemas, novas mecânicas de combate/itemização, UI complexa. | Raciocínio de arquitetura e design sem o custo do topo. Faz boas decisões onde a spec deixa margem. |
| **Fable 5** | **Determinismo-crítico**, algoritmicamente difícil, alto raio de explosão: IA dentro do tick, geração procedural com garantias, hardening de simulação. | Onde errar quebra replay/balance/perf. Vale o modelo mais forte. Ver [FABLE_TRACK.md](FABLE_TRACK.md). |

**Como ler a etiqueta de owner:** `Owner: Codex → Opus` significa "Codex se a spec abaixo for
seguida à risca; escale para Opus se aparecer decisão de design não coberta". O primeiro nome é
o piso recomendado.

> Regra de bolso para escalar: se a task **não pode quebrar o determinismo** (roda no `GameWorld`
> e afeta replay), o piso é **Opus** e o teto é **Fable 5**. Se é puramente de apresentação
> (frontend/asset/UI) sem lógica de simulação, o piso é **Sonnet**.

A tabela-mestra de atribuição (todas as tasks `T-*` + features `F-*`) está no fim deste documento.

---

## 0.7 Ordem de execução (prioridade real — SOBREPÕE a ordem das Fases)

> **Leia isto antes de pegar qualquer task.** As "Fases" mais abaixo são um **catálogo
> temático** (agrupam por assunto), **não** a ordem de fazer. A ordem real é esta. Motivo: o
> roadmap foi escrito em ordem de sprint natural (conteúdo → UI → sistemas → fundação), mas as
> features **mais fundacionais e de maior valor caíram no fim** (Fase 6 e FABLE_TRACK). Construir
> conteúdo (T-10/T-13) sobre sistemas rasos seria refazer trabalho. Então **a fundação vem
> primeiro**.

**Onda 0 — concluída:** T-01..T-04 (fluidez de movimento/IA/pausa/reconexão). ✅

**Onda 1 — Fundação de combate (P0, fazer AGORA).** Desbloqueiam quase tudo:
1. **T-52** — refundação de classes (4 Kaelis + stance). _Opus._ Base de todo personagem; sem
   ela, T-13 e F-A/F-B assumem um modelo errado.
2. **T-53** — fidelidade de IA/kit de monstro do Canary. _Fable 5._ Faz **todo** combate ficar
   real (bosses com kit de verdade); **absorve T-14**; é pré-requisito de qualidade de F-A.
   _(T-52 e T-53 são independentes entre si — podem ir em paralelo por owners diferentes.)_
3. **T-54** — persistência em MySQL (banco `kaezan_fable` separado). _Opus._ **Track paralelo**:
   não depende de T-52/T-53 e pode rodar junto. Deve **preceder** T-51/T-23/F-B/F-C (todos
   guardam estado novo) — senão construímos equipamento/maestria/leaderboard sobre JSON e
   retrabalhamos. Risco baixo (a persistência já está nas fronteiras da run, não no tick).

**Onda 2 — Itemização e elenco sobre a fundação (P0/P1):**
4. **T-11** — atributos/preços de item. _Codex._ Desbloqueia equipamento.
5. **T-51** — equipamento 6 slots + mount-as-gear. _Codex→Opus._ Dá propósito ao loot. _(Após T-54.)_
6. **T-50** — ícones de equipamento (extractor por `clothes.slot`). _Sonnet._ Pareia com T-51.
7. **T-10** — +30 monstros (refatorada — ver abaixo; agora herdam kit real via T-53). _Codex._
8. **T-13** — novas waifus = skins de classe (trivial após T-52). _Sonnet→Codex._

**Onda 3 — Feature flagship de produto (P0 de valor):**
9. **F-A** — Echo Team (seu time de waifus luta junto). _Fable 5._ A ponte coleção→gameplay; a
   feature de maior valor do projeto. Depende de T-52 + T-53.

**Onda 4 — Profundidade de combate e recompensa (P1):**
10. ✅ **F-E** — postura completa + reações elementais. _Opus._ **Inclui o que era T-31.**
   _Entregue 2026-06-12 (Echo Break por ciclo + matriz de reações data-driven)._
11. **T-30** — Sealed Reward + reroll (gacha-dentro-da-run). _Codex→Opus._ _(Após T-54.)_
12. **F-B** — árvore de Maestria por waifu. _Opus._ _(Após T-54.)_

**Onda 5 — Juice/UX/robustez (P1, PARALELIZÁVEL a qualquer momento).** Não bloqueiam nada e
podem ser feitas por Sonnet/Codex entre as ondas:
- T-20 (juice de combate), T-21 (HUD informativo), T-22 (fog of war), T-23 (cerimônia do gacha
  — histórico de pulls depende de T-54), T-24 (polish de meta), T-41 (fallback de asset).

**Onda 6 — Determinismo, desafio e geração (P1/P2, depois de haver o que simular):**
13. **F-C** — determinismo de ouro + Desafio Diário + harness. _Fable 5._ **Inclui T-33** (replay).
   Leaderboard do desafio assume T-54 (tabela de scores).
14. **T-40** — testes de determinismo/regras. _Opus→Fable._
15. **F-D** — geração procedural v2 (prefabs/pacing/set-pieces). _Opus→Fable._
16. **T-12** (biomas) e **T-32** (imbuements) e **T-42** (limpeza de dívida) — encaixar quando fizer sentido.

**Tasks supersedidas/absorvidas (não fazer isoladas):**
- **T-14** (DoT do player) → subconjunto de **T-53**; faça dentro dela.
- **T-31** (Boss Posture MVP) → faça direto a **F-E** (postura completa); não há ganho em fazer o MVP antes.
- **T-33** (replay) → parte de **F-C**.

---

## Fase 1 — Fluidez de gameplay e movimento (P0)

A maior alavanca de qualidade percebida. O jogo funciona, mas o "game feel" tem atritos
conhecidos, listados task a task.

### [x] T-01 — Movimento fluido: buffer de passo no servidor + tuning de cadência
**P0 · M · backend (+frontend leve)**

**Concluída:** passos encadeados no tempo exato, grace de 80ms, heartbeat de input e tuning final `MinStepMs=160`/diagonal `1.4`.

**Contexto.** Hoje `GameWorld.TickPlayerMovement()` (em
`backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs`) só inicia um passo novo se o anterior
já terminou **e** o tick coincidir. Com tick de 100ms e passos de ~400ms, o player perde até
100ms entre passos (sensação de "engasgo"), e mudanças de direção no meio do passo são ignoradas
até o próximo tick. O frontend interpola em `core/renderer.ts` (`actorRenderPos`).

**Instruções.**
1. No servidor, ao terminar um passo com `_moveDirX/_moveDirY` ainda pressionados, inicie o
   próximo passo **no mesmo tick**, com `StepStartMs` igual ao fim exato do passo anterior
   (não ao `NowMs` do tick) — elimina o gap de até 100ms. Cuidado para limitar a 1 passo extra
   por tick (não fazer loop de catch-up).
2. Adicione em `GameConfig` um `StepGraceMs` (~80ms): se o comando de direção chegar até X ms
   antes do fim do passo, o próximo passo já usa a nova direção (input buffer).
3. No frontend (`pages/game/game.ts`), envie `Move` também num `setInterval` leve (ex.: 250ms)
   enquanto houver tecla segurada — protege contra perda de pacote/foco e garante que o servidor
   sempre conheça a direção atual. Não envie quando a direção for (0,0) repetida.
4. Tuning em `GameConfig`: avalie `MinStepMs` 180→160 e `DiagonalStepFactor` 1.5→1.4. Jogue e
   ajuste pelo feel; anote os valores finais no commit.

**Aceite.** Segurar uma direção atravessa um corredor longo sem micro-pausas visíveis; trocar
de direção em movimento responde no passo seguinte; `dotnet build` verde; run jogada de ponta
a ponta sem regressão.

**Armadilhas.** Não introduza client-side prediction (fora de escopo; quebra o modelo
autoritativo). `StepStartMs` no passado é esperado pelo renderer (interp clampa em 0..1).

### [x] T-02 — IA de monstros: anti-empilhamento, perda de aggro e desvio
**P0 · M · backend**

**Concluída:** IA ganhou candidatos menos congestionados, desvio lateral via RNG, perda de aggro por distância/LOS e `staticAttackChance`.

**Contexto.** `TickMonsters()`/`StepToward()` usam passo greedy (sinal de dx/dy). Resultado
visível: mobs formam fila indiana em corredores e travam uns nos outros; aggro nunca é perdido
(perseguem para sempre, mesmo sem ver o player).

**Instruções.**
1. **Desvio lateral:** quando o passo greedy e os dois eixos falham, tente os 2 passos
   perpendiculares à direção desejada (ordem decidida por `_rng` para não sincronizar mobs).
2. **Separação:** ao escolher o passo, evite destino adjacente a 2+ mobs vivos quando houver
   alternativa de mesma distância (cheap: contar vizinhos ocupados antes de mover).
3. **Perda de aggro:** se `Chebyshev > 12` por mais de 4s contínuos **ou** sem LOS por mais de
   6s, zere `TargetId` e volte a `Wander`. Guarde `LastSawPlayerAtMs` no `Actor`.
   Constantes novas em `GameConfig` (`AggroDropRange`, `AggroDropNoLosMs` etc.).
4. **staticAttackChance:** o campo já vem do Canary (`Species.StaticAttackChance`, 0-100).
   Use-o: a cada decisão de movimento com alvo em range de ataque, role `_rng` — se passar,
   o mob fica parado atacando em vez de colar no player (dá identidade: atiradores como
   Minotaur Archer mantêm distância de verdade).

**Aceite.** Numa sala com 6+ mobs, eles cercam o player em arco em vez de fila; fugir de uma
sala e quebrar LOS por ~6s faz os mobs desistirem; archers param para atirar. Determinismo
preservado (só `_rng`).

### [x] T-03 — Escolha de card sem morrer: pausa tática
**P0 · S · backend**

**Concluída:** ofertas congelam o relógio da simulação e autoescolhem deterministicamente a primeira opção após 20s.

**Contexto.** A oferta de cards (`_pendingOffer`) não pausa nada — o player apanha enquanto lê.
Decisão original era "action roguelike", mas em tier alto é punitivo demais.

**Instruções.** Enquanto `_pendingOffer != null`: monstros não se movem nem atacam, projéteis/
DoTs não tickam, e o player não se move nem ataca (congele cedo no `Tick()`, mas continue
processando comandos `ChooseCard`). Adicione um cap: se o player ignorar a oferta por 20s
(`GameConfig.CardOfferTimeoutMs`), escolha automaticamente a primeira opção e despause —
evita exploit de pausa infinita e softlock.

**Aceite.** Ao subir de nível no meio de uma horda, o mundo congela visivelmente até a escolha;
após escolher, tudo retoma; timeout de 20s auto-escolhe.

**Armadilhas.** Não usar tempo real para o timeout — contar ticks (`TickCount`), senão quebra
determinismo de replay futuro.

### [x] T-04 — Reconexão de run (refresh não mata a run)
**P1 · M · backend + frontend**

**Concluída:** runs desconectadas ficam órfãs/pausadas por 60s, podem ser reassociadas e exibem o toast "Run retomada".

**Contexto.** `RunManager.DropRun` trata desconexão como abandono imediato. Um F5 acidental
destrói a run.

**Instruções.**
1. Em `DropRun`, em vez de encerrar, mova a run para um dicionário `_orphans` com timestamp;
   pause o mundo dela (não tickar). Após 60s sem reclamação, aí sim aplique o abandono atual.
2. `GameHub.JoinRun`: aceite um parâmetro opcional `resume: bool` — se houver run órfã (há só
   uma conta), reassocie ao novo `ConnectionId`, reenvie o `map` e retome o tick.
3. Frontend: `GamePage` ao iniciar tenta `JoinRun(tier, resume: true)`; o backend responde se
   retomou ou criou run nova; mostre toast "Run retomada".

**Aceite.** F5 no meio da run volta para a mesma dungeon (mesmo mapa, mesmos mobs, mesmo HP)
em até 60s; após 60s, abandono normal com metade do ouro.

---

## Fase 2 — Conteúdo em escala (P0/P1)

O pipeline existe; estas tasks são principalmente **curadoria + re-rodar tools**, com pouco
código. São as de melhor custo-benefício do roadmap.

### [x] T-10 — +30 monstros do Tibia (preencher os 5 tiers)
**P1 · M · tools + backend (dados) · Owner: Codex**

**Concluída (2026-06-12):** catálogo ampliado de 32 para 62 espécies, tiers rebalanceados com
≥5 commons/≥3 elites e assets/loot regenerados com cobertura completa de outfits e objetos.

> **Refatorada (2026-06-12) — depende de T-53.** Faça **depois** de T-53 (fidelidade de kit/IA).
> Antes, adicionar 30 monstros só dava 30 sacos de HP na IA genérica. Com T-53 no lugar, cada
> espécie nova **herda automaticamente o kit real do Canary** (conditions/summons/healing/FX) —
> esta task volta a ser só **curadoria + re-rodar tools**, e o conteúdo já entra "vivo". Por isso
> deixou de ser P0 (a fundação é T-52/T-53) e virou P1 logo após a itemização.

**Contexto.** `tools/convert-monsters/config.json` lista 29 espécies. Os tiers em
`GameConfig.Tiers` reusam poucas espécies. Fonte: `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\monster\`
(use `find` por nome de arquivo). **Pré-requisito:** T-53 já mergeada (o conversor já emite
condition/summon/healing/speed e o engine já os executa).

**Instruções.**
1. Adicione ao config (verifique existência do arquivo antes; nomes em snake_case):
   - Tier 1 (caverna): `bug`, `wolf`, `winter_wolf` (mammals), `troll`, `troll_champion` (humanoids), `scorpion` (vermins), `slime` (slimes), `centipede` (vermins)
   - Tier 2 (forte orc): `orc_shaman`, `orc_rider`, `goblin`, `goblin_scavenger` (humanoids), `war_wolf` (mammals), `dwarf`, `dwarf_soldier` (humanoids)
   - Tier 3 (cripta): `mummy`, `crypt_shambler`, `bonelord` (não-undead? verifique pasta), `ghost`, `banshee` (undeads), `witch` (humans)
   - Tier 4 (covil): `fire_elemental`, `earth_elemental` (elementals), `dragon_hatchling`, `dragon_lord_hatchling`, `frost_dragon_hatchling` (dragons), `minotaur_guard` (humanoids)
   - Tier 5 (abismo): `hellhound`, `hellfire_fighter` (demons), `frost_dragon` (dragons), `hydra` (reptiles), `dark_torturer`, `juggernaut` (demons)
2. `node convert.mjs`; revise o output: espécies com 0 ataques ou lookType 0 devem ser
   removidas (alguns usam `lookTypeEx`/item — não suportado).
3. Re-rode o AssetExtractor (comando no README) para puxar os novos outfits/loot/corpses.
4. Atualize `GameConfig.Tiers` distribuindo as espécies novas em `CommonMobs`/`EliteMobs`
   por dificuldade (use `health`/`experience` como guia: commons < ~300 hp no tier respectivo).
5. Jogue 1 run em cada tier desbloqueável e confirme que tudo renderiza (sem sprites invisíveis).

**Aceite.** ≥55 espécies em `monsters.json`; cada tier com ≥5 commons e ≥3 elites distintos;
manifest atualizado; runs nos tiers 1-2 verificadas em jogo, e os mobs com kit especial
(ex.: shaman que invoca, witch que envenena) **exibem esse kit** graças a T-53.

**Armadilhas.** Não reimplementar comportamento "à mão" — a fonte de verdade é o dado convertido
+ o executor de kit da T-53. Se um monstro novo tiver mecânica que a T-53 ainda não cobre,
**registre como gap na T-53**, não invente um caso especial aqui. Bosses de raid têm HP estranho
— confira `BossHpScale` se promover algum a boss de tier.

### [x] T-11 — Preços reais de itens (npcsaledata) + items.json
**P0 · M · tools + backend + frontend**

**Concluída (2026-06-12):** extractor gera `items.json` com o maior `sale_price` dos NPCs,
backend vende pelo catálogo com fallback 5 e a Mochila exibe o ganho por unidade e pela pilha.

**Contexto.** `MetaEndpoints.ItemValue` é um placeholder (`15 + itemId % 35`). O protobuf de
appearances tem `AppearanceFlagNPC npcsaledata` com `sale_price`/`buy_price` por item
(campo 40 de `AppearanceFlags` — já compilado no AssetExtractor).

**Instruções.**
1. No `AssetExtractor`, ao exportar objects, colete `flags.npcsaledata`: gere
   `backend/src/KaezanArenaFable.Api/Data/items.json` com `{ itemId, name, salePrice }`
   (maior `sale_price` entre os NPCs; name do appearance ou do loot). Inclua **todos** os ids
   presentes em monsters.json loot + os semantic.
2. Backend: carregue em `GameData` (`Items`); `ItemValue` passa a usar `salePrice`
   (fallback 5 se ausente). Exponha `salePrice` no `/catalog` para a Mochila.
3. Frontend Mochila: mostre o valor de venda no card do item ("Vender 1 (+45 🪙)").

**Aceite.** Vender um dragon ham paga o preço real do Tibia; itens sem comprador pagam 5;
`items.json` commitado; nenhum `itemId % 35` restante no código.

### [ ] T-12 — Biomas visuais por tier + cantos de parede
**P1 · L · tools + backend + frontend leve**

**Contexto.** Todos os tiers usam o tileset de caverna (`DungeonGenerator` hardcoda
`CaveGround`/`Wall*`). Tiles de stone wall (1112-1122) e lava (727-730) já estão extraídos.
As paredes usam só 3 variantes (pole/horizontal/vertical) — sem cantos, o contorno fica pobre.

**Instruções.**
1. Crie em `GameConfig` (ou arquivo novo `Domain/Biomes.cs`) um `BiomeDef` por tier:
   ground[], wallPole/H/V + corners, decor[], accent[] (ex.: lava no tier 4/5). Curadoria de ids:
   grep em `canary-3.4.1/data/items/items.xml` por nome (`stone wall`, `lava`, `grave`, `bone`,
   `mountain`); adicione os ids novos ao `content-config.json` e re-extraia. **Valide
   visualmente**: extraia, monte uma página de debug simples ou capture o canvas via hook.
2. `DungeonGenerator.Generate` recebe o `BiomeDef` (do tier) em vez das constantes atuais.
3. Cantos: na `PaintTiles`, classifique a célula de parede por vizinhança em 8 direções
   (N/S/E/W abertos) e escolha entre H, V, canto-NE/NW/SE/SW e pole. A família dirt wall
   356-367 tem as variantes — descubra o mapeamento renderizando as 12 lado a lado (hook do
   renderer ou contact sheet) e documente no código qual id é qual.
4. Decoração temática: cripta = ossos/lápides; covil = pedras + poças de lava decorativas
   (accent em células de sala, nunca bloqueando o caminho entre entrada↔escada↔boss).

**Aceite.** Tier 1 caverna, tier 3 cripta de pedra, tier 5 com lava; cantos de parede corretos
(sem "dentes" visuais nas quinas das salas); caminho sempre transitável.

### [ ] T-13 — 6 novas waifus (expansão do elenco gacha)
**P1 · S · backend (dados) + frontend leve · Owner: Sonnet → Codex**

> **Refatorada (2026-06-12) — depende de T-52.** Faça **depois** de T-52 (waifu = skin de uma das
> 4 classes). Com isso, "nova waifu" deixa de exigir um kit inventado — vira **declarativa**:
> escolher classe + outfit + raridade + stats-base + paleta. Por isso o esforço caiu de **M para
> S**. **Não** crie kits novos por waifu (isso violaria T-52).
>
> **Tooling pronto (2026-06-13):** o **Outfit Studio** (aba Kaelis no admin) já oferece o seletor
> de outfit/montaria + a paleta de recolor head/body/legs/feet + addons com preview animado —
> hoje usado para *skins* autorais (`KaeliRegistry`/`kaeli-skins.json`); o mesmo painel é a base
> visual natural para o passo "escolher outfit + paleta" desta task.

**Contexto.** Outfits femininos já extraídos e ainda não usados: 140 (noblewoman), 150 (oriental),
157 (beggar), 158 (shaman), 270 (jester), 279 (brotherhood), 288 (demonhunter).

**Instruções.**
1. Crie 6 waifus (2× 3★, 3× 4★, 1× 5★) usando esses lookTypes. Cada uma aponta para uma das 4
   **classes** (T-52: warrior/sentinel/shaman/wizard) — o kit vem da classe, não da waifu.
   Distribua para cobrir lacunas (ex.: uma 5★ warrior; uma 4★ shaman para a stance ice/earth).
2. Identidade própria: nome/título/descrição PT-BR (1 linha de lore), raridade, stats-base e
   **paleta de outfit** (head/body/legs/feet 0-132) distinta — valide no preview da Kaelis.
3. A nova 5★ entra no pool do banner padrão; avalie um segundo banner promocional rotativo em
   `GachaService.Banners` (id estável `banner:<tema>`).

**Aceite.** ≥19 waifus no catálogo, cada uma vinculada a uma classe existente; todas renderizam
no Kaelis/preview com addons; nenhuma waifu com kit próprio inventado (todas herdam da classe);
pull do banner pode tirar as novas.

### [x] T-14 — Condições do Tibia: poison/burn como DoT — **ABSORVIDA POR T-53**
**Owner: Fable 5 (dentro de T-53)**

**Concluída (2026-06-12) dentro da T-53:** DoT por tipo com FX 17/16/12, chip no HUD,
cor de dano por tipo no renderer e `card:antidote` no pool.

> **Não fazer isolada (2026-06-12).** Condições (DoT) são um subconjunto do **kit completo de
> monstro** que a **T-53** executa. Fazer T-14 separada criaria um caminho de condição que a T-53
> teria que reescrever. **Implemente o conteúdo abaixo como parte da T-53** (a T-53 já manda
> emitir `condition` no conversor e executá-la no engine). Mantida aqui só como checklist do que
> a T-53 deve cobrir para o lado do player: poison/fire/energy DoT + FX (17/16/12) + chip no HUD
> + card `card:antidote` (-50% dano de condição).

**Contexto (referência para a T-53).** O converter (`tools/convert-monsters/convert.mjs`) descarta
o campo `condition` dos ataques (ex.: snake/poison spider aplicam veneno no Tibia). O engine não
tem DoT.

**Instruções.**
1. Converter: quando o ataque tiver `condition = { type = "CONDITION_POISON"|..., totalDamage/
   interval/...}`, emita `condition: { type: "poison"|"fire"|"energy", totalDamage, tickMs }`
   (mapeie os nomes; valores ausentes → defaults: 10 ticks de 2s).
2. Engine: lista de DoTs ativos no player (`type, damagePerTick, ticksLeft, nextTickAtMs`);
   aplicada quando o ataque acerta; tick dentro do `Tick()` com dano via `DamagePlayer`
   (sem refresh stack — reaplicar substitui se maior). FX por tick: 17 (poison) / 16 (fire) /
   12 (energy) no tile do player, e cor do número de dano correspondente.
3. HUD: chip de condição ativa ao lado dos buffs (`PSN`/`BRN` com cor).
4. Card novo `card:antidote` ("Resistência: -50% dano de condições") entra no pool.

**Aceite.** Levar hit de Poison Spider aplica veneno visível (chip + ticks verdes);
o card reduz; determinismo preservado.

---

## Fase 3 — UI/UX e juice (P1)

### [ ] T-20 — Juice de combate
**P1 · M · frontend (renderer)**

**Contexto.** `core/renderer.ts`. Hoje: número de dano + efeito de sprite. Falta impacto.

**Instruções.**
1. **Hit flash:** ao ingerir evento `damage` de um monstro, pinte o sprite dele de branco por
   ~90ms (composite `source-atop` num offscreen por criatura, ou `filter: brightness` via
   `ctx.filter` — medir custo; cache por espécie se necessário).
2. **Screen shake:** 4-6px decaindo em 250ms quando o player toma hit ≥8% do MaxHp e no cast
   de ultimate (offset aleatório do camera antes do clamp).
3. **Números de dano:** crit maior (16px) com pop (escala 1.4→1.0 nos primeiros 120ms);
   dano do player no monstro em branco/amarelo, dano recebido em vermelho — já há cores, falta
   o pop e empilhamento (offsets alternados quando vários números no mesmo tile).
4. **Kill feedback:** no death event, fade-out rápido do sprite do monstro (já existe corpse;
   adicione 150ms de "ghost" branco esvaecendo).

**Aceite.** Capturas antes/depois via hook mostram flash/shake/pop; FPS estável (~60) com 10+
mobs na tela (medir com `performance.now()` no loop e logar média no console em dev).

### [ ] T-21 — HUD informativo: tooltips, alvo e bússola
**P1 · M · frontend**

**Instruções.**
1. **Tooltip de skill:** hover/long-press num slot mostra nome, descrição (`catalog.skills`),
   custo/cooldown — hoje o jogador não tem como ler o que a skill faz dentro da run.
2. **Frame do alvo:** painel compacto no topo (nome, HP numérico, elemento fraco/resistente —
   derivável de `catalog.monsters[species].loot`? Não: exponha `elements` do monstro no
   `/catalog`) quando houver `targetId`.
3. **Bússola de objetivo:** seta discreta na borda da tela apontando para a escada (andar 1)
   ou para o boss (andar 2), usando `map.ladderX/Y` e a posição do boss no snapshot —
   dungeons procedurais confundem; isso elimina o "andar perdido".
4. **Indicador de range:** com alvo travado, mostre se está em range de auto-attack
   (borda do frame do alvo verde/cinza).

**Aceite.** As 4 features visíveis e correta; nenhuma sobreposição com HUD existente em 1280×720.

### [ ] T-22 — Minimapa com fog of war
**P2 · S · frontend**

> **Absorvida por G-04** (`docs/GAMEPLAY_ROADMAP.md`): o fog of war ali cobre mapa principal
> + minimapa com tracking de exploração no backend. Faça G-04 em vez desta; não fazer as duas.

**Instruções.** `drawMinimap` hoje revela o mapa inteiro. Mantenha um `Set` de tiles vistos
(raio 9 do player, atualizado por snapshot); desenhe não-visitados em preto, visitados em
marrom, e POIs só depois de vistos. Reset por andar.

**Aceite.** Minimapa começa quase preto e revela conforme exploração; escada aparece só
depois de vista.

### [ ] T-23 — Cerimônia do gacha
**P1 · M · frontend**

**Contexto.** `pages/recruit/recruit.ts` tem reveal por cards com cor de raridade — funcional,
sem clímax.

**Instruções.**
1. **Tease de raridade:** antes do flip, brilho na borda na cor do melhor item do pull
   (dourado se houver 5★, roxo se 4★) — padrão do gênero.
2. **5★ especial:** ao revelar uma 5★, overlay fullscreen rápido (1.2s, skippable por clique):
   fundo radial dourado + outfit preview grande animado + nome/título.
3. **Histórico de pulls:** registre cada pull no backend (lista no `AccountState`, máx. 200)
   e mostre num modal "Histórico" na página (data, banner, resultado, raridade) — transparência
   de pity que jogador de gacha espera.
4. Som fica para T-34; não bloqueie nisso.

**Aceite.** 10-pull com 5★ mostra o tease dourado + cutscene skippable; histórico lista os
pulls com raridade colorida; pity exibido continua correto.

### [ ] T-24 — Polish geral de meta
**P2 · S · frontend**

**Instruções.** (a) Título da aba "Kaezan Arena Fable" + favicon (gere um PNG 32px do sprite
da Mirai com o próprio AssetExtractor ou recorte manual); (b) confirmação ao vender pilha
inteira (>5 itens); (c) botão "Vender duplicatas baratas" na Mochila (vende tudo com
salePrice < 20, depende de T-11); (d) skeleton/spinner de loading nas páginas meta em vez de
"Carregando..."; (e) estados de erro do fetch com retry (hoje só `alert`).

---

## Fase 4 — Features de sistema (P1/P2)

### [ ] T-30 — Sealed Reward: baú selado pós-boss com reroll
**P1 · L · backend + frontend**

**Contexto.** Inspiração: `kaezan/mapping/changes/features/sealed_reward/` e
`canary/client/features/sealed_reward.md` (somente leitura). Conceito: vitória → baú com
N slots revelados um a um; 1 reroll grátis em um slot.

**Instruções.**
1. Backend: ao matar o boss, gere (com o `_rng` da run — determinístico) 4 recompensas:
   1 garantida (Kaeros), 1 item raro do loot pool do tier (chance < 2000 no canary = raro),
   1 material/ouro, 1 slot "premium" (pequena chance de Echo Shards de waifu aleatória).
   Inclua no `RunEndDto` como `sealedReward: [{kind, itemId?, amount, revealed}]` + flag
   `rerollAvailable`.
2. Novo comando hub `RerollSealedSlot(index)` válido apenas com run terminada vitoriosa e
   reroll disponível; re-rola aquele slot (mesma tabela) e aplica a diferença na conta via
   `RewardService`.
3. Frontend: na tela de vitória, os slots aparecem virados; clique revela com flip + FX 184
   (pixie explosion) por sprite; botão de reroll por slot (1 uso).

**Aceite.** Vitória mostra o ritual de revelação; reroll funciona 1x e persiste o resultado
final na conta; derrota não gera sealed reward.

### [x] T-31 — Boss Posture (Echo Break) — **ENTREGUE DENTRO DE F-E (2026-06-12)**
**Owner: Opus (como F-E)**

> **Concluída via F-E (2026-06-12).** A postura completa (ciclos com multiplicador `2.5/3.5/5/6.5×`,
> fraqueza elemental que quebra mais rápido, bônus de %HP máx, decaimento) + a camada de reações
> elementais foram entregues na **F-E** ([FABLE_TRACK.md](FABLE_TRACK.md)). DTOs `bossPosture/Max/
> Staggered/Cycle` + barra de postura no HUD. Não há MVP isolado — a F-E entregou o sistema inteiro.

**Contexto (referência para a F-E).** Inspiração: `kaezan/mapping/changes/features/boss_posture/`.
Barra secundária do boss que enche com hits; cheia = boss quebrado (stun longo + dano amplificado).

**Instruções.**
1. Backend: `PostureMax` por boss (escala com tier; constante base em `GameConfig`). Cada hit
   do player adiciona postura (skills > auto-attack; valores em `GameConfig`). Postura decai
   ~2/s quando sem hits por 3s. Ao encher: stun 4s, +50% dano recebido durante o stun, FX 35 +
   texto "QUEBRADO!", barra reseta.
2. Snapshot: `bossPosture/bossPostureMax` no `RunStateDto`.
3. Frontend: barra fina dourada sob a HP bar do boss; pisca quando >80%.

**Aceite.** Lutar contra a Rotworm Queen permite ~1-2 quebras por luta; quebra é visível e
recompensadora; decaimento impede encher "de graça".

### [ ] T-32 — Imbuements lite (sockets elementais com loot)
**P2 · L · backend + frontend**

**Contexto.** Inspiração: `kaezan/mapping/canary/systems/economy.md` (imbuements). Dá uso ao
loot além de vender.

**Instruções.** 1 slot de imbuement por waifu (desbloqueia em A1). Receitas fixas em
`GameConfig`: ex. "Presas venenosas" = 20× snake skin? → use itens que realmente dropam
(verifique `monsters.json`); efeito = +10% dano do elemento X ou +5% lifesteal, por **conta**
(persistente). Endpoints REST `POST /api/v1/waifus/imbue`; UI na página Kaelis (aba do kit);
consumo de itens do inventário com confirmação.

**Aceite.** Aplicar um imbuement consome itens, persiste, afeta o dano em run (verificável
nos números), e aparece no detalhe da waifu.

### [~] T-33 — Replay determinístico — **PARTE DE F-C**
**Owner: Fable 5 (dentro de F-C)**

> **Não fazer isolada (2026-06-12).** O replay é a entrega 2 da **F-C** (determinismo + desafio
> diário + harness). Faça lá, onde o hardening de determinismo que o replay exige já está no escopo.

**Contexto (referência para a F-C).** O engine já é determinístico por seed+comandos; falta
gravar/reproduzir (kaezan-arena tinha; ver `InMemoryBattleStore.Replay.cs` lá como referência de shape).

**Instruções.** Grave `(tick, Command)` de cada run em memória; ao fim, persista JSON em
`.data/replays/` (`seed`, `tier`, `waifu`, `ascension`, `commands[]`, hash do estado final).
Endpoint dev `POST /api/v1/dev/replay` roda a run inteira headless re-aplicando os comandos
nos mesmos ticks e responde o hash final — base do teste T-40. Sem UI.

**Aceite.** Replay de uma run real reproduz hash idêntico; alterar 1 comando muda o hash.

### [ ] T-34 — Áudio
**P2 · L · frontend (+tools)**

**Contexto.** Zero áudio hoje. O Canary tem ids de som por monstro mas os arquivos de áudio
do cliente não estão disponíveis no formato extraível — **não tente extrair**; use SFX
livres (ex.: Kenney.nl, CC0) baixados para `frontend/public/assets/sfx/`.

**Instruções.** Serviço `AudioService` (Web Audio, pool, volume em localStorage, mute por
padrão até 1ª interação): hit melee, projétil, level-up, abrir baú, morte de mob, ultimate,
reveal de gacha (comum/4★/5★), vitória/derrota. Gatilhos pelos eventos já existentes no
renderer/ingest. Slider de volume no overlay de pausa/config (criar mini-modal de config
no shell).

**Aceite.** Sons nos 9 gatilhos; mute/volume persistem; sem erro de autoplay no primeiro load.

---

## Fase 5 — Qualidade, bugs e dívidas (P1)

### [ ] T-40 — Testes de determinismo e de regras (xunit)
**P1 · M · backend**

**Instruções.** Projeto `backend/tests/KaezanArenaFable.Tests` (xunit):
1. **Determinismo:** mesma seed + mesma sequência de comandos sintéticos (500 ticks de
   movimento + casts) ⇒ snapshots finais idênticos (serialize e compare). Rode 2× no mesmo
   processo e 1× com ordem de dicionários embaralhada se possível.
2. **Geração:** para 100 seeds, o dungeon é conexo (BFS da entrada alcança escada/boss),
   tem ≥1 baú e o boss existe no andar 2.
3. **Gacha:** com `Random` seedado injetável (refatore `GachaService` para aceitar um
   `Func<double>`), valide: hard pity 80 nunca estoura, 4★ a cada 10 sempre, 50/50 + garantia.
4. Adicione `dotnet test` ao fluxo de verificação no CLAUDE.md.

**Aceite.** `dotnet test` verde; teste de determinismo falha se alguém introduzir `Random`
no engine (prove removendo temporariamente o guard e revertendo).

### [ ] T-41 — Robustez de assets: fallback de sprite + diagnóstico
**P1 · S · frontend + tools**

**Contexto.** Sprite não extraído = invisível silencioso (ex.: futura espécie com lookType
fora do manifest).

**Instruções.** (1) `AssetsService`: quando `entry()` falhar, desenhe um placeholder
(quadrado magenta 50% alpha com "?") e registre `console.warn` com dedupe (1× por id).
(2) Tool nova `tools/check-assets.mjs`: cruza monsters.json (lookTypes, corpses, loot ids) e
Waifus/skills (effect/missile ids — exporte-os num JSON pequeno do backend ou hardcode a
extração via regex de `Waifus.cs`) contra o manifest, e lista o que falta. Rode no aceite
de toda task de conteúdo (referencie em T-10/T-12/T-13).

**Aceite.** Id inexistente mostra placeholder visível; `node check-assets.mjs` sai com
exit 1 e lista clara quando falta asset.

### [ ] T-42 — Limpeza de dívidas pontuais
**P2 · S · backend + frontend**

Checklist objetiva (tudo pequeno):
- [ ] `GameWorld.KillMonster`: guard de double-kill é código morto confuso — substitua por
  early-return `if (monster.Hp <= 0 && !overkill) return;` **antes** de processar, e garanta
  que `DealDamageToMonster` não chame `KillMonster` duas vezes no mesmo tick.
- [ ] `KaelisPage`: troque o `setInterval` de inicialização por `effect()` sobre os signals
  de `ApiService`.
- [ ] `ItemIcon`: troque o retry de 10× `setTimeout` por `await assets.image(...)` direto
  (a API já existe) e um único redraw.
- [ ] `GamePage.onKeyDown`: `Q/E/R` como alias já ok; adicione `preventDefault` nos números
  para não rolar a página em layouts pequenos.
- [ ] Facing diagonal: `FacingFrom` prefere E/W em diagonal; mantenha o último eixo dominante
  do passo anterior quando |dx|==|dy| (campo `LastFacingAxis` no Actor) para o sprite não
  "tremular" andando em diagonal.
- [ ] `MetaEndpoints`: padronize erros como `{ error }` com status 400 em todos os handlers
  (já é o padrão; revise `items/sell` e `active-waifu`).
- [ ] Remova `window.__kaezanRenderer`? **Não** — mantenha; é o hook de verificação headless.
  Documente-o com comentário apontando para o ROADMAP §0.4.

---

## Fase 6 — Refundação de conteúdo (correções de fundação do v0)

Quatro features pedidas pelo dono do projeto que corrigem decisões rasas do v0. São
**fundacionais** — várias outras tasks dependem delas, então têm prioridade alta apesar de
algumas serem grandes. Ordenadas por complexidade crescente (e por owner crescente, conforme
§0.5).

> **Atenção à ordem:** apesar de catalogadas aqui na "Fase 6", **T-52 e T-53 são Onda 1** (fazer
> primeiro) e **T-51/T-50 são Onda 2** — ver **§0.7 Ordem de execução**. O número da Fase é só
> agrupamento temático, não ordem de fazer.

### [x] T-50 — Cobertura de ícones de equipamento via AssetExtractor (auto-curado por slot)
**Owner: Sonnet 4.6** (ou Codex) · **P1 · S · tools**

**Concluída (2026-06-12):** `--equipment` auto-seleciona os 5 slots úteis por `clothes.slot`, grava a flag no manifest e exporta os ícones/stats sem incluir legs/feet/backpack.

**Decisão tomada (2026-06-12):** **não** construir um pipeline de importação estática a partir de
`C:\xampp\htdocs\assets`. Analisado e descartado — ver reasoning abaixo. O escopo desta task
encolheu para uma extensão pequena do extractor existente.

**Reasoning da decisão (por que NÃO usar o xampp como fonte de produção).**
- Tudo que é **animado** (outfits/Kaelis, monstros, FX, missiles) **fica no `AssetExtractor`** —
  o xampp é estático e achataria a animação. Isso não está em questão.
- A única coisa que o xampp poderia substituir são **ícones estáticos de item**. Mas o extractor
  **já** exporta itens (categoria `objects`) como PNG single-frame limpo (magenta→transparente,
  ancorado), e a Mochila/`ItemIcon` já consomem isso.
- Com o escopo de equipamento reduzido a **6 slots sem backpack/legs/boots**, **não precisamos**
  dos 31k itens do xampp — só de um subconjunto curado de equipáveis + o loot que já cai.
- O proto tem a flag `clothes.slot` por item → o extractor pode **se auto-curar**: extrair todo
  item com `clothes.slot` nos slots que usamos, **sem listar ID a ID e sem o xampp**.
- Importar do xampp daria **mais** trabalho (normalizar framing inconsistente — é por isso que o
  flag `--static-items` "normaliza" thumbnails — + manter uma **segunda fonte de verdade**), não
  menos. Um pipeline só é mais simples e mais consistente.
- **Conclusão:** o xampp fica como **referência/fallback** (folha de contato para escolher IDs de
  equipamento; plano B se um item específico falhar no decode), **não** como pipeline.

**Instruções (escopo enxuto).**
1. No `AssetExtractor`, ler a flag `clothes` (`AppearanceFlagClothes.slot`, campo 34) ao exportar
   `objects` e expô-la no manifest por item.
2. Modo `--equipment`: extrair automaticamente todo item cujo `clothes.slot` mapeie para os nossos
   slots úteis — **helmet, armor, weapon, amulet/necklace, ring** (ignorar legs/feet/backpack). É a
   fonte de ícones + base de stats para T-51, sem curadoria manual de IDs.
3. Mounts: continuam vindo da categoria creature (animados, via `lookMount`) — o slot `mount` da
   T-51 reaproveita isso; nenhum thumbnail estático de mount é necessário.
4. (Opcional, fallback) deixar documentado no README que, se um item específico não decodificar,
   pode-se copiar o thumbnail correspondente de `C:\xampp\htdocs\assets/thumbnails/items/<id>.png`
   como tapa-buraco pontual — não como fluxo padrão.

**Aceite.** `--equipment` extrai os equipáveis dos 5 slots de inventário (sem legs/boots/backpack)
com `clothes.slot` no manifest; nenhum segundo pipeline/fonte de assets introduzido; o mundo
continua 100% animado pelo extractor.

**Armadilhas.** Não reintroduzir importação estática em massa. Não extrair slots que não usamos
(legs/feet/backpack) — mantém o repo enxuto e o paperdoll coerente com os 6 slots da T-51.

### [x] T-51 — Equipamento simplificado: 6 slots + montaria-como-equipamento
**Owner: Codex → Opus** · **P1 · L · backend + frontend**

**Concluída (2026-06-12):** equipamento por Kaeli com 6 slots, stats do Tibia congelados por run,
paperdoll/Mochila, persistência JSON+MySQL e montarias de boss com bônus e visual no mundo.

**Reasoning do owner.** A spec é fechável (slots fixos, stats vêm dos atributos do Tibia), mas é
**cross-cutting** (drops → inventário → equipar → agregação de stats na run → render de mount) e
toca a fórmula de poder. Codex consegue com a spec abaixo; escale para Opus se as decisões de
balance (quanto cada slot/raridade vale) exigirem julgamento.

**Contexto.** Já temos dungeons e drops do Tibia, mas o loot só serve para vender. Vamos dar
propósito com um sistema de **equipamento enxuto**. O usuário pediu **simplicidade**: apenas
**6 slots** — `helmet, armor, weapon, necklace, ring, mount`. E uma ideia-chave: **montarias do
Tibia são subutilizadas; viram equipamento** (dão status e reaproveitam o visual via `lookMount`).

**Instruções.**
1. **Stats de item vêm do Tibia.** O `items.xml` do Canary tem atributos reais
   (`attack`, `armor`, `defense`, `weaponType`, slot via `AppearanceFlagClothes.slot`). Estenda o
   `AssetExtractor`/`items.json` (T-11) para incluir, por item: `slot` (derivado), `weaponType`,
   `attack`, `armor`, `defense`. Para montarias, derive um stat sintético (ver passo 4).
2. **Modelo de conta:** `AccountState.Equipment[waifuId] = { helmet, armor, weapon, necklace,
   ring, mount }` (cada um um itemId ou vazio). Equipamento é **por waifu** (combina com a
   identidade) — ou por conta, se preferir simplicidade; decida e documente (sugestão: por waifu).
3. **Agregação de stats na run:** ao iniciar a run (`GameWorld`), some os stats do equipamento da
   waifu ativa à fórmula de poder existente (junto de ascensão/cards). Centralize num
   `EquipmentStats` (espelha o `EquipmentStatAggregator` do kaezan-arena). Determinístico: stats
   entram como números no início da run, não mudam durante.
4. **Montaria-como-equipamento:** o slot `mount` aceita itens-montaria (derivados dos mounts do
   Tibia). Dá stats (ex.: velocidade de movimento, HP) **e** troca o `lookMount` renderizado — o
   `AssetExtractor` já lê patterns de mount; a waifu passa a aparecer montada no mundo quando há
   mount equipado. Reaproveita visual ocioso e dá um slot cosmético-funcional.
5. **Drops alimentam o sistema:** equipamento cai como loot (já temos o pipeline de loot); itens
   equipáveis aparecem na Mochila com seus stats; UI de equipar na página Kaelis (paperdoll de 6
   slots, drag/drop ou clique). Vender continua existindo para o que não serve.
6. **Raridade/tiers:** mantenha simples no MVP — o valor do item vem dos atributos do Tibia
   (uma `dragon scale legs` é naturalmente melhor que `leather armor`). Sem afixos aleatórios
   ainda (isso é o Forge, ROADMAP T-32 / futuro).

**Aceite.** Equipar um helmet dropado muda os números da run de forma verificável; equipar uma
montaria dá stats **e** faz a waifu aparecer montada no mundo; os 6 slots funcionam na página
Kaelis; loot equipável é distinguível do loot de venda; determinismo preservado.

**Armadilhas.** Não explodir o escopo para um sistema de itemização completo (afixos, sets,
sockets) — isso é o Forge, fora daqui. Manter exatamente 6 slots. Stats entram no início da run
(nunca recalcular no meio, para não quebrar replay).

### [x] T-52 — Refundação de classes: 4 Kaelis canônicas + stance (substituir kits rasos)
**Owner: Opus → Fable 5** · **P0 · L · backend + frontend** · **Onda 1 (fundação — fazer cedo)**

**Concluída:** 13 waifus migradas para Warrior/Sentinel/Shaman/Wizard, kits canônicos 1-4+R data-driven e stance autoritativa com cooldown por slot.

**Reasoning do owner.** É um **refactor de combate com design real**: colapsar ~19 kits inventados
rasos em 4 classes profundas com mecânica de stance, migrar todas as waifus, e manter o combate
balanceado. Exige julgamento de design e toca o coração do `GameWorld`. Opus como piso; Fable se
a composição com postura/reações (F-E) for feita junto.

**Contexto.** No v0 inventei um kit raso por waifu (`Domain/Waifus.cs`, `Domain/Skills`). O
**Kaezan World** já tinha 4 classes ("Kaelis") com kits reais, espelhados de spells do Tibia
(geometria/dano/cooldown reais), com **stance** (postura): Tab troca o elemento dos slots 1-4.
Fonte: `kaezan/mapping/changes/features/kaeli_spell_library/`. O usuário quer **usar essas 4 como
fundação e criar novas aos poucos**, em vez de muitos kits rasos.

As 4 classes e seus kits (resumo da fonte):
| Classe | Postura(s) | Kit (slots 1-4 + R) |
|---|---|---|
| **Warrior** | — (sem stance) | Groundshaker (nova) · Chivalrous Challenge (taunt) · Front Sweep (cone) · Fierce Berserk · R: Blood Rage (buff) |
| **Sentinel** | Holy ↔ Physical | Divine/Storm Missile (single) · Grenade (area) · Caldera (AoE) · Barrage · R: Sentinel Aegis (buff) |
| **Shaman** | Ice ↔ Earth | Avalanche/Stone Shower · Forked Glacier/Earth Wave · Ice/Terra Burst · Strong Ice Wave/Earth Storm · R: Nature's Embrace (heal) |
| **Wizard** | Energy ↔ Fire | Thunderstorm/Great Fireball · Energy/Fire Wave · Energy/Fire Beam · Rage of Skies/Hell's Core · R: aura (uma por elemento) |

**Instruções.**
1. **Modelo:** introduza `Domain/Classes.cs` com as 4 classes, cada uma com kit por **stance**
   (Warrior tem 1 stance "fixa"). As skills continuam **data-driven por shape** (`single|beam|nova|
   area|cone|buff`) — reaproveite o dispatch existente; não crie switch paralelo. Cada slot tem
   versão por elemento da stance.
2. **Stance (Tab):** comando de hub `ToggleStance`; troca os slots 1-4 entre os dois elementos da
   classe (sem afetar R, que para o Wizard tem aura por elemento). Cooldowns persistem por slot ao
   trocar (não resetar — senão vira exploit). HUD mostra a stance ativa.
3. **Waifu = skin de uma classe.** `WaifuDef` ganha `classId` (warrior|sentinel|shaman|wizard). A
   waifu mantém identidade (nome, raridade, outfit, stats-base, afinidade elemental) mas **o kit
   vem da classe**. Isso colapsa os 19 kits inventados em 4 reais. Migre cada waifu existente para
   a classe de melhor encaixe (melee→warrior; holy/phys ranged→sentinel; ice/earth→shaman;
   fire/energy/death→wizard) e documente o mapeamento.
4. **Geometria/dano/cooldown** das spells: use os valores do Tibia como referência (o
   `kaeli_spell_library` lista quais spells legacy cada uma espelha) e os FX já extraídos
   (`effectId`/`missileId`). Adicione `effectIds/missileIds` faltantes ao content-config e re-extraia.
5. **Frontend:** Kaelis mostra a classe da waifu e o kit por stance; HUD de run mostra a stance e
   permite Tab; preview de skills atualiza ao trocar.
6. **Novas classes "aos poucos":** deixe o modelo aberto para uma 5ª classe (ex.: Necromancer para
   `death`) sem refatorar — só adicionar uma entrada em `Classes.cs`.

**Aceite.** As 4 classes jogáveis com kits reais e stance funcional (Tab troca elemento dos slots
1-4); toda waifu aponta para uma classe; nenhum kit raro inventado remanescente; combate continua
balanceado (jogar tiers 1-5); determinismo preservado.

**Armadilhas.** Não perder a identidade das waifus (elas não viram "4 personagens" — são skins com
stats/visual próprios de uma das 4 classes). Não recriar o dispatch de skill por classe (manter os
shapes). Cooldown ao trocar de stance não pode resetar.

### [x] T-53 — Fidelidade de IA/kit de monstro do Canary (parar de inventar comportamento)
**Owner: Fable 5** · **P0 · L · tools + backend (engine)** · **Onda 1 (fundação — fazer cedo)**

**Concluída (2026-06-12):** conversor emite condition/summon/healing/speed/defenses; engine executa
o kit completo (DoT+chip+FX no player, slow, summons determinísticos com `max`+orçamento global,
self-heal capado a 10% do MaxHp, haste, fuga por `runHealth`); `card:antidote`; +3 espécies
summonáveis (fire elemental, ghost, mummy). Verificado in-game via bot headless (poison DoT,
Necromancer invocando Ghost, self-heal de Ghoul) e determinismo 2×200 ticks idênticos (tiers 3/5).
Gaps registrados: ataques `manadrain` descartados (sem mana no arena) e field attacks (firefield)
fora de escopo.

**Reasoning do owner.** É **determinismo-crítico no hot path** do tick, algoritmicamente sutil
(executar fielmente conditions/summons/healing/speed sem alocar nem desestabilizar a ordem), e de
alto raio de explosão (mexe na IA central de combate que toda run usa). É o perfil clássico Fable 5.

**Contexto.** O usuário apontou: "um demon no canary já possui habilidades e comportamentos
definidos — não precisa recriar essa AI. Você está fazendo isso?" **Resposta honesta: só em
parte.** O `tools/convert-monsters` extrai os *dados* de ataque do `.lua` (interval/chance/range/
dano/elemento/FX), e o `GameWorld.TickMonsters` usa parte disso — mas roda uma **IA genérica** e
**ignora**: `condition` (poison/fire/energy DoT, paralyze), `summon` (spawns), ataques de **cura**
(o boss/healer curando aliados), `speed` (debuff de lentidão), e a distinção fina de
`targetDistance`/estratégias de alvo. Ou seja, hoje um Demon do Canary perde quase toda a sua
identidade de kit.

**Instruções.**
1. **Conversor:** parar de descartar campos. Estender `convert.mjs` para emitir, por ataque:
   `condition { type, totalDamage, tickMs, duration }`, `summon { name, max, chance, interval }`,
   `isHealing` (dano negativo em aliado), `speedChange`, `needTarget`, `areaSpread/length`. (T-14
   cobre só DoT do player; aqui é o kit **completo** do monstro.)
2. **Engine — executar o kit fielmente** dentro do tick, determinístico:
   - **Conditions:** aplicar DoT/paralyze ao player conforme o ataque (compõe com T-14).
   - **Summons:** o monstro invoca outras espécies (respeitando `max` vivo) — novos atores no
     `GameWorld`, contados no orçamento. (Plague Titan/Necromancer ganham vida.)
   - **Healing:** ataques de cura curam o aliado ferido mais próximo (o "Echo Doc"/healer real).
   - **Speed:** debuff de movimento no player por duração.
   - **targetDistance/estratégia:** ranged mantém distância (compõe com `staticAttackChance` do
     T-02); caster fica fora de alcance corpo a corpo.
3. **Mapa de FX/efeitos:** garantir que os `CONST_ME_*`/`CONST_ANI_*` de cada ataque (já no JSON)
   sejam emitidos — o kit do monstro deve **parecer** o do Tibia (a bola de morte do Demon, etc.).
4. **Determinismo:** toda decisão e roll usa o `Rng` da run; summons entram em ordem estável; sem
   `Random`/iteração instável. Isto é pré-requisito de F-A (Echo Team) e F-C (replay).
5. **Bosses ganham kit real:** os 5 bosses passam a usar seus ataques completos do Canary
   (summons do Plague Titan, etc.) em vez do ataque único atual — substitui parte do ROADMAP T-31
   improvisado e dá identidade real a cada luta.

**Aceite.** Um Demon usa seu kit do Canary (death AoE, summons se houver, etc.); Necromancer
invoca; um healer cura aliados; veneno/lentidão funcionam; tudo com os FX corretos do Tibia; e o
teste de determinismo (F-C/T-40) continua verde com summons na cena.

**Armadilhas.** Summons podem explodir a contagem de atores/perf — respeitar `max` e o orçamento.
Cura pode tornar salas intransponíveis — capar e testar. Não introduzir não-determinismo (o maior
risco). Não recriar comportamento "à mão" quando o `.lua` já define — a fonte de verdade é o dado
convertido.

### [x] T-54 — Persistência em MySQL: banco `kaezan_fable` separado (sair do JSON)
**Owner: Opus** · **P0 · L · backend (infra) ·** **Onda 1 (fundação — track paralelo, independe de T-52/T-53)**

**Concluída:** `AccountStore` abstraído com fallback JSON, EF Core/Pomelo + migrations no banco isolado `kaezan_fable` e importação one-shot da conta local.

**Reasoning do owner.** É arquitetura de dados com decisões reais (schema, separação de fronteiras,
migração do JSON, manter a abstração) e alto raio de explosão (camada de dados de todo o `Meta`).
Não é determinismo-crítico (fica **fora** do tick), então não é Fable; mas pede julgamento de
design → **Opus**. A fiação mecânica do EF Core, depois do schema decidido, um Codex toca.

**Contexto.** Hoje a conta é um **único JSON** (`Meta/AccountStore.cs` → `.data/account.json`,
reescrito inteiro a cada mutação). Funciona para 1 conta local, mas não escala para o que vem:
equipamento por waifu (T-51), maestria (F-B), histórico de gacha (T-23), replays e **leaderboard
do Desafio Diário** (F-C) — leaderboard precisa de query ("top 100 de hoje"), que JSON não dá. O
usuário já tem **MySQL rodando no XAMPP** (o Canary usa o banco `otservbr-global`).

**Duas decisões de arquitetura (as partes que importam):**
1. **Banco SEPARADO `kaezan_fable` — nunca dentro de `otservbr-global`.** O banco do Canary é
   outro app, com schema/migrations próprios; misturar arriscaria corromper o servidor e seria um
   pesadelo de manutenção. Confirma o instinto do dono ("outro banco de dados"). Só compartilham a
   instância MySQL.
2. **DB = estado MUTÁVEL do jogador; conteúdo continua em código/JSON.** Vai para o banco: contas,
   roster/ascensão, equipamento, inventário, maestria, pity/histórico de gacha, bestiário,
   contratos diários, run_results, replays, scores de desafio. **NÃO** vai para o banco o
   **conteúdo de design** (monstros, itens, defs de waifu/classe, cards, biomas) — isso é versionado
   com o código (determinismo e balance não devem depender de escrever no DB). Static = código;
   dinâmico = DB.

**Por que o risco é baixo (já confirmado no código).** A persistência já vive **só nas fronteiras
da run**: `GameHub.JoinRun` lê a conta **uma vez** para construir o `GameWorld` (passa um snapshot
de bestiário); `RewardService.Apply` escreve **uma vez** no fim. **Nada de DB dentro do tick.**
Trocar JSON→MySQL é só trocar o backing de `AccountStore` — a fronteira de determinismo já está de pé.

**Instruções.**
1. **Abstração primeiro:** introduza `IAccountRepository` (ou reuse o padrão `IAccountStatePersistence`
   do kaezan-arena). Duas implementações: `JsonFileAccountRepository` (default, zero-dependência —
   preserva o boot sem MySQL para CI/clone rápido) e `MySqlAccountRepository`. Seleção por
   connection string em `appsettings.json` + override por env var; sem connection string → JSON.
2. **EF Core + Pomelo (`Pomelo.EntityFrameworkCore.MySql`)**: `DbContext` + migrations. Schema
   inicial (player state):
   - `accounts(id, level, xp, gold, kaeros, active_waifu_id, daily_date, runs_played, runs_won, ...)`
   - `account_waifus(account_id, waifu_id, ascension, shards)`
   - `account_equipment(account_id, waifu_id, slot, item_id)` — os 6 slots (T-51)
   - `account_inventory(account_id, item_id, count)`
   - `account_mastery(account_id, waifu_id, node_id)` + `account_mastery_points(account_id, waifu_id, points, spent)` (F-B)
   - `gacha_pity(account_id, banner_id, since_5, since_4, guaranteed, total)`
   - `gacha_history(id, account_id, banner_id, waifu_id, rarity, ts)` (T-23)
   - `bestiary(account_id, species, kills)`
   - `daily_contracts(account_id, date, contract_id, progress, claimed)`
   - `run_results(id, account_id, seed, tier, waifu_id, outcome, kills, run_level, duration_ms, ts)`
   - `replays(id, account_id, seed, tier, commands_json, final_hash, ts)` (F-C)
   - `daily_challenge_scores(date, account_id, score, time_ms)` — índice por (date, score) p/ leaderboard (F-C)
3. **Migração one-shot:** se `.data/account.json` existir e o banco estiver vazio, importar para o
   MySQL no primeiro boot (não perder a conta local de testes).
4. **Manter a API de `AccountStore`** (`Read`/`Mutate`) como fachada — o resto do `Meta` não muda;
   só o backing. Transações onde fizer sentido (ex.: pull de 10 + atualização de pity atômicos).
5. **Não tocar no `Engine`.** Nenhum `using` de EF/DB em `Engine/`. Reforce isso com um comentário
   no `GameWorld` e, se possível, um teste que falhe se `Engine` referenciar `Meta`/DB.

**Aceite.** Com connection string configurada, conta/roster/inventário/pity persistem no banco
`kaezan_fable` (separado do `otservbr-global`); sem connection string, o jogo ainda boota em JSON;
a conta local existente é migrada; nenhum acesso a DB dentro do tick; `dotnet build` verde e uma
run completa (join → matar boss → reward) persiste corretamente.

**Armadilhas.** **Jamais** criar tabelas dentro de `otservbr-global`. Não mover conteúdo de design
para o DB (vira acoplamento + quebra de determinismo/balance). Não acessar o DB no `GameWorld`/tick.
Não tornar o MySQL obrigatório para o boot (manter o fallback JSON).

---

## Tabela-mestra de atribuição (todas as tasks e features)

> Visão única de quem implementa o quê. `→` = piso → teto (escale se a spec deixar margem de
> design). Tasks `T-*` neste arquivo; features `F-*` em [FABLE_TRACK.md](FABLE_TRACK.md).

| Item | Título curto | Owner | Por quê (reasoning) |
|---|---|---|---|
| T-01..T-04 | Movimento, IA, pausa de card, reconexão | **Codex** ✅ | Specs fechadas; já concluídas. (T-04 toca determinismo levemente → ok Codex.) |
| T-10 | +30 monstros | **Codex** | Curadoria + re-rodar tools; **depois de T-53** (herdam kit real). |
| T-11 | Preços reais de itens | **Codex** | Extração + fiação; bounded. |
| T-12 | Biomas + cantos de parede | **Opus** | Geração com curadoria visual e mapeamento de tiles; decisão de design. |
| T-13 | 6 waifus novas | **Sonnet → Codex** | Conteúdo declarativo; vira trivial **depois** de T-52 (waifu = skin de classe). |
| T-14 | Condições (poison/burn) do player | **(absorvida por T-53)** | Subconjunto do kit de monstro; não fazer isolada. |
| T-20 | Juice de combate | **Sonnet → Codex** | Apresentação no renderer; sem lógica de simulação. |
| T-21 | HUD informativo | **Codex** | Frontend bounded. |
| T-22 | Minimapa fog of war | **Sonnet** | Pequeno, isolado, frontend. |
| T-23 | Cerimônia do gacha | **Codex** | Frontend com alguma orquestração. |
| T-24 | Polish de meta | **Sonnet** | Itens pequenos de UI. |
| T-30 | Sealed Reward + reroll | **Codex → Opus** | Backend+frontend bounded; design de tabela de loot pode pedir Opus. |
| T-31 | Boss Posture (MVP) | **(substituída por F-E)** | Fazer a postura completa direto na F-E. |
| T-32 | Imbuements lite | **Opus** | Economia + balance; cross-cutting. |
| T-33 | Replay determinístico | **(parte de F-C)** | Entrega 2 da F-C; não fazer isolada. |
| T-40 | Testes (determinismo/regras) | **Opus → Fable 5** | Testes de determinismo exigem entender o invariante a fundo. |
| T-41 | Fallback de asset + diagnóstico | **Sonnet** | Pequeno, frontend+tool. |
| T-42 | Limpeza de dívidas | **Sonnet → Codex** | Itens pontuais; alguns tocam o engine (facing) → Codex nesses. |
| **T-50** | **Ícones de equipamento via extractor (auto-curado por slot)** | **Sonnet** | Extensão pequena do extractor; importação estática do xampp avaliada e descartada (um pipeline só). |
| **T-51** | **Equipamento (6 slots, mount-as-gear)** | **Codex → Opus** | Spec fechável, mas cross-cutting e toca a fórmula de poder. |
| **T-52** | **Refundação de classes + stance** | **Opus → Fable 5** | Refactor de combate com design real; coração do `GameWorld`. |
| **T-53** | **Fidelidade de IA/kit de monstro** | **Fable 5** | Determinismo-crítico no hot path; IA central. |
| **T-54** | **Persistência MySQL (banco `kaezan_fable` separado)** | **Opus** | Arquitetura de dados; fora do tick (não é Fable), mas alto raio e decisões de schema/migração. |
| F-A | Echo Team (companions) | **Fable 5** | IA de aliado determinística + anti-bodyblock + balance. Flagship. |
| F-B | Árvore de Maestria | **Opus** | Design-heavy, mas fora do hot path determinístico. |
| F-C | Determinismo + Desafio Diário + harness | **Fable 5** | Literalmente sobre o invariante de determinismo. |
| F-D | Geração procedural v2 | **Opus → Fable 5** | Algorítmico com garantias; roda no seed. |
| F-E | Postura completa + reações elementais | **Opus** | Composicional; ancorada pelo MVP T-31. Fable se feita junto com T-52. |

**Dependências que afetam a ordem:**
- **T-52 (classes)** deve vir **antes** de T-13 (waifus novas viram triviais) e idealmente antes
  de F-A/F-B (time e maestria assumem o modelo de classe).
- **T-53 (kit de monstro)** é pré-requisito de qualidade para F-A (a IA aliada e inimiga
  compartilham padrões) e para F-C (replay com summons).
- **T-54 (MySQL)** deve **preceder** T-51 (equipamento), T-23 (histórico de gacha), F-B (maestria)
  e F-C (leaderboard) — todos guardam estado novo. É track paralelo, não bloqueia T-52/T-53.
- **T-11 (preços/atributos)** é pré-requisito de **T-51 (equipamento)** (stats vêm dos atributos).
- **T-50 (assets)** destrava UI melhor para T-24/T-23/Mochila/Bestiário.

**Sequência de execução:** ver **§0.7 Ordem de execução** (a fonte da verdade). Resumo das ondas:
Onda 1 fundação **T-52 + T-53 + T-54** → Onda 2 itemização/elenco **T-11 → T-51 → T-50 → T-10 →
T-13** → Onda 3 **F-A** → Onda 4 **F-E → T-30 → F-B** → Onda 5 juice/UX (paralelo) → Onda 6
**F-C/F-D/T-40**.

---

## Backlog (sem spec ainda — promover a task quando chegar a vez)

- Montarias como drop raro de boss (extractor já lê patternZ de mount nos outfits).
- Segundo modo de jogo: "Echo Spot" rápido de 1 sala com waves (budget já existe) para daily.
- Eventos de mundo / elemento do dia (kaezan-arena tinha; trazer com FX do Tibia).
- Expedições idle de waifus fora da equipe (pet expedition).
- Wheel-like de progressão por waifu (mastery) — ver `kaezan/mapping/changes/features/kaeli_mastery/`.
- Acessibilidade: remapeamento de teclas, daltônico (cores de dano).
- i18n EN (strings hoje em PT-BR espalhadas nos templates).
- Multiplayer co-op (grande; exigiria revisitar snapshot por conexão e occupancy).
