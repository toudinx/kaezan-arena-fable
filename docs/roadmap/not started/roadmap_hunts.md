# Roadmap — Sensação de Hunt (visual + lure/box estilo Tibia)

> **Como usar este arquivo.** Cada `H-NN` abaixo é uma unidade de trabalho **auto-contida**: o agente
> que executa começa "frio", então o prompt já traz o contexto que precisa. Você dispara com
> **"implemente o prompt H-NN do `docs/roadmap/not started/roadmap_hunts.md`"** e o agente faz o resto.
>
> Cada prompt declara: **Modelo · Effort · Skill · Depende de · Aceite · Verificação.** Há dependências
> reais — a maior parte do trabalho mora num único arquivo (`Engine/DungeonGenerator.cs`), então o
> **conflito de arquivo força uma espinha serial**; o paralelismo vem das duas trilhas laterais (aggro
> em `GameWorld`, assets no extractor).
>
> **Não confundir com:** `docs/roadmap/ongoing/roadmap_dungeons.md` (variedade de **modos** — arena,
> editor de biomas; a LM-13 de lá enriquece o catálogo de tiles e cruza com a H-08 daqui),
> `docs/roadmap/done/roadmap_refactor_kaelis.md` (roster/kits) e `docs/ROADMAP.md` (tasks pequenas Codex).
> Este arquivo é a **terceira entrada do `## Depois` do roadmap_dungeons** ("dungeon procedural mais
> rica"): aproximar a hunt procedural das hunts do Tibia em **visual** (salas orgânicas, paredes-maciço,
> chão em manchas) e em **jogabilidade** (espaço pra dar *overlure* e *fechar box*). Toca
> **principalmente o backend** (`Engine/DungeonGenerator.cs`, `Engine/GameMode.cs`, `Engine/GameWorld.cs`,
> `Domain/GameConfig.cs`), mais o pipeline de assets (`tools/AssetExtractor`).

---

## Modelos & quando usar

| Modelo | Papel | Effort típico | Por quê |
|---|---|---|---|
| **Claude Code Opus 4.8** | Algoritmo de geração (autômato celular, choke/box, archetype de caverna), tuning de aggro/lure, leitura visual do mapa, qualquer coisa onde errar cascateia no determinismo/feel | `high` / `medium` | Decisão de game design + invariantes de engine (determinismo, backend autoritativo). Vale o modelo premium. |
| **GPT-5.5 (Codex)** | Mudanças bounded com regra fechada: ruído de chão value-noise mecânico, curadoria de ids de tile no extractor | `low` / `medium` | Regra explícita e padrão a seguir. Barato e rápido. |

- Use **`use context7`** ao consultar API de biblioteca (ASP.NET Core, SignalR, Angular) nos prompts.
- **Nenhuma skill é necessária** — as decisões de design vivem nos próprios prompts.

---

## Invariantes inegociáveis (todo prompt respeita)

- **Backend autoritativo.** Frontend nunca simula combate/movimento — só interpola e renderiza.
- **Determinismo do engine.** Geração e tick usam **apenas** o `Rng` da run (xorshift seedado). Nunca
  `Random`, `DateTime.Now`, `Guid.NewGuid()`, ou iteração de coleção sem ordem estável. O autômato
  celular, o value-noise e a escolha de choke/box **têm de ser determinísticos** (só `Rng`).
- **Todas as constantes de simulação em `Domain/GameConfig.cs`.** Nada de hardcode de tamanho/densidade/
  threshold no gerador ou no tick.
- **IDs estáveis** (espécies do Tibia, ids de POI, `GameMode` numérico) não são renomeados.
- **Rede-ouro obrigatória.** Toda mudança que altera a saída do gerador **rebaselina conscientemente** o
  teste-ouro (`tools/BalanceSim --golden` → `docs/balance/golden_dungeon.txt`, da LM-01) e confirma
  `--golden-check` **verde 2× seguidas** (prova de determinismo). Rebaseline é *intencional*, nunca pra
  "calar" não-determinismo.
- **Assets do Tibia por enquanto.** Tileset próprio segue fora de escopo (ver `## Depois` do roadmap_dungeons).
- `dotnet build` (backend) e `npx ng build` (frontend) passam sem erro ao fim de cada prompt que tocar o respectivo lado.

---

## Tese

As hunts geradas hoje leem como "geradas": salas **retangulares** de 5–9 tiles, paredes de **1 tile**
(famílias degeneradas — na cave `WallCorner == WallH`, sem canto real), chão em **ruído per-célula** e
decor escasso. Lado a lado com uma hunt do Tibia (caverna orgânica, parede-montanha espessa, respawn
espalhado num campo aberto), a nossa perde em **beleza** e em **jogabilidade**: sala pequena + corredor
de 2 tiles não dá pra **juntar uma pilha** (overlure) nem pra **afunilar num choke de 1** e tankar
(fechar box).

A visão é aproximar dos dois eixos **sem trocar de tileset** (assets do Tibia continuam servindo),
mexendo na **estrutura** da geração:

- **Visual:** salas orgânicas (autômato celular), parede que lê como **maciço de rocha** (anel espesso +
  borda decorada), chão em **manchas** coerentes em vez de ruído.
- **Jogabilidade:** salas **maiores e menos numerosas**, corredores largos (2–3, nunca 1) e um **nicho de
  box 3×3 com boca de 1 tile** dentro da sala (o choke mora na alcova, não no corredor), um **archetype de
  caverna aberta** (campo único com mobs espalhados — o que destrava o
  overlure de verdade), e **persistência de aggro** afinada pra um train deliberado não evaporar.

