# Anatomia de hunts do OTServBR (otservbr.otbm)

> Doc de pesquisa da fatia **authored maps** (plano `docs/superpowers/plans/2026-07-06-authored-maps.md`,
> Task 3). Tudo aqui foi verificado empiricamente contra o mundo real do OTServBR-Global
> (`otservbr.otbm` + `otservbr-monster.xml`, Canary 3.4.1) usando as CLIs do `tools/map-importer`:
>
> ```powershell
> node crop.mjs --x <x> --y <y> --z <z> --w <w> --h <h>          # ASCII do terreno (# blocked, . livre)
> node spawns-query.mjs --x <x> --y <y> --z <z> --w <w> --h <h>  # espécies na bbox (count desc)
> ```
>
> A **Tabela curada** (§2) é o input direto do `prefabs-config.json` da Task 5.

---

## 1. Como uma hunt é montada no OTServBR

### 1.1 Zona de spawn (otservbr-monster.xml)

O spawn não é "uma área com N monstros": é uma nuvem de **âncoras pequenas e sobrepostas**.
Cada âncora é um elemento `<monster centerx=".." centery=".." centerz=".." radius="..">`
(o wrapper também se chama `monster`, não `spawn` — pegadinha do formato do Canary) contendo
1–3 crias com offset relativo ao centro e `spawntime` (90s é o padrão dominante):

```xml
<monster centerx="32146" centery="32350" centerz="9" radius="2">
    <monster name="Rotworm" x="0" y="1" z="0" spawntime="90" />
</monster>
```

Consequências para o nosso modelo:

- **Densidade = quantidade de âncoras, não raio.** Raio típico é 2–3 tiles; uma hunt "cheia"
  como o templo antigo tem ~40 âncoras de Skeleton num corredor de 70×60. A sensação de
  "área infestada" vem da sobreposição, não de spawns grandes.
- **Composição é local.** A mistura de espécies muda por corredor: a mesma caverna tem bolsões
  só de Rotworm e bolsões com Carrion Worm no meio. O nosso `spawnTheme` por sala captura
  exatamente isso — a sala inteira compartilha um tema, e o budget/wave do tier faz o resto.
- **Respawn fixo (90s)** não nos interessa: o arena-fable usa waves por budget. O que importa
  do XML é **quais espécies** e **em que proporção** (o `count` do spawns-query é um proxy
  direto da proporção).

### 1.2 Layout: corredores, salas e chokepoints

Três morfologias se repetem no mundo inteiro (dissecadas em §1.4):

1. **Caverna natural** — túneis sinuosos de 1–3 tiles de largura que se alargam em bolsões.
   Chokepoints em toda transição túnel→bolsão. É a morfologia mais próxima do nosso gerador
   procedural atual (rooms + corredores + erosão).
2. **Estrutura autoral** (cidade, templo, fortaleza) — salas retangulares, paredes retas,
   portas de 1–2 tiles, simetria. É o que o procedural **não** produz e o motivo de importar
   prefabs: Mintwallin e Cyclopolis são imediatamente reconhecíveis como "lugar construído".
3. **Salão amplo** — arena aberta com pilares ou anel de parede, tipicamente associada a
   bosses e elite mobs (ver `boss_room_anatomy.md`).

Escadas/buracos de corda conectam os andares (z) e são sempre gargalos de 1 tile — no nosso
modelo viram a `ladder` procedural; o prefab não precisa carregá-las.

### 1.3 Nível ↔ faixa de dificuldade

No OTServBR a dificuldade cresce com **profundidade e distância da cidade**: rotworms a
1 andar do templo de Thais, demônios a 8 andares sob Edron ou em ilhas remotas. Traduzido
para os nossos tiers (`GameConfig.Tiers`):

