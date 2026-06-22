# Estudo de Design — Padrões de Mapa do Tibia → Engine do Kaezan

> **LM-02 do `docs/roadmap_dungeons.md`.** Estudo **acionável** de como o Tibia constrói lugares,
> traduzido para os conceitos do nosso engine (`Engine/DungeonGenerator.cs`, `Domain/GameConfig.cs`,
> `Domain/Biomes.cs`). **Não é tooling** — é leitura + síntese de design. Nenhuma mudança de código.
>
> **Consumido por:** a seção **§4 (Layout de arena)** é a spec direta da **LM-04**. As seções de
> **cidade**, **hunt circular** e **sala de boss** alimentam o boss-rush e a cidade do `## Depois`.
>
> **Fontes lidas (sem parser de OTBM):**
> - `C:\Kaezan\kaezan\mapping\baseline\canary\systems\map.md` — hierarquia OTBM, spawns, teleports,
>   instâncias, persistência de tile.
> - `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\world\otservbr-monster.xml` — 85k+ blocos de
>   spawn; medimos a distribuição real de `radius`/`spawntime`/clustering (números abaixo).
> - `…\world\quest\` (Soul War, Dream Courts, Cults of Tibia) como exemplos de sala de boss/arena.
> - Nosso lado: `DungeonGenerator.Generate` (rooms/corredores/POIs/`Role`), `GameConfig` (budgets,
>   tamanhos), `Biomes.ForTier`.

---

## 0. Vocabulário: o Tibia em termos do nosso engine

| Conceito Tibia | O que é | Equivalente no Kaezan |
|---|---|---|
| `OTBM_TILE` (ground item u16) | célula de chão autorada | `DungeonFloor.Ground[i]` (ushort por célula) |
| parede / item bloqueante | tile intransponível | `DungeonFloor.Blocked[i]` + `Wall[i]` (sprite via `ClassifyWall`) |
| `<monster center… radius=R>` | **ponto de spawn em zona** (raio R) com respawn por timer | nosso **spawn por sala** (orçamento, sem respawn — instanciado uma vez) |
| `spawntime` (s) | atraso de respawn | **não temos respawn na dungeon**; vira **intervalo de wave** na arena |
| `Teleport` (`ATTR_TELE_DEST`) | portal para outra coordenada | `LadderDown` (descer andar) — mesma ideia de "saída direcionada" |
| `OTBM_TOWN` / waypoint | âncora de cidade/NPC | POI de sala (`Role`), `Entry` |
| área instanciada (coord. reservada) | "sala fake" em bloco de coordenadas | nosso modelo **já é instanciado por natureza**: cada run gera seu próprio `DungeonFloor` em memória |

**Conclusão de framing:** o Tibia é um **mapa global único autorado à mão**, com spawns por *zona de
raio* e respawn por *timer*. Nós somos **procedurais e instanciados por run** (cada `DungeonFloor` é
privado da run). Logo, não copiamos pixels nem o sistema de respawn — copiamos **a geometria e a
densidade**: como ele dimensiona ruas, anéis de hunt, salas de boss e bolsões de spawn. Esses números
são o que extraímos abaixo.

### Medições reais do `otservbr-monster.xml` (a base empírica das specs)

- **Distribuição de `radius`** (≈85k blocos): `1` → 21.770 · `3` → 21.416 · `2` → 5.393 · `5` → 1.407 ·
  `4` → 1.406 · `6` → 185 · resto (7–30) raríssimo.
  → **Leitura:** o Tibia spawna em **bolsões pequenos**. `radius=1` = criatura única/forte num ponto;
  `radius=3` = bolsão de hunt típico; `radius=4–5` = bolsão "grande" (até ~4 criaturas). Raios ≥6 são
  exceção (campos abertos). **Nossa sala de 5–9 tiles já é, em escala, um bolsão radius 2–4.**
- **Distribuição de `spawntime`**: `90s` → 58.532 · `60s` → 24.219 · `30s` → 173 · valores enormes
  (1445, 14402, 28815…) para bosses/raros.
  → **Leitura:** a cadência base de "reaparecer pressão" é **60–90s**; bosses têm respawn de horas. Na
  arena, o **90s** vira referência de *teto* do intervalo de graça entre waves (nosso é bem mais curto —
  ritmo de arena, não de MMO).
- **Clustering multi-espécie:** um único bloco `radius=4` mistura `Ice Golem ×2 + Wyvern + Crystal
  Spider`. → **Leitura:** bolsões **não são monoespécie**; misturam um "corpo" (tank/melee) + 1–2
  "temperos" (ranged/elemental). Mapeia direto no nosso `CommonMobs`/`EliteMobs` por tier + arquétipos
  de comportamento (`MonsterBehaviorProfiles`: bruiser, ranger, artillery, swarm…).

---

## 1. Estrutura de cidade

**(a) O que o Tibia faz.** Cidades (Thais, Carlin, Edron) são **malhas ortogonais** de quarteirões:
ruas largas (2–4 tiles) ligando uma **praça central** com o **templo** (ponto de respawn/cura), o
**depot** (banco/armazenamento) e NPCs de serviço (loja, banco) agrupados ali. Blocos de **casa** ficam
nas bordas, com **portas** (`ATTR_HOUSEDOORID`) e camas. O templo é uma âncora `OTBM_TOWN`; NPCs vêm de
`*-npc.xml`. Tudo é **autorado**, sem combate dentro da zona segura (flag `PZ` — protection zone).

**(b) Por que funciona.** Legibilidade: a praça é o hub mental ("volto pro templo"). Ruas largas evitam
travamento de pathfinding com multidão. Serviços agrupados = um lugar resolve tudo. As casas na borda
dão **lugar pessoal** sem atravessar o miolo público. É hub-and-spoke clássico.

**(c) Spec traduzida (para a "Cidade + 1 house" do `## Depois`).** Cidade é **autorada**, não procedural
— mas reusa nossas estruturas:

| Parâmetro | Valor sugerido | Mapeia em |
|---|---|---|
| Tamanho do mapa | 48–64 tiles (maior que dungeon: `Floor1Size=40`) | novo tamanho de mapa autorado |
| Praça central | sala aberta 10×10 a 14×14 | `Room { Role="entry" }` ampliada |
| Largura de "rua" | **3 tiles** (corredor de dungeon é 2; cidade respira mais) | `CarveCorridor` com largura paramétrica |
| POIs de serviço | 3–5 âncoras (loja, banco, gacha, dailies) na praça | novo `Role="npc"` (lista de POIs como `Chests`) |
| Bloco de casa | 1 sala 7×9 reservada, com porta e tiles de mobília | `Role="house"`; persistência em `Meta/Persistence` |
| Combate | **nenhum** (zona segura) | modo `City` não roda `SpawnFloorMonsters` |

**(d) Fora de escopo / risco.** Persistência de mobília da house (o `tile_store` do Tibia só salva tiles
de casa — espelhar com tabela própria `Meta/Persistence`, **não** no snapshot da run). Pathfinding de
NPC de lore: manter NPCs **parados ou em patrulha fixa** (determinismo). Sem PvP, sem depot real.

---

## 2. Hunt circular (anel de spawn)

**(a) O que o Tibia faz.** Hunts clássicas (Mintwallin, Hero Cave, demon hunts) são **anéis**: um
corredor que dá a volta com **bolsões de spawn** distribuídos ao longo do anel. O jogador "faz a volta",
limpando bolsão por bolsão, e quando completa o círculo o primeiro bolsão já respawnou (`spawntime`
60–90s). Bolsões têm `radius=3–5` e misturam 2–4 criaturas (ver §0). O centro do anel costuma ser
parede/rocha (não atravessável) — força a rota circular.

**(b) Por que funciona.** Fluxo contínuo sem becos: você nunca fica preso, sempre há "o próximo
bolsão". O respawn temporizado cria um **loop sustentável** (farmar indefinidamente). A densidade por
bolsão é calibrada pro DPS de uma vocação fazer a volta sem morrer nem entediar.

**(c) Spec traduzida.** Hoje geramos **árvore de salas** (`ConnectRooms` = Prim + 1 aresta de loop),
não anel. O anel é uma **variante de topologia** futura, mas a **densidade de bolsão** já se aplica:

| Parâmetro | Valor (tier 1 → 5) | Mapeia em |
|---|---|---|
| Geometria do anel | raio 8–12 tiles, largura **3** | futuro gerador "ring" (corredor circular) |
| Nº de bolsões no anel | 5–7 | nº de salas `Role="mob"` no anel |
| Densidade por bolsão | **orçamento de spawn por sala** já existente | `SpawnBudgetBase=14 × (1+(tier-1)·0.55)` → ~14 (T1) a ~45 (T5) |
| Custo por criatura | comum **2**, elite **5**, swarm **1** | `SpawnCostFor` / `MonsterBehaviorProfile.SpawnCost` |
| Criaturas por bolsão | budget/custo ≈ **7 comuns** (T1) a **9–10 + elites** (T5) | já é o comportamento de `SpawnFloorMonsters` |
| Mistura | 1 "corpo" + 1–2 "temperos" | `CommonMobs` (bruiser/skirmisher) + 1 `ranger`/`artillery` |
| "Respawn" do anel | **não temos** (instanciado) — a arena reintroduz pressão via waves | ver §4 |

> **Ponte com o existente:** a aresta de loop que o `ConnectRooms` já carva (`n>3 →
> CarveCorridor(0, rng.Range(2,n-1))`) é exatamente o embrião do anel — garante que a topologia não é
> uma árvore pura, então "fazer a volta" já é possível em mapas grandes.

**(d) Fora de escopo / risco.** Respawn dentro de um andar de dungeon **não vai existir** (quebra o
contrato "limpou, acabou" e o determinismo do golden de LM-01). O loop de farm sustentável é função do
**modo arena** (§4), não da dungeon. Gerar um anel geometricamente limpo (sem becos no miolo) exige um
gerador dedicado — fica para o `## Depois` (item 3, dungeon mais rica).

---

## 3. Sala de boss

**(a) O que o Tibia faz.** Salas de boss de quest (Soul War, Dream Courts, Cults of Tibia) são
**arenas fechadas** atrás de um **choke**: um corredor/porta estreito (1–2 tiles) que abre numa **sala
grande e regular** (retângulo ou círculo, ~11×11 a 15×15). O boss fica no centro; o espaço permite
**kite** (correr em volta). Frequentemente há **escolta** (adds) e **cobertura** (pilares/colunas que
bloqueiam linha de visão de ataques ranged). A entrada às vezes **sela** (teleport/parede) ao começar —
sem retirada.

**(b) Por que funciona.** O choke separa "trash" de "boss" (beat dramático: você *entra* na arena). O
tamanho dá espaço de kite — luta de mobilidade, não de encurralamento. Pilares dão decisão tática
(quebrar LOS vs. ficar no DPS). A escolta impede tunelar 100% no boss.

**(c) Spec traduzida.** Já temos uma "boss hall": no andar de boss, a última sala é forçada a **11×9**
(`isBossFloor && Rooms.Count == roomCount-1`), com `BossGround` próprio e `Role="boss"`. Specs:

| Parâmetro | Valor atual / sugerido | Mapeia em |
|---|---|---|
| Forma da sala de boss | **11×9** retangular (existente) → alvo 13×11 p/ kite | linha `w=11; h=9` em `DungeonGenerator.Generate` |
| Choke de entrada | corredor de **2 tiles** (largura padrão) | `CarveCorridor` (já estreito vs. sala) |
| Chão distinto | `biome.BossGround` (material próprio) | `PaintTiles` (`bossRoom ? BossGround : Ground`) |
| Escolta | comuns que escoltam | `MiniBossEscort=2` (boss-rush pode subir p/ 3–4) |
| HP do clímax | boss = `MonsterStatLines["{tier}:boss"]`; miniboss = elite × `2.4` | `MiniBossHpScale` |
| Postura / Echo Break | poço de postura por tier, janelas de stagger | bloco `Posture*` do `GameConfig` (F-E) |
| Cobertura (pilares) | **2–4 tiles bloqueados** internos como colunas | novo: marcar `Blocked` em padrão fixo dentro da boss room |

**Spec de boss-rush (`## Depois` item 1):** sequência de **3–5 salas de boss** ligadas por chokes, sem
salas de trash entre elas. Reusa `Role="boss"`/`"miniboss"`, escala HP por posição na sequência, pluga
na costura de modo da LM-03 como `BossRush`. Pilares opcionais por sala (LOS).