Cauteloso e incremental: cada peça é uma unidade pequena, verificada contra a rede-ouro, reusando as
primitivas que a LM-07 já deixou limpas (bedrock fill, auto-tiling, decor agrupado).

## Decisões Fechadas

- **Não trocar de tileset.** Tudo reusa os assets do Tibia já extraídos; B4 (H-08) só *enriquece* o
  catálogo (família de parede-montanha), não desvincula.
- **O lure já funciona mecanicamente** — monstros agarram por aggro + linha de visão e perseguem o player
  fora da sala (`GameWorld.cs:3284-3355`). G4 (H-07) é **tuning de persistência**, não "fazer o mob seguir".
- **Rooms-and-corridors continua existindo**, melhorado (orgânico, maior, com box). A **caverna
  aberta (G1/H-06) é um archetype adicional**, não uma reescrita — escolhido por andar no `BuildFloors`.
- **Corredores NUNCA têm 1 sqm — sempre 2–3 tiles** (`CorridorWidthMin=2`/`CorridorWidthMax=3`, pincel
  quadrado em `CarveCorridor`). Decisão do dono: corredor estreito vira "rachadura" e pincha o movimento.
  Consequência: o choke do "fechar box" (H-03) **não vem mais do corredor** — é a **boca de 1 tile de uma
  alcova de box** carvada dentro da sala. O corredor continua largo.
- **A caverna aberta se representa como `List<Room>` de zonas grandes** (sub-regiões de spawn que ladrilham
  o campo), pra o povoamento por-sala (`GameWorld.SpawnFloorMonsters`) e o orçamento de spawn seguirem
  **intactos** — G1 não toca `GameWorld`. Isso mantém H-06 e H-07 em arquivos disjuntos.
- **Determinismo acima de tudo:** autômato celular e value-noise rodam no `Rng` da run; sem isso, a
  rede-ouro pega.
- **47-blob pleno de parede fica fora** (depende de arte própria, ver `## Depois`); B2/B4 melhoram dentro
  das peças que existem + a família de montanha do Tibia.

---

## Estado atual (âncoras medidas)

| Constante (`Domain/GameConfig.cs`) | Valor hoje | Efeito |
|---|---:|---|
| `Floor1Size` / `Floor2Size` | 40 / 30 | lado do andar (normal / boss) |
| `RoomMin` / `RoomMax` | 5 / 9 | sala média ~7×7 — pequena demais p/ pilha |
| `RoomsFloor1` / `RoomsFloor2` | 8 / 4 | muitas salinhas em vez de poucas grandes |
| `CorridorWidthMin` / `CorridorWidthMax` | **2 / 3** (pincel quadrado em `CarveCorridor`) | corredor nunca pincha em 1 sqm; box vem da alcova (H-03), não do corredor |
| `DecorDensityScale` / `ClusterFalloff` | 0.5 / 0.45 | decor escasso |
| `MonsterAggroRange` / `AggroDropRange` / `AggroDropOutOfRangeMs` / `AggroDropNoLosMs` | (ver arquivo) | governam quanto tempo um train segue antes de soltar |

Peças do pipeline `DungeonGenerator.Generate` (ordem real): **placa rooms** (`:63-74`) → **carve rooms**
(`:77-80`) → **ConnectRooms/CarveCorridor** (`:101`,`:229`) → **AssignRoles** (`:140`) → **PaintTiles**
(`:244`: walls `ClassifyWall :336`, ground `:259`, decor `PaintClusters :300`).

---

## Mapa de prompts (escopo)

| Prompt | Tema | Modelo | Effort | Depende de | Onda |
|---|---|---|---|---|---|
| H-01 | G2 — Salas maiores, menos numerosas (constantes + placement) | Opus 4.8 | medium | — | 1 |
| H-08 ✅ | B4 — Catálogo de parede-montanha (extractor) | GPT-5.5 (Codex) | medium | — | 1 |
| H-02 ✅ | B1 — Salas orgânicas (autômato celular) ⭐ fundação visual | Opus 4.8 | high | H-01 | 2 |
| H-07 ✅ | G4 — Persistência de aggro / overlure (tuning) | Opus 4.8 | medium | — (valida c/ H-06*) | 2 |
| H-03 | G3 — Nicho de box 3×3 c/ boca de 1 tile (corredor fica largo) | Opus 4.8 | high | H-02 | 3 |
| H-04 | B2 — Paredes-maciço (anel espesso + borda decorada) | Opus 4.8 | medium | H-03 | 4 |
| H-05 | B3 — Chão em manchas (value-noise) | GPT-5.5 (Codex) | medium | H-04 | 5 |
| H-06 | G1 — Archetype de caverna aberta ⭐ destrava overlure | Opus 4.8 | high | H-02, H-03, H-04 | 6 |
| H-09 | Verificação & balance de hunt (golden + playtest lure/box) | Opus 4.8 | medium | H-01…H-08 | 7 |

> O `*` em H-07 marca dependência **branda**: o código do tuning de aggro é independente da geração, mas o
> *feel* só se valida bem num espaço aberto (H-06). Implemente o tuning na Onda 2; afine os números na H-09.
>
> **A espinha H-01→H-02→H-03→H-04→H-05→H-06 é serial de propósito:** todos editam
> `Engine/DungeonGenerator.cs`. Conflito de arquivo mata paralelismo — não há como rodar dois desses
> juntos sem merge hell. O paralelismo real são as **duas trilhas laterais** (H-08 nos assets, H-07 no
> `GameWorld`), que saem "de graça" nas Ondas 1–2.