| Tier do jogo | Tema (GameConfig) | Análogo no OTServBR |
|---|---|---|
| 1 Echoing Burrow | vermes/cavernas | Rotworm caves (Mount Sternum, Darashia), troll caves |
| 2 Uruk Stronghold | orcs/goblins/anões | Orc Fortress, goblin caves, minas de Kazordoon |
| 3 Dark Crypt | mortos-vivos | Ancient Temple, Drefia, tumbas do deserto |
| 4 Scaled Den | minotauros/dragões | Mintwallin, Cyclopolis, dragon lairs |
| 5 Echoing Abyss | demônios/topo | Demona, hellhound caves, hydra caves |

A coluna `tier` da tabela em §2 segue **os pools do jogo**, não o "nível recomendado" do
Tibia — ex.: minotauros são conteúdo inicial no Tibia mas são `CommonMobs` do tier 4 aqui.

### 1.4 Três hunts dissecadas

#### (a) Rotworm cave — Mount Sternum (caverna natural, tier 1)

`node spawns-query.mjs --x 32125 --y 32316 --z 9 --w 68 --h 46`:

```
  25 Rotworm
   9 Carrion Worm
```

`node crop.mjs --x 32125 --y 32316 --z 9 --w 68 --h 46` (recorte central):

```
..##.#############.....#######..........##.########
..################.....#######...........##########
..##.#############.....#############......#########
.##..#############.........#########....################
##.###############.........#########....##################
#.##..#################....#########....##################
###....################....#########..####################
............###########....#########..####################
........###############....#########.#####################
........#####..............###########.##..####...########
#############.......#########.....####.##.........########
##########....###...#########......###.###..........######
##########...####....#################.###..........######
##########...####....################.##...........######.
##########...####......#############...#......#....#####..
##########....###......#############...##..........###....
```

Anatomia: túneis de 1–3 tiles serpenteando entre bolsões de 4–8 tiles; nenhuma linha reta
longa; muitos becos. A proporção 25:9 Rotworm:Carrion Worm é o template perfeito de
`spawnTheme` ponderado só pela presença (o engine sorteia uniforme dentro do tema — a
proporção real vem de quantas âncoras cada espécie tem, que aqui é ~3:1).

#### (b) Mintwallin — cidade minotaura (estrutura autoral, tier 4)

`node spawns-query.mjs --x 32383 --y 32088 --z 15 --w 64 --h 45`:

```
  20 Minotaur
   8 Minotaur Guard
   3 Minotaur Archer
   2 Minotaur Mage
   1 Snake
```

`node crop.mjs --x 32383 --y 32088 --z 15 --w 64 --h 45` (recorte):

```
.#..#################..#########################################
.#..#...##......#..##..#........##.........###........##........
.#..#....#......#...#..#........##.........####.......##.....###
.##.#....#......#...#..#........##......#########......#.....#..
..#.#....#......#...#..#........##......##.....##......#....##..
.##.#....###.####...#..#........##......#.......#......#######..
.####....#..........#..#.......####.....#.......#...............
...##....#..........#..#.############...#.......#...............
..###....##########.#..#.###..####..#...#.......#...............
..###....####...###.#..#.##...####..#...#.......#######....#####
..##.....##.......#.#..#.##...##....#...#.............#....#.###
.........##########.#..#.##...##....#...##......####..##...#.###
```

Anatomia: ruas retas de 2–3 tiles, quarteirões de casas com portas de 1 tile, salas
retangulares aninhadas. A hierarquia de patente (Minotaur comum nas ruas, Guard/Mage nos
prédios) é um padrão que o spawn theme por sala reproduz de graça quando recortamos um
quarteirão como prefab. É o exemplo canônico do que o procedural não gera sozinho.

#### (c) Demona — complexo demoníaco a leste de Edron (salões estruturados, tier 5)

`node spawns-query.mjs --x 33404 --y 31745 --z 8 --w 72 --h 67`:

```
  21 Demon
  18 Hellspawn
  14 Dark Torturer
  14 Grim Reaper
  13 Juggernaut
  12 Destroyer
  11 Hellhound
```

`node crop.mjs --x 33412 --y 31776 --z 8 --w 30 --h 26` (o salão central):

