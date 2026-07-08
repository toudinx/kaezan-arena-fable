# Map Layout — Natural Arena Shape (Design)

**Data:** 2026-07-07 · **Autor:** Opus 4.8 (brainstorming) · **Status:** direcionamento aprovado; spec em revisão

## Contexto

A reescrita de composição (`2026-07-07-map-composition-rewrite-*`) está entregue e **correta** —
provado por dado real do backend (maciço = corpo da família sobre bedrock opaco, `1116` eliminado;
lava bordeada; costura auto-bordeada). Mas as screenshots do Map Lab (2026-07-07) mostram que a
composição **renderiza fielmente formas ruins**: o desconforto restante é de **LAYOUT**, não de
composição. Feedback do usuário, confirmado no código:

1. **Tamanho de arena aleatório** ("umas dá pra andar bem, outras não"). Um preview T1 tinha 200/400
   células abertas (50%); outro seed pode abrir 30% ou 70%.
2. **Formas não-naturais** ("quadrado com uma mordidinha"; "só deixa uma parte minúscula sem ser reta").
3. **Pedras cortadas** (lascas de parede de 1 célula invadindo a sala; beiras de maciço em escada).

## Causa raiz (verificada em `Engine/DungeonGenerator.cs`)

Os floors são **arena única** (`GameConfig.RoomsFloor1/2 = 1`): uma arena de ~20×20 (margem 3) preenche
o floor e é esculpida por `ErodeArena` → `SeedArenaRock` (`DungeonGenerator.cs:541–581`) + `ApplyRockToFloor`
(`:589+`). `SeedArenaRock` monta a massa aberta como união de **2–4 lobos elípticos** (banda 0.30–0.70)
com raios em fração variável, mais um **ruído de borda** (`ArenaEdgeNoiseProb`) que o CA (regra 4-5)
transforma numa costa. Um core central é forçado aberto; um flood-fill mantém só o componente conexo.

- **Variância de área:** nº de lobos (`ArenaLobesMin/Max`) × raios (`ArenaLobeRadiusMinFrac/MaxFrac`)
  sem **piso de área caminhável** → a fração aberta varia muito por seed.
- **Costa serrilhada:** `ArenaEdgeNoiseProb` + CA deixam degraus de 1 célula e nubs → "pedras cortadas"
  e beiras em escada (a composição as renderiza fielmente).
- **Sem limpeza morfológica:** nada remove protrusão/enseada de 1 célula depois do carve.

`ErodeRoom` (`:462`) é o carver do caminho **multi-sala**, hoje **dormante** (`RoomsFloorN = 1`) — fora
de escopo. `CarveAmphitheater` (boss) compartilha `ApplyRockToFloor` — entra no escopo pela cauda comum.

## Meta (decidida com o usuário)

**Tamanho consistente, forma variada.** Toda arena abre uma fração estável e caminhável (**alvo ~70%
do interior**, faixa aceitável ~65–78%), mas a costa/baías variam por seed. Referência de forma:
`audit-shots/ref-troll-cave.png` (caverna arredondada e orgânica, com baías — não retângulo, não blob
serrilhado).

## Arquitetura — 3 alavancas

### 1. Área caminhável consistente (piso garantido)
Recalibrar os lobos para uma massa maior e menos variável (menos lobos, maiores, mais sobrepostos), e
adicionar um **piso de área**: após o carve+flood-fill, se a fração aberta ficar abaixo do alvo, **dilatar**
a massa aberta (crescimento morfológico determinístico a partir da borda do componente conexo) até
bater o piso. Assim nenhum seed produz arena "apertada". Tudo em `GameConfig`.

### 2. Forma orgânica arredondada
Baixar `ArenaEdgeNoiseProb` e recalibrar `ArenaLobeRadius*`/`ArenaLobesMin/Max` para lobos que se
unem numa forma arredondada com baías, não num quadrado mal arranhado nem numa costa serrilhada. Manter
a união-de-lobos + CA (a abordagem é correta), só os parâmetros mudam. Nenhum passe novo aqui.

