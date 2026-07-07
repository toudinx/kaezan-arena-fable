# Anatomia de quests e salas de tesouro do OTServBR (otservbr.otbm)

> Doc de pesquisa da fatia **authored maps** (plano `docs/superpowers/plans/2026-07-06-authored-maps.md`,
> Task 4). Mesma metodologia empírica dos irmãos (`hunt_anatomy.md`, `boss_room_anatomy.md`,
> `city_anatomy.md`): crops ASCII via `crop.mjs` + varredura dos atributos `aid`/`uid` dos
> itens do OTBM (o vendor `otbm2json` expõe `ATTR_ACTION_ID`→`aid` e `ATTR_UNIQUE_ID`→`uid`)
> + leitura dos scripts Lua do datapack Canary 3.4.1. Referência para a fatia futura
> **"quests/tesouro"**; nenhum código desta fatia depende dele.
>
> Baselines citados (repo kaezan): `canary/systems/map.md` §36.1 (atributos de item no OTBM),
> `canary/gameplay/quests.md` §37 (storages, quest_system1/2, chest.lua),
> `canary/systems/rewards.md` §17 (baú com storage check).

---

## 1. As primitivas: actionid e uniqueid no mapa

Uma quest no OTServBR **não existe no mapa** — o mapa só carrega dois inteiros por item
(`ATTR_ACTION_ID` e `ATTR_UNIQUE_ID`, ver baseline map.md §36.1) que servem de gancho para
scripts Lua. Varredura completa do `otservbr.otbm`:

- **693 actionids distintos** em uso; **587 itens** com uniqueid.
- `aid 2000` × 147 — baús do **quest_system1** (lock permanente: storage = `item.uid`).
- `aid 2001` × 36 — baús do **quest_system2** (encadeado: `formerValue`/`newValue`).
- `aid 100` × 222 — itens de mostruário ("you see"), decorativos.
- O resto são aids dedicados: portas de quest (o aid É a chave de storage — `QuestDoorTable`),
  alavancas, teleports scriptados, pisos de evento.

Três mecanismos, todos documentados no baseline quests.md §37.4:

| Mecanismo | Gancho no mapa | Lock | Uso |
|---|---|---|---|
| `quest_system1` | `aid=2000` (+`uid` único) | `storage[uid] > 0` → "empty" | baú de tesouro simples |
| `quest_system2` | `aid=2001` (+`uid`) | storage deve == `formerValue` | baú de estágio de quest |
| `chest.lua` / `quest_reward_common` | `uid` em ranges 5000–12000 | storage ou KV | rewards com config central (`startup/tables/chest.lua` declara `itemPos` + reward + storage; o startup injeta o uid no item do mapa) |

**Storage como gating** (quests.md §37.3): o estado inteiro da quest é um `int32` por
jogador (`-1` = nunca iniciou; valores crescentes por estágio). Portas, baús e teleports só
checam esse inteiro — o mapa é 100% estático, o "progresso" mora no jogador.

---

## 2. Duas quests clássicas dissecadas

### 2.1 The Annihilator (Edron, z=13) — arena gated por alavanca

O padrão-ouro de "boss room gated". Tudo verificado no OTBM + scripts
(`scripts/quests/the_annihilator/lever.lua` e `door.lua`, `startup/tables/chest.lua`).

`node crop.mjs --x 33215 --y 31650 --z 13 --w 26 --h 25`:

```
##########################
############.#.#.#.##.####   ← y=31655: alcovas dos 4 baús de reward (uid 6085–6088)
############.#.#.#.##.####      + corredor da porta selada (coluna x=33236)
####.#.####....#....#.####   ← y=31657: sala dos demons (~11×5, pilares)
############.......##.####
####......#.........#.####      centro da sala: 33221,31659 (do lever.lua)
############.......#######
#####.#.##################
##########################
        (10 linhas de maciço — as duas salas são seladas)
##########################
#####........#############
#####........#############   ← y=31669–31673: sala da alavanca
.#.........#.#############   ← y=31671: fileira dos 4 jogadores (33222–33225)
#####........#############
#####........#############
##########################
```

O fluxo completo, com cada peça no seu lugar:

1. **Alavanca** (`uid 30025`, item 2772): o script exige **4 jogadores level 100+** parados
   nos 4 tiles declarados (`33222–33225, 31671`), checa se a sala está vazia
   (`roomIsOccupied` no centro `33221,31659`), **spawna 6 "Angry Demon"** em posições fixas
   e teleporta os 4 para dentro. A sala dos demons **não tem entrada física** — o crop
   mostra maciço em volta; o teleport é a única porta.
2. **Baús de reward**: 4 chests (item 2472) em alcovas individuais na sala ao norte.
   `chest.lua` declara cada um por `itemPos` (33227/29/31/33, 31656) com reward fixo
   (demon armor, magic sword, stonecutter axe, present) e **o mesmo storage**
   (`TheAnnihilator.Reward`) — abrir um marca todos: escolha única.
3. **Porta selada** (`aid 10102`, `door.lua`): dá acesso de volta à sala dos baús, mas o
   script **bloqueia quem já tem `Reward == 1`** — anti-re-farm espacial.

Morfologia: três câmaras autorais (alavanca → arena → tesouro) ligadas apenas por script.
É "boss room + treasure room" onde os corredores foram substituídos por teleports gated.