```
.###############....##########
.###############....##########
#####...#.#................###
#####......................###
#####......................###
.####......................###
.####......................###
.####......................###
.####......................###
.####......................###
.####......................###
.###############....##########
.###############....##########
```

Anatomia: salões retangulares de ~22×18 com bocas de 4 tiles no eixo norte–sul, repetidos
em grade e ligados por corredores largos (4 tiles) — arquitetura de "fortaleza infernal".
Metade do bestiário local ainda não existe no jogo (Hellspawn, Grim Reaper, Destroyer),
mas Demon + Dark Torturer + Juggernaut + Hellhound já formam um tema tier 5 completo.

---

## 2. Tabela curada de candidatas a prefab

Colunas: `nome · x · y · z · w · h · tema · tier (1-5) · espécies (nomes do monsters.json) ·
espécies faltantes · role (mob|treasure|boss)`.

**Nota sobre w·h:** são as janelas **verificadas** (crop + spawns conferidos); o prefab final
da Task 5 recorta uma sub-região dentro delas (salas de ~16–32 tiles de lado), então um
candidato pode render mais de um prefab. Coordenadas são absolutas do otservbr.otbm.

| nome | x | y | z | w | h | tema | tier | espécies | espécies faltantes | role |
|---|---|---|---|---|---|---|---|---|---|---|
| rotworm-cave-sternum | 32125 | 32316 | 9 | 68 | 46 | cave | 1 | Rotworm, Carrion Worm | — | mob |
| rotworm-cave-darashia | 33220 | 31872 | 9 | 60 | 62 | cave | 1 | Rotworm | — | mob |
| troll-cave-thais | 32325 | 32125 | 8 | 62 | 42 | cave | 1 | Troll, Spider, Cave Rat, Poison Spider | — | mob |
| orc-fortress | 32893 | 31688 | 7 | 68 | 60 | fortress | 2 | Orc, Orc Spearman, Orc Warrior, Orc Berserker, Orc Shaman, Orc Rider, Wolf | Orc Leader, Pig, Chicken | mob |
| goblin-cave-edron | 33045 | 31805 | 10 | 40 | 58 | cave | 2 | Goblin, Goblin Scavenger | Goblin Assassin | mob |
| kazordoon-mines | 32444 | 31933 | 10 | 72 | 65 | mine | 2 | Dwarf, Dwarf Soldier, Rotworm, Poison Spider | Dwarf Guard | mob |
| ancient-temple-upper | 32380 | 32005 | 9 | 70 | 60 | crypt | 3 | Skeleton, Ghoul, Rat | — | mob |
| ancient-temple-lower | 32383 | 31996 | 10 | 65 | 65 | crypt | 3 | Ghoul, Skeleton, Cave Rat | — | mob |
| drefia-necromancer | 32974 | 32380 | 10 | 54 | 68 | crypt | 3 | Necromancer, Demon Skeleton, Ghost, Ghoul, Rat | Priestess, Shadow Pupil, Lich, Skeleton Warrior, Blood Hand, Zombie, Tarnished Spirit, Bat | mob |
| desert-tomb-mummy | 32255 | 32577 | 9 | 65 | 65 | tomb | 3 | Mummy, Ghost, Crypt Shambler | Gargoyle, Zombie | mob |
| vampire-crypt-ankrahmun | 33095 | 32960 | 14 | 57 | 46 | tomb | 3 | Vampire, Ghost, Mummy, Demon Skeleton, Necromancer | Stalker, Ancient Scarab, Bonebeast | treasure |
| mintwallin | 32383 | 32088 | 15 | 64 | 45 | city | 4 | Minotaur, Minotaur Guard, Minotaur Archer, Minotaur Mage, Snake | — | mob |
| cyclopolis | 33216 | 31677 | 8 | 68 | 57 | halls | 4 | Cyclops, Dwarf Soldier | Dwarf Guard, Cyclops Drone | mob |
| dragon-lair-darashia | 33212 | 32257 | 11 | 61 | 58 | lava | 4 | Dragon Lord | — | mob |
| hydra-cave-porthope | 32776 | 32510 | 11 | 58 | 69 | jungle-cave | 5 | Hydra | Bonebeast, Serpent Spawn, Medusa | mob |
| giant-spider-tiquanda | 32907 | 32512 | 8 | 52 | 49 | jungle-cave | 5 | Giant Spider | Tarantula | mob |
| hellhound-cave-edron | 33217 | 31613 | 11 | 53 | 51 | fire-cave | 5 | Hellhound | Cyclops Smith | mob |
| demona | 33404 | 31745 | 8 | 72 | 67 | demon-halls | 5 | Demon, Dark Torturer, Juggernaut, Hellhound | Hellspawn, Grim Reaper, Destroyer | mob |
| behemoth-drefia | 33021 | 32469 | 9 | 45 | 44 | cave | 5 | Behemoth, Cave Rat, Demon Skeleton, Ghoul | Skeleton Warrior | mob |
| frost-dragon-cave | 32131 | 31429 | 8 | 40 | 20 | ice-cave | 5 | Frost Dragon, Frost Dragon Hatchling | — | mob |