---

## Execução paralela ⭐

**Regra de ouro:** dois prompts só rodam em paralelo se (a) as dependências fecharam **e** (b) não
editam o mesmo arquivo. Casamento natural: 1 Opus + 1 Codex por onda.

```
Onda 1   H-01 (Opus · G2 salas maiores, DungeonGenerator)  ‖  H-08 (Codex · B4 assets, tools/AssetExtractor)
              │                          arquivos disjuntos: backend C# vs extractor/assets
              ▼
Onda 2   H-02 (Opus · B1 orgânico, DungeonGenerator)        ‖  H-07 (Opus · G4 aggro, GameWorld+GameConfig)
              │                          disjuntos: DungeonGenerator vs GameWorld (aggro ≠ spawn)
              ▼
Onda 3   H-03 (Opus · G3 nicho de box c/ boca de 1 tile, DungeonGenerator, solo)
              ▼
Onda 4   H-04 (Opus · B2 paredes-maciço, DungeonGenerator/PaintTiles, solo)
              ▼
Onda 5   H-05 (Codex · B3 chão em manchas, DungeonGenerator/PaintTiles, solo)
              ▼
Onda 6   H-06 (Opus · G1 caverna aberta, DungeonGenerator + GameMode, solo)
              ▼
Onda 7   H-09 (Opus · verificação & balance, solo — precisa de tudo fechado)
```

**Conflitos que forçam sequencial:**
- **H-01 → H-02 → H-03 → H-04 → H-05 → H-06** — todos tocam `Engine/DungeonGenerator.cs` (e H-06 também
  `GameMode.cs`). Espinha serial; cada um reusa/edita o que o anterior deixou no pipeline `Generate`.
- **H-07 fica isolada em `GameWorld.cs` (aggro `:3284-3355`) + `GameConfig.cs`** — disjunta da espinha,
  então paraleliza; só cuidar de não colidir com H-09 (que só lê/verifica). G1 (H-06) **não** toca
  `GameWorld` por decisão fechada (caverna = `List<Room>` de zonas), então não conflita com H-07.
- **H-08 cruza com a LM-13 do roadmap_dungeons** (ambas editam `tools/AssetExtractor/content-config.json`).
  Se a LM-13 for rodar, **sequencie** as duas (não paralelize) e H-08 cobre só a **família de parede-
  montanha**; LM-13 cobre as famílias de chão.

**Caminho crítico:** H-01 → H-02 → H-03 → H-04 → H-05 → H-06 → H-09. H-07 e H-08 saem em paralelo cedo.

---

# H-01 — G2: Salas Maiores, Menos Numerosas

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** H-08 (Onda 1)

> **[x] H-01 — feito.** Salas maiores e menos numerosas (`RoomMin/Max 5/9→9/16`, `RoomsFloor1/2 8/4→5/3`),
> andares ampliados pra caberem (`Floor1/2Size 40/30→52/42`) + `RoomPlacementAttempts=300`; orçamento de
> spawn passou a escalar por área com teto maior (`SpawnRoomAreaBaseline=120`, clamp `0.6–2.2`) pra a sala
> grande não nascer vazia. Todas as 7 seeds × 5 tiers da rede-ouro fecham os alvos (normal=5, boss=3, sem
> andar degenerado); golden rebaselinado + `--golden-check` verde 2×; `dotnet build` limpo.