### 2.2 Galeria de tesouro sob Thais (z=9) — fileira de baús uid-locked

O padrão de "sala de tesouro pura", sem boss.

`node crop.mjs --x 32306 --y 32238 --z 9 --w 32 --h 14`:

```
################################
########.#.#.#.#.#.#.#.#.#######   ← y=32244: 9 itens de mostruário (aid 100) na parede
########.................#######   ← y=32245: 9 baús 2472, uid 1300–1308, um sob cada item
########.................#######
########.................#######
#########..#...#........########
############...#################   ← entrada única ao sul (corredor de 3 tiles)
############...#################
```

Anatomia: sala retangular ~17×4 com **uma fileira de baús encostados na parede norte, cada
um com seu item de mostruário pendurado acima** (o jogador vê o prêmio antes de abrir) e
**entrada única estreita ao sul**. Cada baú tem `uid` próprio (1300–1308) — o lock
per-player permanente do padrão universal de baú (rewards.md §17.2: `storage[uid] > 0` →
"The chest is empty").

Dois exemplos-satélite do mesmo submundo de Thais, confirmando os dois quest_systems em
campo:

- `32316,32260,z8` — chest 2472 com `aid 2000` + `uid 64142`: **quest_system1** clássico,
  baú avulso escondido num porão (tesouro de exploração).
- `32356,32196,z7` — chest 2472 com `aid 2001` + `uid 65207`: **quest_system2** na
  superfície, dentro de um prédio — baú que só abre no estágio certo de uma quest.

---

## 3. Tradução para o arena-fable

### O que o modelo atual já cobre (fatia de prefabs, Tasks 5–9)

- **Sala `role: treasure`**: a morfologia da galeria de Thais (§2.2) — sala compacta,
  entrada única (1 mouth), baús encostados em parede — é exatamente o que o schema de
  prefab expressa hoje: `chests: [{x,y}]` vira POI de `BenefitChests` quando
  `Role == "treasure"`. A candidata `vampire-crypt-ankrahmun` da tabela do
  `hunt_anatomy.md` já está marcada com esse role.
- **Escolha única de reward** (os 4 baús do Annihilator compartilhando storage) tem análogo
  direto no jogo: os `BenefitChests` do run-end já são "abra um" — a semântica bate de graça.

### O que fica para a fatia futura "quests/tesouro"

- **Quest chain / storage por jogador** (§1): o análogo natural são flags account-level no
  `Meta` (a conta já persiste pity/dailies) — um `storage` por quest id estável
  (`quest:*`), com os mesmos estágios inteiros do padrão Canary. Nada no engine de run
  precisa mudar: gating de quest é decisão de meta, não de tick.
- **Porta/alavanca gated** (Annihilator §2.1): exige POIs interativos com condição
  (`lever`, `sealed-door`) no schema de prefab + eventos de interação no engine. Hoje o
  schema só tem `mouths`/`chests`; a extensão é aditiva (campo novo opcional — não quebra
  prefabs commitados).
- **Arena por teleport** (sala sem entrada física): incompatível com o invariante atual do
  gerador (toda sala conecta por corredor via mouth). Se um dia entrar, é um `role` novo
  com regra própria de conectividade — registrado como decisão explícita, não default.
- **Item de mostruário sobre o baú** (§2.2): puro visual — quando houver POI de chest com
  decor associado, basta o export emitir o item da parede como `decor` normal (já
  suportado); nenhuma mecânica nova.

### Riscos anotados para a fatia futura

- **uid/aid não sobrevivem ao nosso pipeline**: o `lib/otbm.mjs` atual descarta atributos
  de item (só ids). A fatia de quests precisará estender o reader para carregar `aid`/`uid`
  das regiões exportadas — mudança local no importer, sem tocar engine.
- **Dados divergem entre mapa e scripts** no próprio otservbr (ex.: os chests do
  Annihilator têm `uid 4016–4018` gravados no OTBM, mas o `chest.lua` os re-endereça por
  `itemPos` com uids 6085–6088 injetados no startup). Lição: **a fonte de verdade de
  mecânica é o script/config, não o atributo gravado no mapa** — nosso análogo deve manter
  a config de quest fora do prefab JSON (como o Canary mantém `chest.lua` fora do OTBM).

---

## 4. Comandos usados na verificação

```powershell
# tools/map-importer
node crop.mjs --x 33215 --y 31650 --z 13 --w 26 --h 25   # Annihilator (alavanca/arena/baús)
node crop.mjs --x 32306 --y 32238 --z 9 --w 32 --h 14    # galeria de tesouro sob Thais

# varredura de aid/uid: script ad-hoc sobre vendor/otbm2json.js iterando OTBM_TILE_AREA →
# tiles → items e filtrando item.aid/item.uid por bbox (mesma travessia do lib/otbm.mjs)

# scripts consultados (Canary 3.4.1, fora do repo):
#   data-otservbr-global/scripts/quests/the_annihilator/{lever,door}.lua
#   data-otservbr-global/scripts/actions/other/others/quest_system{1,2}.lua
#   data-otservbr-global/scripts/actions/system/quest_reward_common.lua
#   data-otservbr-global/startup/tables/chest.lua
```
