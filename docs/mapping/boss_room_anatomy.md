# Anatomia de boss rooms do OTServBR (otservbr.otbm)

> Doc de pesquisa da fatia **authored maps** (plano `docs/superpowers/plans/2026-07-06-authored-maps.md`,
> Task 3), irmão do `hunt_anatomy.md` — mesma metodologia de verificação (`crop.mjs` +
> `spawns-query.mjs` do `tools/map-importer`), mesmo schema de tabela.

---

## 1. Padrões de arena de boss no OTServBR

Dissecamos três arenas reais. Os padrões que se repetem:

- **Formato:** ou octógono/losango selado (Black Knight), ou salão retangular com pilares
  (Demona), ou caverna ampla de teto alto (Dragon Lords). Sempre **um espaço aberto único**
  — o boss precisa de room para perseguir o alvo.
- **Tamanho típico:** 17×16 a 22×18 de interior útil; as cavernas de dragão passam de 40×30
  mas o "palco" do encontro é um bolsão central de ~20 tiles.
- **Entradas:** 1–2 bocas estreitas (1–4 tiles) em lados opostos ou lado único. A entrada
  estreita É a mecânica: força o jogador a se comprometer com o espaço do boss.
- **Escolta:** o boss quase nunca está sozinho no XML — Orc Warlord vem com Berserkers e
  Shamans, Black Knight com Bonelords, Demon com a corte infernal. A escolta é da mesma
  família temática do boss.
- **Gating:** as arenas mais icônicas ficam atrás de alavanca, porta com chave ou teleport
  (padrão quest — `ATTR_ACTION_ID`/`ATTR_UNIQUE_ID` no OTBM; detalhado no
  `quest_treasure_anatomy.md` da Task 4). O acesso gated fica **fora** desta fatia.

### (a) Black Knight — vila em Plains of Havoc, subsolo (z=11)

Boss do tier 3 do jogo. Única âncora de spawn do Black Knight no mundo inteiro — sala
exclusiva do encontro.

`node spawns-query.mjs --x 32856 --y 31933 --z 11 --w 40 --h 32`:

```
   2 Bonelord
   2 Scorpion
   1 Black Knight
```

`node crop.mjs --x 32856 --y 31933 --z 11 --w 40 --h 32` (recorte da arena):

```
##############.##....################
#############...........#############
############.............############
##########.................##########
###########................##########
##########.................##########
###########...............###########
##########.................##########
##########.................##########
###########...............###########
##########.................##########
###########................##########
##########.................##########
############.............############
#############...........#############
##############.........##############
#####################################
################.####################
```

Anatomia: octógono de ~17×16 escavado em maciço sólido, entradas de 1 tile ao norte e ao
sul, zero cobertura interna — duelo puro. É o formato mais próximo do nosso
`CarveAmphitheater` atual (`DungeonGenerator.cs`), o que faz dele o candidato de menor
atrito para o primeiro prefab `role: boss`.

### (b) Demon hall — complexo de Demona, leste de Edron (z=8)

Demon é o boss do tier 5 do jogo.

`node spawns-query.mjs --x 33412 --y 31776 --z 8 --w 30 --h 26`:

```
   6 Dark Torturer
   5 Demon
   4 Destroyer
   4 Grim Reaper
   4 Hellhound
   4 Hellspawn
   4 Juggernaut
```

`node crop.mjs --x 33412 --y 31776 --z 8 --w 30 --h 26`:

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
.###############....##########
.###############....##########
```

Anatomia: salão retangular de ~22×18 com bocas de 4 tiles centradas no norte e no sul,
paredes retas, repetido em grade pelo complexo (arquitetura autoral de fortaleza infernal).
Boca larga = encontro de grupo, não duelo. Como prefab boss, as duas bocas declaradas dão
ao gerador liberdade de conectar o corredor por qualquer eixo.

### (c) Caverna dos Dragon Lords — sob Plains of Havoc (z=12)

Dragon Lord é o boss do tier 4 do jogo.

`node spawns-query.mjs --x 32768 --y 32317 --z 12 --w 60 --h 50`:

```
  19 Dragon Lord
   5 Dragon