**Objetivo:** dar **espaço** pra hunt. Salas de 5–9 tiles não cabem uma pilha de mobs; o Tibia hunta em
poucas cavernas grandes. Aumentar o tamanho das salas e reduzir a contagem, ajustando o orçamento de
spawn pra a sala maior não nascer vazia. É a fundação de geometria sobre a qual B1/G3/G1 assentam.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs:316-322` — `Floor1Size=40`, `Floor2Size=30`,
  `RoomMin=5`, `RoomMax=9`, `RoomsFloor1=8`, `RoomsFloor2=4`.
- `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs:63-74` — loop de placement (200 tentativas,
  `rng.Range(RoomMin,RoomMax)`, teste de overlap com margem +2). `:67` força a boss hall 11×9.
- Orçamento de spawn que escala com a área: `GameWorld.cs:409-413`
  (`sizeFactor`/`SpawnBudgetBase`/`SpawnBudgetTierGrowth`, clamp 0.6–1.4). Sala maior → conferir se o
  `sizeFactor`/clamp ainda enche a sala (senão a sala grande lê vazia).
- Rede-ouro: `tools/BalanceSim --golden` / `docs/balance/golden_dungeon.txt`.

**Tarefas:**
- Subir `RoomMax` (alvo ~14–18) e `RoomMin` (alvo ~8–10); reduzir `RoomsFloor1` (~5) e rever `RoomsFloor2`.
  Conferir que `Floor1Size`/`Floor2Size` comportam poucas salas grandes sem o placement falhar (se preciso,
  subir o tamanho do andar e/ou as 200 tentativas).
- Ajustar o clamp/scale do orçamento de spawn (`GameWorld.cs:409-413`) pra a densidade por tile continuar
  coerente numa sala maior — alvo: dá pra juntar ≥10–15 mobs num andar sem ficar vazio nem sobrelotado.
- **Todas** as constantes novas/alteradas em `GameConfig.cs`; nada hardcoded no gerador.
- Rebaselinar a rede-ouro conscientemente (a saída muda de propósito).

**Aceite:**
- Salas visivelmente maiores e em menor número; o andar normal acomoda uma pilha jogável de mobs.
- Placement não falha (sem andar com 1 sala por falta de espaço) nas 7 seeds × 5 tiers da rede-ouro.
- Densidade de spawn coerente (sala grande não nasce vazia). Constantes em `GameConfig.cs`.
- Determinístico; `--golden-check` verde 2× contra a baseline nova.

**Verificação:** `cd backend/src/KaezanArenaFable.Api && dotnet build` limpo. `tools/BalanceSim --golden`
(rebaseline) → `--golden-check` verde 2×. Via preview MCP, rodar tier 1: confirmar salas grandes e mobs
suficientes pra formar uma pilha.

---

# H-02 — B1: Salas Orgânicas (autômato celular)  ⭐ fundação visual

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** H-01 · **Paraleliza com:** H-07 (Onda 2)

> **[x] H-02 — feito.** Cada sala carvada passa por um `ErodeRoom` (autômato celular determinístico,
> regra 4-5) no `DungeonGenerator`: só o anel de borda (`OrganicSeedBand=3`) é semeado com rocha
> (`OrganicFillProb=0.45`), `OrganicCaIterations=4` suavizam o contorno (fora do retângulo conta como
> rocha → cantos erodem), e um flood-fill 4-way do centro (forçado aberto) mantém só o componente
> conectado. Corredores carvam centro↔centro depois, então a conectividade nunca quebra. POIs de canto
> (chest elite/extra) passam por `OpenCellInRoom` pra não caírem em rocha. Parâmetros em `GameConfig.cs`
> (`Organic*`). Golden rebaselinado + `--golden-check` verde 2×; harness de reach confirma 280/280 salas
> erodidas, 0 centros inalcançáveis, 0 POI bloqueado nas 7 seeds × 5 tiers.

**Objetivo:** matar a cara "gerada" das salas retangulares. Hoje o carve é um retângulo literal
(`DungeonGenerator.cs:77-80`); o Tibia é orgânico (blobs irregulares de terra). Erodir cada sala com um
passe de **autômato celular determinístico** quebra as retas e dá contorno natural — o maior ganho visual
isolado, sem asset novo.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` — `Generate` (`:44`), carve de salas
  (`:77-80`), `Room.Contains` (`:12`). O carve hoje só seta `Blocked[i]=false` no retângulo.
- A pintura de parede (`PaintTiles`/`ClassifyWall :336`) já é **agnóstica de forma** — classifica pela
  vizinhança de chão aberto, então paredes orgânicas auto-tilam de graça (a LM-07 deixou isso pronto).
  Conferir que o bedrock-fill (`:269-279`) cobre os novos vãos internos sem void preto.
- `Engine/Rng.cs` — `rng.Chance(p)` / `rng.Range(...)` pro CA determinístico.
- Conexão/POI assumem `Room.CenterX/CenterY` como ponto aberto — garantir que a erosão **nunca fecha o
  centro** (corredores ligam centro a centro; centro bloqueado = sala inalcançável).
- Rede-ouro: `tools/BalanceSim --golden`.

**Tarefas:**
- Após carvar o retângulo da sala, rodar **N iterações de autômato celular** (regra clássica 4-5: célula
  vira parede se ≥5 vizinhos-8 são parede; vira chão se ≤3) **restritas ao retângulo da sala**, semeadas
  por `rng.Chance(fillProb)` nas bordas — produz contorno irregular sem ilhas que desconectem.
- **Garantir conectividade:** o `CenterX/CenterY` e um caminho até ele permanecem abertos (ex.: re-abrir o
  centro + um flood-fill que repreenche bolsões desconectados, ou semear o fill só perto das bordas).
  Idealmente um pós-passe que mantém só o maior componente conectado da sala e religa ao centro.
- Parâmetros (iterações de CA, `fillProb`, threshold) em `GameConfig.cs`.
- Manter determinismo (só `Rng` da run, ordem de varredura estável). Rebaselinar a rede-ouro.

**Aceite:**
- Salas têm contorno **orgânico** (sem retângulo perceptível) e continuam todas conectadas/alcançáveis.
- Sem void preto interno novo (bedrock-fill cobre); paredes auto-tilam nas curvas sem "dente".
- Determinístico; parâmetros em `GameConfig.cs`; `--golden-check` verde 2× contra baseline nova.

**Verificação:** `dotnet build` limpo. `tools/BalanceSim --golden` (rebaseline) → `--golden-check` verde 2×.
Conferir nos dados do mapa (ou no `--golden`) que toda sala tem caminho centro↔entry. Via preview MCP,
screenshot tier 1: salas com borda irregular, sem void, paredes limpas.

---

# H-03 — G3: Nicho de Box 3×3 com Boca de 1 Tile

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** H-02 · **Paraleliza com:** — (solo, Onda 3)

> **⚠️ Revisado (decisão fechada do dono):** **corredores NUNCA têm 1 sqm** — sempre 2–3 tiles de largura
> (já implementado: `CorridorWidthMin=2`/`CorridorWidthMax=3` + pincel quadrado em `CarveCorridor`). Logo o
> "fechar box" **não vem mais de afunilar o corredor**. O choke de 1 tile passa a ser a **boca da alcova de
> box**, carvada *dentro da sala* — o corredor continua largo, mas o ponto de tank é uma alcova com entrada
> única de 1 tile. Esta task agora é **só o nicho de box**; nada de mexer na largura do corredor.