**(d) Fora de escopo / risco.** Selar a entrada (teleport-lock) é gameplay de hazard/portal — adia.
Pilares exigem que o pathfinding de mob (já existente, A*-like no plano XY) lide com obstáculos internos
**sem** travar — testar densidade baixa (≤4 colunas) primeiro. Não introduzir respawn de escolta.

---

## 4. Layout de arena ⭐ (spec direta da LM-04)

**(a) O que o Tibia faz.** As "arenas" mais próximas do nosso modo são as **salas de horda** de quest
(ondas de adds num espaço fechado) e os bolsões densos de hunt. O padrão é: **uma sala retangular
fechada e regular**, com o jogador no centro e **spawns vindo das bordas** (pontos ao redor do
perímetro), nunca em cima do jogador. A pressão escala porque mais pontos de borda ativam por vez. Sem
cobertura no miolo — é um **espaço de kite puro** (correr em círculos/oito).

**(b) Por que funciona.** Sala fechada regular = leitura instantânea do espaço de recuo (você sabe onde
dá pra correr). Spawn pela borda telegrafa a ameaça (vem de fora pra dentro) e dá ao jogador a janela de
reposicionar. Kite no centro com adds vindo das bordas é o loop de "survival" comprovado (twin-stick /
horde mode).

**(c) Spec traduzida — números que a LM-04 deve pôr em `Domain/GameConfig.cs`:**

```
// ---- arena (LM-04) ----
ArenaWidth          = 21     // sala retangular aberta (ímpar → centro exato)
ArenaHeight         = 15     // ~21×15 = 315 tiles; ~280 andáveis após borda
ArenaBorderThick    = 1      // parede de borda (Blocked + Wall via ClassifyWall)
// Entry: centro da sala = (ArenaWidth/2, ArenaHeight/2) = (10, 7)

ArenaSpawnPoints       = 8       // pontos determinísticos na faixa de borda (anel a 1 tile da parede)
ArenaSpawnMinDistFromPlayer = 5  // nunca spawna a menos de 5 tiles do player (telegrafa + janela de recuo)

ArenaWaveBudgetBase    = 10            // orçamento da wave 1 (comum=2 → ~5 mobs)
ArenaWaveBudgetGrowth  = 0.35          // budget(w) = Base × (1 + Growth·(w-1)) × tierMult
ArenaWaveTierMult      = reusa (1 + (tier-1)·SpawnBudgetTierGrowth)   // 0.55, já existe
ArenaWaveGraceMs       = 2500          // intervalo de graça após limpar a wave antes da próxima
ArenaEliteStartWave    = 3             // elites (custo 5) entram a partir da wave 3
ArenaMiniBossEveryWaves= 5             // a cada 5 waves, 1 mini-boss (elite × MiniBossHpScale) + escolta
ArenaMaxConcurrentMobs = 24            // teto de vivos simultâneos (perf + legibilidade do tick)
```

**Geometria (build do mapa na costura "fonte-de-mapa" do modo `Arena`):**
- Sala retangular `ArenaWidth × ArenaHeight`, **toda andável** exceto a **borda de 1 tile** (`Blocked=true`).
- `Entry` = centro exato `(W/2, H/2)`. Sem `LadderDown`, sem `Chests`, sem `Sanctuaries`.
- `Ground` = `Biomes.ForTier(tier).Ground` (reaproveita bioma do tier — zero arte nova).
- `Wall` pela borda via `ClassifyWall` (já lida com cantos sem "dentes").

**Pontos de spawn (determinístico — só `Rng` da run + contagem de tick):**
- Pré-computar `ArenaSpawnPoints` posições no **anel interno** (1 tile dentro da parede), distribuídas
  ao longo do perímetro (ex.: passo uniforme pelo retângulo borda-1).
- A cada criatura de uma wave: escolher um ponto via `_rng` entre os que distam ≥
  `ArenaSpawnMinDistFromPlayer` do player **no tick de spawn**. Empate/ordem estável por índice do ponto.
- **Nunca** usar `DateTime.Now`/`Random`. O agendador de wave é estado por-run (wave atual, budget
  restante, próximo tick de spawn) avançado pelo contador de ticks.

