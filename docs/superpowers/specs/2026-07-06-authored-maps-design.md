# Mapas autorais (OTBM → prefabs) + fundação de mapping/lore — Design

**Data:** 2026-07-06 · **Status:** aprovado em brainstorming, aguardando plano de execução

## Contexto e motivação

A expedition é o único modo de gameplay: salas procedurais (rooms-and-corridors com erosão)
geradas pelo `DungeonGenerator`, 1 wave + 1 boss. Está divertido, mas todo mapa é "procedural
simples" — falta o sabor de mapas bem montados que é a essência do Tibia: dungeons de hunt
temáticas, salas de boss, cidades com NPCs e salas de tesouro de quest.

**Descoberta central da pesquisa:** o mundo completo do OTServBR está disponível localmente em
`C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\world\`:

- `otservbr.otbm` — todas as cidades, dungeons de hunt, boss rooms e áreas de quest de um mundo
  Tibia-like, construído à mão por mappers ao longo de anos.
- `otservbr-monster.xml` — **todos os spawns**: quais espécies, onde, densidade, raio. É a
  "seleção de hunts por nível" dos sites, em forma legível por máquina.
- `otservbr-npc.xml` + pasta `quest/` — colocação de NPCs e dados de quest (fatias futuras).

O formato OTBM já está documentado no baseline do usuário
(`C:\Kaezan\kaezan\mapping\baseline\canary\systems\map.md`): árvore binária de nós, tile areas,
atributos de item, teleports, spawns. A arquitetura do arena-fable está pronta para consumir
mapas autorais: o renderer do frontend é genérico (desenha qualquer ground/wall/decor id que o
backend emitir), `BiomeDef` já prova que "dado visual autoral" funciona, e o AssetExtractor já
extrai sprites por appearance id.

**Insight de arquitetura:** um único "importador de crops de OTBM" destrava as quatro ideias
(hunt rooms temáticas, boss rooms, cidades, salas de quest) — todas são crops diferentes do
mesmo arquivo de mundo com wiring de gameplay diferente por cima.

## Decisões de escopo (brainstorming)

| Pergunta | Decisão |
|---|---|
| Primeira fatia | Docs (4 domínios) + importer OTBM + hunt rooms temáticas na expedition |
| Integração com o procedural | **Prefabs dentro do floor procedural** — o gerador continua mandando no layout; rooms selecionadas são substituídas por prefabs autorais |
| Temática de monstros | **Prefab carrega spawn theme** — espécies vêm do monster.xml da área de origem (filtradas pelo `MonsterRegistry`); dificuldade continua no budget/wave por tier |
| Abrangência dos docs | **Os 4 domínios de uma vez** (hunts, boss rooms, cidades, quests); código só para hunts/boss nesta fatia |
| Pipeline | **A) Conversor offline em `tools/`** — prefabs JSON commitados; runtime nunca vê OTBM |

### Abordagens consideradas para o pipeline

- **A) Conversor offline em `tools/` (escolhida)** — padrão idêntico ao AssetExtractor/
  convert-monsters: conteúdo é dado commitado e revisável; zero custo em runtime; determinismo
  trivial; o mesmo pipeline aceita mapas autorais feitos no Remere's Map Editor no futuro.
- **B) Leitor OTBM em runtime no backend** — rejeitada: acopla o jogo a um arquivo de mundo de
  ~100 MB que não é artefato nosso, mapeamento de ids em runtime, nada revisável em PR.
- **C) Prefabs JSON à mão** — rejeitada: joga fora anos de mapping pronto; lento demais.

## Seção 1 — Fase de pesquisa/documentação (4 domínios)

Novos docs em `docs/mapping/` deste repo (com links para o baseline do repo kaezan), escritos
dissecando `otservbr.otbm` + XMLs com scripts de análise, usando TibiaWiki e os sites de hunt
como guia de exemplos canônicos:

1. **`hunt_anatomy.md`** — como uma hunt é montada: zona de spawn (densidade, raio, mix de
   espécies), layout da dungeon (corredores vs salas, chokepoints), faixa de nível; inclui uma
   **tabela curada de ~15–20 hunts candidatas a prefab** (nome, coordenadas no OTBM, tema, tier
   equivalente, espécies já presentes no `MonsterRegistry`).
2. **`boss_room_anatomy.md`** — padrões de arena de boss: formato, gating (alavanca/teleport),
   tiles de entrada/saída, decoração ritual.
3. **`city_anatomy.md`** — estrutura de cidade (templo, depot, lojas, guildhalls), colocação de
   NPCs, ganchos de lore. Base para a fatia futura "cidade hub" — sem código agora.
4. **`quest_treasure_anatomy.md`** — padrões de sala de tesouro/quest: baús com
   actionid/uniqueid, storages, salas seladas. Base para a fatia futura de quests — sem código.

Somente os itens 1–2 alimentam código nesta fatia.

## Seção 2 — Conversor offline + formato de prefab

**`tools/map-importer/`** (Node, no padrão do `convert-monsters`), com parsing baseado na lib
[OTBM2JSON](https://github.com/Inconcessus/OTBM2JSON).

- **Input:** `prefabs-config.json` — lista curada de crops:
  `{ id: "prefab:rotworm-cave", x, y, z, w, h, role: "mob|boss|treasure", tier, theme }`.
  Coordenadas vêm da tabela do `hunt_anatomy.md`.
- **Processo:**
  1. Lê o crop do `otservbr.otbm`.
  2. Converte **server item id → appearance id** (dados do Canary que o AssetExtractor já usa).
  3. Classifica cada item em ground/wall/decor pelas flags do items.otb (blocking etc.).
  4. Lê o `otservbr-monster.xml` da região e extrai o **spawn theme** (espécies filtradas pelo
     `MonsterRegistry`).
  5. Detecta POIs: baús, bocas de entrada naturais.
- **Output:** um JSON por prefab commitado em `Content/prefabs/` no backend: grids de
  ground/wall/decor (appearance ids, mesmo modelo conceitual do `DungeonFloor`), máscara
  `blocked`, bocas de entrada, POIs, spawn theme.
- **Relatório de gaps:** sprites ausentes do `content-config.json` do extractor e espécies
  ausentes do `MonsterRegistry` saem num relatório para curadoria manual. O conversor **falha
  ruidosamente** se o prefab final referenciar sprite não extraído, crop fora do mapa ou id
  desconhecido.

Invariante preservado: conteúdo é dado commitado e revisável; runtime nunca vê OTBM.

## Seção 3 — Integração com o engine (prefab stamping)

- **Carga:** `PrefabRegistry` novo (em `Content/`, ao lado do `ContentStore`) carrega os JSONs no startup com
  ordem estável (sort por id). Prefab inválido = **falha no startup** (fail fast).
- **Stamping no `DungeonGenerator`:** depois do posicionamento de rooms e antes do
  `ConnectRooms`, o gerador decide via `Rng` da run se substitui rooms por prefabs (pool
  filtrado por tier/tema do bioma; o boss hall pode ser um prefab `role: boss`). O grid do
  prefab é estampado nos arrays `Ground/Wall/Decor/Blocked` do floor; a `Room` ganha o `Role`
  do prefab. Corredores conectam pelas **bocas de entrada** declaradas (em vez de
  centro-a-centro). `DungeonValidator` continua validando reachability.
- **Spawn theme:** sala prefab spawna usando a lista de espécies do prefab, mantendo o
  budget/wave por tier atual — composição temática, dificuldade inalterada.
- **Constantes** (frequência de prefab por floor etc.) em `GameConfig.cs`.
- **Determinismo/replay:** prefabs são dados estáticos + seleção via `Rng` = replay bit-perfect
  preservado. Mexer no gerador **exige rebaseline do golden do `--replay-check` (FF-01)** —
  passo explícito do plano.

## Seção 4 — Assets e frontend

- Sprites novos: adicionar os appearance ids reportados pelo conversor ao `content-config.json`
  e re-rodar o AssetExtractor (workflow existente).
- **Frontend: zero mudança estrutural** — o renderer é genérico. Risco conhecido: objetos
  64px/multi-tile inéditos; verificar com os primeiros prefabs no preview.

## Seção 5 — Erros e testes

- **Conversor:** falha ruidosa (id desconhecido, crop fora do mapa, sprite não extraído);
  relatório de gaps é output normal.
- **Testes de engine** (padrão `DungeonGeneratorTests`/`DungeonValidatorTests`):
  - stamping preserva reachability;
  - mesma seed → mesmo layout com prefabs;
  - prefab de boss substitui o boss hall corretamente;
  - spawn theme só produz espécies válidas do `MonsterRegistry`.
- **Verificação final:** `dotnet build` + `npx ng build` limpos; replay golden rebaselinado;
  run visual no preview com prefab aparecendo.

## Entregáveis da fatia

1. 4 docs de anatomia em `docs/mapping/` (hunts, boss rooms, cidades, quests).
2. `tools/map-importer/` funcional + `prefabs-config.json` curado.
3. **~6–10 prefabs** commitados (mob/treasure/boss, 2–3 temas) com sprites extraídos.
4. Stamping determinístico na expedition + spawn themes + testes + golden rebaselinado.

## Fora de escopo (fatias futuras, já especificadas nos docs)

- Cidade hub com NPCs e lore (usa `city_anatomy.md` + `otservbr-npc.xml`).
- Quests e salas de tesouro gatilhadas por progressão (usa `quest_treasure_anatomy.md`).
- Floors 100% autorais / pool de mapas por tier.
- Autoria própria no Remere's Map Editor (o pipeline já vai aceitar, mas não é entregável).

## Referências

- Baseline local: `C:\Kaezan\kaezan\mapping\baseline\canary\systems\map.md` (formato OTBM),
  `instances.md`, `quests.md`, `events.md`.
- Mundo fonte: `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\world\`.
- Parsers OTBM: [OTBM2JSON](https://github.com/Inconcessus/OTBM2JSON),
  [ot-otbm](https://github.com/V0RT4C/ot-otbm),
  [tibia-go-otbm-parser](https://pkg.go.dev/github.com/levantocode/tibia-go-otbm-parser).
- Curadoria de hunts: [TibiaWiki Hunting Places](https://tibia.fandom.com/wiki/Hunting_Places),
  [TibiaBuddy Hunt Finder](https://www.tibiabuddy.com/tools/hunt-finder),
  [intibia](https://intibia.com/hunts), [tibiaroute](https://tibiaroute.com/hunting-places),
  [TibiaWiki BR — Locais de Caça](https://www.tibiawiki.com.br/wiki/Locais_de_Ca%C3%A7a).
- Navegação visual do mundo: [tibiamaps/tibia-map-data](https://github.com/tibiamaps/tibia-map-data).
- Editor para autoria futura: Remere's Map Editor (fork opentibiabr).