**Objetivo:** destravar o **fechar box**. O corredor é largo (2–3) de propósito — bom pra trazer o train,
ruim pra tankar. A tática clássica vira: lurar a pilha na sala aberta, recuar pra uma **alcova de box** (nicho
~3×3 encostado na parede da sala) cuja **única entrada é de 1 tile**; os mobs entram em fila por essa boca
enquanto você tanka batendo só nos da frente. O choke de 1 tile mora **na boca da alcova**, não no corredor.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` — `CarveCorridor` (já 2–3 wide via pincel
  quadrado; **não tocar a largura**). O nicho é um pós-passe que carva uma alcova depois das salas e dos
  corredores, fechando-a com parede menos uma boca de 1 tile.
- `Room`/`AssignRoles` — escolher uma parede interna da sala (longe da boca do corredor pra a alcova não
  abrir direto no fluxo) e carvar lá a alcova + a boca de 1 tile. Determinístico (posição via `Rng` +
  geometria da sala).
- O movimento/nav do player e do mob respeita `Blocked`/BFS (`GameWorld.cs`, `NextNavStep`), então uma boca
  de 1 tile **já afunila** o pathing — é só geometria.
- A erosão orgânica da H-02 deixa a parede da sala irregular: garantir que a alcova é carvada em rocha
  adjacente à sala (não cria void solto) e que sua boca encosta em chão aberto da sala.
- Rede-ouro: `tools/BalanceSim --golden`.

**Tarefas:**
- Na **parede de cada sala de combate** (`Role=="mob"/"elite"`), carvar um **nicho de box** ~3×3 encostado
  na rocha, com **uma única entrada de 1 tile** voltada pra dentro da sala. Determinístico (parede/posição
  derivadas do `Rng` + geometria da sala). Entry/ladder/boss/sanctuary podem ficar de fora.
- **Não alterar a largura do corredor** (fica 2–3, decisão fechada). O único choke de 1 tile é a boca da alcova.
- Parâmetros (tamanho do nicho, largura da boca = 1, quais roles recebem, distância mínima da boca do
  corredor) em `GameConfig.cs`.
- Cuidar pra a alcova não criar bolsão inalcançável (BFS de sanidade — a boca de 1 tile tem de ligar à sala),
  não sobrepor o POI da sala e não estourar a borda do andar.
- Rebaselinar a rede-ouro.

**Aceite:**
- Corredores seguem 2–3 wide (inalterados); o player/mob **afunila em fila só na boca da alcova de box**.
- Salas de combate têm uma alcova ~3×3 com entrada única de 1 tile, sem quebrar conectividade nem sobrepor POI.
- Determinístico; parâmetros em `GameConfig.cs`; `--golden-check` verde 2× contra baseline nova.

**Verificação:** `dotnet build` limpo. `tools/BalanceSim --golden` (rebaseline) → `--golden-check` verde 2×.
Via preview MCP: lurar uma pilha numa sala, recuar pra alcova e confirmar que os mobs entram em fila pela
boca de 1 tile; tankar só a frente. Screenshot da alcova de box (boca de 1 tile, corredor largo ao redor).

---

# H-04 — B2: Paredes-Maciço (anel espesso + borda decorada)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** H-03 · **Paraleliza com:** — (solo, Onda 4)

**Objetivo:** dar **profundidade** à parede. No Tibia a parede é um **maciço de montanha** de vários tiles
com ridge/sombra; aqui é uma borda fina de 1 tile (e famílias degeneradas — na cave `WallCorner==WallH`).
A LM-07 já preenche o miolo com bedrock; falta a **espessura de borda** e a **decoração de ridge** pra o
negativo do mapa ler como rocha sólida, não como contorno de 1px.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs:244-292` — `PaintTiles` (pass 1 ground+wall,
  pass 2 decor/accent). `ClassifyWall` (`:336`) escolhe a peça de borda; bedrock-fill em `:269-279`.
- `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs:24-29` (`BiomeDef`), `:63-73` (peças de parede/bedrock
  por bioma; `CaveRocks`/`Bones` são decor candidatos a "ridge" de borda).
- `frontend/src/app/core/renderer.ts` — desenho de ground/wall/decor (a LM-07 anota ~`:630-745`); a parede
  é **renderizada como qualquer id**, então a espessura é dado do backend — **conferir** que um anel de
  bedrock de 2–3 tiles não estoura o layout/iluminação do renderer.
- Rede-ouro: `tools/BalanceSim --golden`.

**Tarefas:**
- Engrossar o anel de borda: as células bloqueadas a **1–2 tiles** de distância do chão recebem a peça de
  parede/maciço (em vez de só a primeira fileira), de modo que o contorno tenha espessura. Reaproveitar o
  bedrock-fill pra o miolo; o anel novo é a transição chão→maciço.
- **Borda decorada (ridge):** estampar decor de rocha (`biome.Decor`/uma paleta de ridge) **na primeira
  fileira de parede** com falloff, pra a borda ganhar relevo (sombra/pedra) em vez de linha chapada —
  reusando o `PaintClusters`/regras de decor da LM-07, sem pôr obstáculo no chão jogável.
- Espessura do anel e densidade da borda em `GameConfig.cs`. Onde a família do bioma não tem canto real,
  fechar com a peça sólida (sem dente) — como a LM-07 já faz.
- Conferir no renderer que não surge void nem sobreposição estranha. Rebaselinar a rede-ouro.

**Aceite:**
- A borda das salas/corredores lê como **maciço espesso** com relevo, não como contorno de 1 tile.
- Sem void preto; sem decor de ridge invadindo tile jogável; sem dente de canto.
- Determinístico; espessura/densidade em `GameConfig.cs`; `--golden-check` verde 2× contra baseline nova.