**Ciclo de wave:**
1. Wave `w` começa → calcula `budget(w)`. Enquanto `budget>0` e `vivos<ArenaMaxConcurrentMobs`: spawna
   `CommonMobs`/`EliteMobs` (reusa `SpawnMonster` e `SpawnCostFor`), debita custo, em ponto de borda.
2. Elites (custo 5) só a partir de `ArenaEliteStartWave`. A cada `ArenaMiniBossEveryWaves`, 1 mini-boss
   (HP elite × `MiniBossHpScale`) + `MiniBossEscort` comuns.
3. Wave "limpa" quando **todos os mobs daquela wave morreram** → espera `ArenaWaveGraceMs` → `w+1`.

**Fim "sobreviver":**
- Morte do player encerra a run (`Ended`, vitória=`false`). Rastrear `WavesCleared` e `ElapsedMs`.
- **Score** derivado: `WavesCleared` peso alto + bônus de tempo/kills. Score → reward via
  `RewardService` (gold/XP/kaeros), mapeado pela LM-04.

**(d) Fora de escopo / risco.** **Sem cobertura/pilares na arena** (mantém kite puro e o pathfinding
trivial — diferente da sala de boss §3). **Sem respawn por timer** estilo Tibia — a pressão vem do
**escalonamento de wave**, não de respawn de zona. Cuidar do **determinismo do spawn** (é o ponto que o
LM-01-style estável vai cobrar): mesma seed + mesmos inputs → mesma sequência de waves/pontos. O teto
`ArenaMaxConcurrentMobs` protege o tick de 100ms de saturar. Todos os números acima são **ponto de
partida** — a LM-04 afina e mantém **tudo** em `GameConfig.cs`.

---

## 5. Padrão → Onde aplica no Kaezan (modo / prompt)

| Padrão Tibia | O que copiamos | Onde aplica (modo) | Prompt | Status |
|---|---|---|---|---|
| **Bolsão de spawn** (`radius=3–5`, multi-espécie, 60–90s) | densidade por bolsão + mistura corpo/tempero | Dungeon (`SpawnFloorMonsters`) e Arena (waves) | LM-04 | densidade já existe; waves na LM-04 |
| **Hunt circular** (anel + bolsões + "fazer a volta") | geometria de anel; loop de pressão | Dungeon (variante de topologia) / Arena (loop de wave) | `## Depois` (3) + LM-04 | aresta de loop já no `ConnectRooms` |
| **Sala de boss** (choke → arena grande + escolta + cobertura) | forma 11×9→13×11, choke 2-tiles, BossGround, pilares | Dungeon (boss floor, existe) + **Boss-rush** | `## Depois` (1) | boss hall existe; pilares/rush a fazer |
| **Arena de horda** (sala fechada + spawn de borda + kite) | sala 21×15, 8 pontos de borda, escala de wave | **Arena de sobrevivência** | **LM-04 / LM-05** | spec desta §4 |
| **Cidade** (praça + templo/depot + casas na borda, zona segura) | malha + praça hub + rua-3 + house | **Cidade + 1 house** | `## Depois` (2) | nenhum código ainda |
| **Teleport direcionado** (`ATTR_TELE_DEST`) | "saída direcionada" | já temos como `LadderDown` (descer andar) | — | existe |
| **Instância por coordenada reservada** | não precisamos — já somos instanciados por run | todos os modos | LM-03 (costura) | nativo do nosso design |

---

### Notas de fidelidade ao engine (invariantes respeitadas por este estudo)

- **Backend autoritativo / determinismo:** toda spec de spawn aqui assume **`Rng` da run + contagem de
  tick** — nada de `Random`/`DateTime.Now`. O respawn por timer do Tibia é **deliberadamente não
  copiado** (quebraria o golden de LM-01); a pressão recorrente vira **wave** na arena.
- **Constantes em `GameConfig.cs`:** todos os números propostos (tamanhos, budgets, thresholds) são
  candidatos a constante — nenhum hardcode no tick.
- **Assets do Tibia por ora:** arena/cidade reusam `Biomes.ForTier` e os ids de tile existentes; nenhum
  tileset novo nesta trilha (ver `## Depois` item 4).
- **Sem mudança de gameplay neste prompt:** este doc é só leitura + síntese. A primeira aplicação real
  de código é a LM-04, que consome a §4.