Cobertura: 20 candidatas — T1×3, T2×3, T3×5, T4×3, T5×6; ≥2 temas por tier em todos os
tiers (T1 tem cave em duas regiões distintas — caverna úmida de Sternum e caverna seca do
deserto de Darashia, visualmente diferentes pelos ground ids).

Candidatas a **boss room** (Orc Warlord, Black Knight, Dragon Lord, Demon) estão na tabela
do `boss_room_anatomy.md` — mesma escola, schema idêntico.

### Observações por candidata

- **vampire-crypt-ankrahmun** (role `treasure`): cripta estruturada com câmaras seladas
  simétricas — o layout natural de "sala de baú". Melhor candidata a `BenefitChests`.
- **frost-dragon-cave**: túneis muito estreitos; usar o recorte pequeno indicado (40×20).
  Espécies 100% presentes — vale o esforço de curadoria fina na Task 5.
- **kazordoon-mines**: a janela contém muito maciço; recortar os corredores de mina
  (túneis retos com trilhos) na metade sul da janela.
- **Draconia** (dragões em superfície, 33215,31295,z7) foi **rejeitada**: crop quase todo
  rocha, sem salas utilizáveis — dragões tier 4 ficam melhor servidos pelo lair de Darashia.
- **dragon-peak-tiquanda** (33027,32638,z7, Dragon x18) foi rejeitada pelo mesmo motivo
  (montanha de superfície com vale estreito).

---

## 3. Critérios de seleção usados

1. **Espécies já no jogo primeiro.** Toda candidata tem ≥1 espécie presente no
   `monsters.json` do backend (62 espécies hoje); a coluna "faltantes" lista o resto do
   bestiário local para decisão futura (Task 6 Step 4 — adicionar `.lua` no convert-monsters
   se a curadoria julgar essencial).
2. **Verificação empírica dupla.** Coordenada só entra na tabela depois de: (a)
   `spawns-query.mjs` confirmar as espécies na bbox; (b) `crop.mjs` mostrar layout utilizável
   (salas/corredores legíveis, não maciço de rocha). As coordenadas foram encontradas
   mineirando o próprio `otservbr-monster.xml` por clusters de âncoras de spawn — não por
   memória de wiki — e batem com os locais clássicos (Ancient Temple, Mintwallin, Drefia,
   Orc Fortress etc.).
3. **Tier pelo pool do jogo** (`GameConfig.Tiers`), não pelo nível do Tibia: a candidata
   entra no tier cujo `CommonMobs`/`EliteMobs` contém suas espécies dominantes.
4. **Cobertura de morfologia.** Mix deliberado de caverna natural (parecida com o
   procedural — transição suave), estrutura autoral (cidade/templo/fortaleza — o payoff
   visual dos prefabs) e salão amplo (candidatas a boss/treasure).
5. **Diversidade de tema visual por tier**, para o pool de prefabs do gerador não repetir
   a mesma cara em runs seguidas do mesmo tier.