**Verificação:** `dotnet build` + `npx ng build` limpos. `tools/BalanceSim --golden` (rebaseline) →
`--golden-check` verde 2×. Via preview MCP, screenshots antes/depois (mesma seed) — parede com espessura
e relevo, negativo lendo como rocha.

---

# H-05 — B3: Chão em Manchas (value-noise)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** H-04 · **Paraleliza com:** — (solo, Onda 5)

**Objetivo:** trocar o **ruído per-célula** do chão por **manchas coerentes**. Hoje cada célula sorteia
uma variante de ground independente (`DungeonGenerator.cs:259`, `rng.Pick(biome.Ground)`), o que vira
"chiado" uniforme. Agrupar as variantes em patches (value-noise seedado) dá um chão que lê como terreno,
como o do Tibia. Tarefa bounded e mecânica — regra fechada.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs:251-261` — pass 1 de ground:
  `floor.Ground[i] = rng.Pick(biome.Ground)` por célula (e `BossGround` na boss hall).
- `Engine/Rng.cs` — fonte determinística pro hash do value-noise (semear por `(x,y)` + seed da run).
- `Domain/Biomes.cs` — `biome.Ground` é o array de variantes a distribuir em manchas.
- Rede-ouro: `tools/BalanceSim --golden`.

**Tarefas:**
- Implementar um **value-noise determinístico** (hash de `(x,y)` + seed da run, interpolado numa grade de
  baixa frequência) e usar o valor pra **escolher a variante de ground por região** em vez de sorteio
  por célula — variantes adjacentes formam manchas de tamanho controlável.
- Frequência/escala do noise (tamanho da mancha) em `GameConfig.cs`. Boss hall (`BossGround`) segue
  destacada — pode usar o mesmo noise sobre o array dela.
- **Sem** dependência de `System.Random`/ruído de lib não-seedado — só aritmética sobre o `Rng`/seed.
  Rebaselinar a rede-ouro.

**Aceite:**
- O chão lê como **manchas** de variante, não ruído per-célula; tamanho de mancha controlável por config.
- Determinístico (mesma seed → mesmo chão); constante de escala em `GameConfig.cs`.
- `--golden-check` verde 2× contra baseline nova.

**Verificação:** `dotnet build` + `npx ng build` limpos. `tools/BalanceSim --golden` (rebaseline) →
`--golden-check` verde 2×. Via preview MCP, screenshot: chão agrupado em manchas coerentes vs. o chiado anterior.

---

# H-06 — G1: Archetype de Caverna Aberta  ⭐ destrava overlure

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` se mexer em algo de lib) · **Depende de:** H-02, H-03, H-04 · **Paraleliza com:** — (solo, Onda 6)

**Objetivo:** o paradigma rooms-and-corridors é **anti-lure** — cada sala é uma briga isolada. O overlure de
verdade pede um **campo único e aberto** com mobs **espalhados**, onde você faz a volta puxando um train e
junta a pilha. Adicionar um **archetype de caverna aberta** como alternativa de andar (escolhido no
`BuildFloors`), reusando o autômato celular (B1) pra gerar uma caverna orgânica grande em vez de salas+corredores.

**Decisão de representação (fechada):** a caverna se expõe como `List<Room>` de **zonas grandes** que
ladrilham a área aberta. Assim `GameWorld.SpawnFloorMonsters` (itera `Rooms`, `:390-435`) e o orçamento de
spawn por sala continuam funcionando **sem alteração** — os mobs nascem espalhados pelo campo, e H-06 **não
toca `GameWorld`** (fica disjunto de H-07).

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` — `Generate` (`:44`). Adicionar uma variante
  (ex. parâmetro `layout`/`isOpenCavern`, ou um método `GenerateOpenCavern`) que: roda CA numa área grande
  (reusa o passe de B1), garante uma componente conectada, posiciona `Entry`/`LadderDown`/POIs no campo, e
  preenche `Rooms` com zonas de spawn que cobrem a caverna.
- `backend/src/KaezanArenaFable.Api/Engine/GameMode.cs:55-59` — `DungeonModeStrategy.BuildFloors`: hoje
  andar 0 normal + andar 1 boss. Escolher o archetype por andar (ex. andar 0 = caverna aberta; boss segue
  como está) ou alternar por `Rng`. **Manter `IsBossFloor` intacto.**
- Choke/box (H-03): a caverna pode ter **alcovas de box** nas bordas (reusa o nicho) pra o jogador fechar
  box depois do overlure. Paredes-maciço (H-04) e chão em manchas (H-05) aplicam igual.
- Rede-ouro: `tools/BalanceSim --golden` — **estender** pra cobrir o novo archetype (senão ele fica sem
  rede de segurança). Ver `tools/BalanceSim/Golden.cs`.

**Tarefas:**
- Implementar o gerador de caverna aberta (CA em área grande + conectividade + POIs + zonas de spawn como
  `Rooms`), determinístico (só `Rng`).
- Ligar no `BuildFloors` a escolha do archetype por andar; o boss floor permanece como hoje.
- Garantir alcovas de box nas bordas da caverna (reuso de H-03) pra o ciclo lure→box funcionar no aberto.
- Estender a rede-ouro pra incluir o archetype; constantes (tamanho da caverna, densidade de mob no campo,
  nº de alcovas) em `GameConfig.cs`. Rebaselinar conscientemente.

**Aceite:**
- Existe um andar de **caverna aberta**: campo único orgânico, mobs espalhados, sem a malha de salinhas.
- Dá pra fazer a volta puxando um train grande (overlure) e fechar box numa alcova de borda.
- `GameWorld` **inalterado** (spawn por `Rooms`/zonas funciona); boss floor intacto.
- Determinístico; rede-ouro estendida e verde 2×; constantes em `GameConfig.cs`.

**Verificação:** `dotnet build` + `npx ng build` limpos. Rede-ouro estendida → `--golden-check` verde 2×.
Via preview MCP: rodar o andar de caverna, puxar um train de 10+ mobs pela volta, juntar a pilha e fechar
box numa alcova. Screenshot do overlure + comparação com o print do Tibia.

---

# H-07 — G4: Persistência de Aggro / Overlure (tuning)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** — (valida com H-06 branda) · **Paraleliza com:** H-02 (Onda 2)

> **[x] H-07 — feito.** Persistência de aggro afinada pro overlure em `GameConfig.cs`: `AggroDropRange 12→16`
> (cauda do train atrasa mais antes de o timer começar; coerente com salas H-01 e caverna H-06),
> `AggroDropOutOfRangeMs 4000→8000` e `AggroDropNoLosMs 6000→8000` (aguenta a volta + virar quina sem grude
> infinito). `MonsterAggroRange` mantido em 8 (pull ≠ persistência). Drop segue finito — fuga real solta a
> pilha. Determinístico (só comparações de `NowMs`, sem `Rng`); `dotnet build` limpo (0 warn). Sem rebaseline
> do golden (rede-ouro não cobre IA de mob); feel final dos números fica pra H-09 após a caverna (H-06).

**Objetivo:** garantir que um **train deliberado não evapora**. O lure já funciona (mobs agarram por
aggro+LoS e perseguem fora da sala, `GameWorld.cs:3284-3355`), mas os thresholds de drop podem soltar a
pilha cedo demais quando você faz a volta do overlure. Afinar a **persistência de aggro** (alcance/tempo de
drop, e como a LoS quebrada interage) pra sustentar um train, sem deixar o mob "grudado" infinito.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs:3284-3311` — aquisição (`MonsterAggroRange`+LoS) e
  drop de aggro: `AggroDropRange` (`:3295`), `AggroDropOutOfRangeMs` (`:3306`), `AggroDropNoLosMs` (`:3308`),
  `DropAggro` (`:3371`). `HasLineOfSight` (`:3274`).