```

`node crop.mjs --x 32768 --y 32317 --z 12 --w 60 --h 50` (recorte central):

```
......########............######.....#####..................
#.....########.............###..............................
##.....#####................................................
###.........................................................
###..................####..................................#
####.................#..##.................................#
######...............#...#.........######...........##....##
############.........#####.........#######........##########
##############..............###..##.########......##########
```

Anatomia: caverna aberta de ~40×20 no miolo, com ilhotas de rocha servindo de cobertura
esparsa e vários acessos por túneis. É o padrão "lair": o chefe domina um espaço amplo e a
densidade de spawn (19 âncoras!) transforma a sala inteira no encontro. Como prefab, um
recorte de ~30×22 do bolsão central com 2 bocas funciona como boss hall de tier 4.

---

## 2. O que traduzimos para prefab `role: boss` — e o que fica fora

**Entra nesta fatia** (compatível com o `DungeonGenerator` atual, que hoje escava uma boss
hall fixa via `CarveAmphitheater` no boss floor):

- Arena aberta com interior 4-conectado e ≥1 mouth em borda — o corredor procedural conecta
  pela boca declarada, preservando a entrada estreita autoral.
- Escolta temática via `spawnTheme` (a composição comum da sala vem do tema; o boss em si
  continua vindo do `Boss` do tier — o prefab só fornece o palco e a corte).
- Chests autorais (`chests` do schema) para recompensa pós-boss quando a arena original
  tiver baú.

**Fica para fatia futura:**

- **Gating por alavanca/teleport/chave** (padrão Annihilator): exige actionid/uniqueid,
  storage e scripting — mapeado no `quest_treasure_anatomy.md`, sem correspondência no
  engine hoje.
- **Multi-andar** (arenas com queda/corda para o nível do boss): nosso floor é único por
  andar da run; a transição vertical continua sendo a `ladder` procedural.
- **Boss mechanics scriptadas** (summons em fase, imunidade até evento): o boss do
  arena-fable é data-driven por stats; mecânica de encontro é outra fatia.

## 3. Candidatas a boss room

Mesmo schema da tabela do `hunt_anatomy.md`. A coluna espécies lista boss + escolta
presentes no `monsters.json`.

| nome | x | y | z | w | h | tema | tier | espécies | espécies faltantes | role |
|---|---|---|---|---|---|---|---|---|---|---|
| orc-warlord-throne | 32930 | 31755 | 8 | 40 | 32 | fortress | 2 | Orc Warlord, Orc Berserker, Orc Shaman, Orc Rider, War Wolf, Bonelord | Orc Leader | boss |
| black-knight-room | 32856 | 31933 | 11 | 40 | 32 | crypt | 3 | Black Knight, Bonelord, Scorpion | — | boss |
| dragonlord-cavern | 32768 | 32317 | 12 | 60 | 50 | lava | 4 | Dragon Lord, Dragon | — | boss |
| demon-hall-demona | 33412 | 31776 | 8 | 30 | 26 | demon-halls | 5 | Demon, Dark Torturer, Juggernaut, Hellhound | Hellspawn, Grim Reaper, Destroyer | boss |

Notas:

- **orc-warlord-throne** é o subsolo do trono da Orc Fortress: complexo de salas pequenas
  em vez de arena única — candidato a boss room "composta" (recortar a sala do trono, ~14×12,
  na metade oeste da janela). Warlord x4 no XML = âncoras do mesmo encontro.
- **Alinhamento perfeito com os bosses do jogo:** Orc Warlord (T2), Black Knight (T3),
  Dragon Lord (T4) e Demon (T5) são literalmente os `Boss` de `GameConfig.Tiers` — cada um
  tem arena autoral real no otservbr.
- **Gap do tier 1:** Rotworm Queen não tem âncora no `otservbr-monster.xml` (spawn de raid
  scriptado), logo não existe arena autoral para importar. O boss floor do tier 1 continua
  com o amphitheater procedural — registrado como gap deliberado.