### 3. Limpeza morfológica — o matador de "pedra cortada"
Um passe **determinístico, rng-free** sobre `DungeonFloor.Blocked`, rodando **depois de `ErodeArena`
e antes de `CarveSidePockets`/`PlacePillars`** (para não apagar pockets/pilares intencionais):

- **Open-close (1 célula):** célula de parede com ≥3 vizinhos-4 abertos vira aberta (remove protrusão/
  lasca); célula aberta com ≥3 vizinhos-4 bloqueados vira parede (enche enseada). Double-buffered.
- **De-stair diagonal:** onde a borda do maciço faz degrau de 1-em-1 (padrão de canto onde uma parede
  toca aberto só na diagonal), preencher/arredondar o canto para a beira não ler como escada.
- **Re-flood de conectividade:** após a limpeza, re-flood-fill a partir do centro forçado-aberto e
  bloquear o que ficou desconexo — nav nunca perde alcance.

Sequência final em `Generate` (arena única, não-boss): `ErodeArena` → **`SmoothArenaShape`** →
`CarveSidePockets` → `PlacePillars`. O boss (`CarveAmphitheater`) recebe o mesmo `SmoothArenaShape`.

## Determinismo (invariante)
Lobos e piso usam o `Rng` da run em ordem de varredura fixa (como hoje). Os passes CA, dilatação, open-
close, de-stair e re-flood são **rng-free e double-buffered** (independentes de ordem de scan). Proibido
`Random`, `DateTime.Now`, `Guid.NewGuid()`, iteração instável.

## Verificação
Testes xunit (TDD) sobre floors gerados em muitos seeds:
- **`ArenaOpenFractionIsConsistent`** — para N seeds × 5 tiers, a fração aberta do interior fica em
  [0.65, 0.78]; nenhum seed abaixo do piso.
- **`NoSingleCellWallProtrusions`** — nenhuma célula de parede com ≥3 vizinhos-4 abertos (pós-limpeza).
- **`NoSingleCellOpenInlets`** — nenhuma célula aberta com ≥3 vizinhos-4 bloqueados.
- **`ArenaIsFullyConnected`** — todas as células abertas alcançáveis do centro (re-flood não deixou ilha).
- Guardar `PlacePillars`/`CarveSidePockets` não quebrados (pilares/pockets ainda presentes).

Aceite visual (Map Lab, 5 tiers × seeds {101,202,303}, zoom 2×, screenshots no doc): arenas com
tamanho parecido e caminhável · forma arredondada orgânica (não quadrado+mordida) · zero lasca de
parede de 1 célula · beiras de maciço sem escada · comparar com `ref-troll-cave.png`.

## Golden / replay
Layout muda a geração → **um rebaseline deliberado** ao final. Este rebaseline **inclui/supera** o
rebaseline pendente da fatia de composição (a bateria de replays está `git`-deletada no working tree;
regravar do zero aqui). `--golden-check` e `--replay-check` = 0 divergências após.

## Fora de escopo (próximos sub-projetos)
- **Editar tile-a-tile no Map Lab + enxugar as opções** (o outro pedido do usuário) — sub-projeto de
  tooling à parte.
- `ErodeRoom`/caminho multi-sala (dormante).
- Composição de tile (entregue).
- Subsistema B (import RME/OTBM).

## Riscos conhecidos
- **Dilatação vs conectividade:** dilatar pode fundir com a borda do floor; limitar ao interior
  (respeitar a margem) e sempre re-flood.
- **Piso alto demais engole a forma:** se o alvo de 70% apagar as baías, baixar o alvo antes de subir
  a dilatação; a forma (baías) tem prioridade sobre encher.
- **Ordem com pilares/pockets:** a limpeza morfológica DEVE rodar antes de `PlacePillars`/`CarveSidePockets`.
- **Rebaseline:** único, ao final; superpõe o da composição.