- `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` — as constantes acima (procurar
  `MonsterAggroRange`/`AggroDrop*`).
- **Determinismo:** o tick já usa `NowMs` da run; manter só `Rng`/`NowMs`, sem relógio de parede.
- Rede-ouro **não cobre IA de mob** (só geração) — então aqui a verificação é playtest + determinismo de run,
  não rebaseline do golden.

**Tarefas:**
- Rever os thresholds pra um overlure deliberado segurar: ex. `AggroDropOutOfRangeMs` generoso o bastante
  pra a volta do train, `AggroDropRange` coerente com o tamanho das salas grandes (H-01) e da caverna (H-06),
  e o drop por LoS perdida não soltando a pilha toda numa curva. Documentar a intenção de cada número.
- Considerar um teto de train se necessário (pra não virar lag/spam), mas sem matar o overlure — decisão de design no prompt.
- **Todas** as constantes em `GameConfig.cs`. Sem hardcode no tick. Preservar determinismo (mesma seed+inputs → mesma run).

**Aceite:**
- Um train puxado pela volta **se mantém** (mobs não soltam cedo demais) num andar grande/caverna.
- Mobs ainda **soltam** quando o jogador realmente escapa (sem grude infinito).
- Determinístico (mesma seed/inputs → run idêntica). Constantes em `GameConfig.cs`.

**Verificação:** `dotnet build` limpo. Via preview MCP (idealmente após H-06): puxar 10+ mobs pela volta e
confirmar que a pilha segue até o box; depois escapar de verdade e confirmar o drop. Rodar a mesma
seed/inputs 2× e confirmar resultado idêntico (determinismo).

---

# H-08 — B4: Catálogo de Parede-Montanha (extractor)  ✅

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** H-01 (Onda 1)

> **[x] H-08** — família de parede-montanha `5630–5653` (corpos + cantos convexos reais + ridge)
> extraída e tagueada em `wall.mountain.{body,corner,ridge}`; 24 PNGs em disco + entradas no
> `manifest.json` (add-only, 2483→2507 objetos). Descoberta via novo modo `--dump-walls` do
> extractor. Mapa de ids → `Biomes.cs` e nota de re-extração em
> [`docs/KNOWLEDGE_wall_mountain_family.md`](../../KNOWLEDGE_wall_mountain_family.md).

**Objetivo:** o que mais aproxima do print do Tibia é a **parede-montanha com cantos reais**. Hoje as
famílias de parede degeneram (cave: `WallCorner==WallH==356`, sem canto). Enriquecer o catálogo de tiles do
extractor com uma **família de parede-montanha** (corpos + cantos + peças de profundidade) dá a B2/B4
material pra a borda deixar de ser linha fina. **Sem trocar de tileset** — só curar/extrair mais ids do Tibia.

**Contexto técnico / Arquivos prováveis:**
- `tools/AssetExtractor/content-config.json` — `objectIds` (pull direto) e grupos `semantic`
  (`ground.*`, `wall.cave/stone`, `decor.*`, `accent.*`, `poi.*`). Instruções de re-rodar no `tools/README.md`.
- `frontend/public/assets/tibia/manifest.json` — saída do extractor (cada id com PNG + `semantic`).
- `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs:67-73` — onde as peças de parede por bioma seriam
  trocadas/ampliadas pra a família nova (uma vez extraída). **Esta unidade só fornece os tiles**; trocar as
  peças no `BiomeDef` é refino de B2/seguimento — pode ficar como sugestão no fim do prompt.
- **Cruzamento com a LM-13** (`docs/roadmap/ongoing/roadmap_dungeons.md`): a LM-13 enriquece o catálogo de
  tiles em geral. **Não duplicar:** H-08 cobre só a **família de parede-montanha** (corpos/cantos/ridge);
  se a LM-13 já rodou, **adicione** a esta; se vai rodar, **sequencie** (mesmo `content-config.json`).

**Tarefas:**
- Identificar no Tibia os ids de object da **parede-montanha** (corpo N/S, corpo L/O, cantos côncavo/convexo,
  peças de ridge/sombra de borda) por estrato/bioma onde fizer sentido (cave dirt, stone, etc.).
- Para ids **já em disco** (dos 2483 objects): re-taguear nos grupos `semantic` (`wall.cave`/`wall.stone` +
  um grupo novo tipo `wall.mountain.*`) — sem re-extração.
- Para ids **ausentes**: adicioná-los a `objectIds`/`semantic` e **re-rodar o extractor** (conforme `tools/README.md`),
  gerando PNGs + atualizando o `manifest.json`. **Add-only** (não renomear grupos que biomas/picker referenciam).
- Documentar no fim quais ids dariam cantos reais a `Biomes.cs` (insumo pro refino de B2).

**Aceite:**
- Existe uma família de **parede-montanha** com corpos + cantos (côncavo/convexo) + ridge no catálogo `semantic`.
- Todo id citado tem PNG em disco e entry no `manifest.json` (sem tile quebrado).
- Grupos antigos preservados (add-only); nenhuma mudança de engine/gameplay (só assets + config do extractor).

**Verificação:** re-rodar o extractor sem erro; checar (script/`node`) que todo id novo de `m.semantic` tem
PNG em disco. `npx ng build` limpo. (Visual real chega quando B2/refino apontar `Biomes.cs` pra a família nova.)

---

# H-09 — Verificação & Balance de Hunt (golden + playtest lure/box)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** H-01…H-08 · **Paraleliza com:** — (solo, Onda 7 — precisa de tudo fechado)

**Objetivo:** fechar a trilha: confirmar que as hunts ficaram **mais bonitas** e **mais jogáveis** (overlure
+ box funcionam de ponta a ponta), sem regressão de build, determinismo ou balance. Exige julgamento de
feel/balance, por isso fica no modelo premium.

**Contexto técnico / Arquivos prováveis:**
- Rede-ouro consolidada: `tools/BalanceSim --golden` / `docs/balance/golden_dungeon.txt` (já estendida pra o
  archetype de caverna na H-06).
- Tuning final de aggro (`GameWorld.cs` + `GameConfig.cs`, da H-07) e densidades (H-01).
- Renderer pra screenshots (`frontend/src/app/core/renderer.ts`).

**Verificação mínima:**
- `dotnet build` + `npx ng build` limpos.
- `tools/BalanceSim --golden-check` **verde 2×** (determinismo do conjunto todo).
- Run tier 1 (rooms-and-corridors melhorado): salas grandes orgânicas, corredores largos (2–3), nicho de box
  com boca de 1 tile, paredes-maciço, chão em manchas.
- Run no andar de **caverna aberta**: puxar um **overlure** de 10+ mobs pela volta, juntar a pilha, **fechar
  box** numa alcova — e o train **se mantém** (H-07) e **solta** ao escapar.
- Conferir que nenhum andar nasce inalcançável/vazio e que o boss floor segue intacto.

**Aceite:**
- Builds verdes; golden verde 2× (determinístico).
- Visual: comparação antes/depois (mesma seed) mostra salas orgânicas + parede-maciço + chão em manchas;
  screenshot do andar de caverna lado a lado com o print do Tibia que motivou a trilha.
- Jogabilidade: ciclo overlure → fechar box demonstrado num andar de caverna (vídeo/sequência de screenshots).
- Sem regressão de determinismo, conectividade ou balance óbvio.

**Verificação:** os builds + golden acima; sessão de preview MCP com os screenshots/sequência do overlure+box;
1 linha de resumo marcando `[x] H-09` aqui.

---

## Depois (fora de escopo — não perder as ideias)

1. **Refino de `Biomes.cs` pra a família de parede-montanha (H-08, ✅ feita)** — apontar
   `WallH/V/Pole/Corner` (e um slot de ridge) pros ids novos (`5630–5653`, grupos
   `wall.mountain.{body,corner,ridge}`) por bioma, fechando o visual de parede do Tibia. Mapa
   pronto em `docs/KNOWLEDGE_wall_mountain_family.md`. Pequeno, segue B2.
2. **47-blob pleno de auto-tiling** — depende de arte própria de parede (todos os cantos); fica pro tileset próprio.
3. **Prefabs/"stamps" de sala data-driven** — salas autorais (armadilha, tesouro, mini-arena) injetadas no
   gerador, pra variedade além do procedural puro.
4. **Hunt circular** (anel de corredores com bolsões, da spec `docs/design/tibia_map_patterns.md`/LM-02) —
   um terceiro archetype além de rooms-and-corridors e caverna aberta.
5. **Fontes de luz / iluminação dinâmica** no renderer (tochas, lava) — visual, independente da estrutura.
6. **Pacing de respawn no campo aberto** (estilo spawntime do Tibia) — se a caverna virar área de farm contínuo.
