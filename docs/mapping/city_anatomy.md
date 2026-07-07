# Anatomia de cidades do OTServBR (otservbr.otbm)

> Doc de pesquisa da fatia **authored maps** (plano `docs/superpowers/plans/2026-07-06-authored-maps.md`,
> Task 4), irmão do `hunt_anatomy.md` / `boss_room_anatomy.md` — mesma metodologia empírica
> (`crop.mjs` do `tools/map-importer` + parse direto do OTBM/XML). Referência para a fatia
> futura **"cidade hub"**; nenhum código desta fatia depende dele.

---

## 1. Como uma cidade é montada no OTServBR

Dissecamos **Thais**, a cidade clássica do mundo global. Uma cidade é a composição de
quatro camadas independentes:

| Camada | Onde vive | O que define |
|---|---|---|
| Town node | `OTBM_TOWNS → OTBM_TOWN` no otbm | id, nome e a posição do **templo** (spawn point) |
| Terreno/prédios | `OTBM_TILE_AREA` normais | ruas, muralha, casas, móveis (tiles comuns) |
| Zona segura | `OTBM_ATTR_TILE_FLAGS` (PZ) por tile | onde não há combate |
| NPCs | `otservbr-npc.xml` + `npc/<nome>.lua` | posição da âncora + comportamento |

### 1.1 Town node: o templo é a âncora da cidade

O OTBM declara as towns num nó próprio (`OTBM_TOWNS`). O mundo global tem **31 towns**;
a posição declarada é o tile do templo, usado como spawn/respawn:

```
townid 8   Thais      32369,32241,z7
townid 6   Carlin     32360,31782,z7
townid 7   Kazordoon  32649,31925,z11   (cidade subterrânea — town pos em z=11)
townid 11  Edron      33217,31814,z8
```

(Extraído com um script sobre o vendor `otbm2json` — o nó town tem `{ townid, name, x, y, z }`.)
Kazordoon prova que "cidade" não implica superfície: o town pos pode estar em qualquer z.

### 1.2 Estrutura urbana: ruas, quarteirões e prédios funcionais

`node crop.mjs --x 32338 --y 32204 --z 7 --w 44 --h 42` (quarteirão do depot + templo de Thais;
recorte):

```
............................................   ← rua principal leste–oeste (3 tiles)
....#################...#####....#######..##
...###....#.##################..###..###..#
..###.....#..#######....######..#######.....
..##......#..######......#.#.#..#.#.. ......
..#.......####.#####..####.###..###..#......
..#.........#######......#.#.#..#.#..#..####
..#..........##.###...####.###..###..#..#...
..#..........######......#.#.#..#.#..#..#...
..#.........########..####.###..###..#######
..#..........#.#.###.....#.#.#..#.#..#..#...
..####.......#.#.###.....#.###..###..#..#...
```

Anatomia (a mesma gramática de "estrutura autoral" do `hunt_anatomy.md` §1.2, mas em escala
de cidade):

- **Ruas em grade** de 2–3 tiles separando quarteirões; uma via principal mais larga
  (a faixa de `.` contínua no topo do crop) atravessa a cidade inteira.
- **Prédios = salas retangulares aninhadas** com portas de 1 tile; interiores com móveis
  (os `#` isolados dentro das salas são balcões/estantes — itens `unpass`).
- **Muralha com portões guardados**: os NPCs `Grof, The Guard` (32372,32182) e
  `Walter, The Guard` (32338,32277) ficam exatamente nos portões norte e sul do perímetro.
- **Multi-andar**: os prédios têm z=6 (andar de cima) e porões em z=8+; a cidade "existe"
  em 3–4 níveis de z empilhados sobre o mesmo footprint.

### 1.3 Prédios funcionais verificados (Thais)

O que faz um quarteirão ser "o depot" ou "o templo" não é o tile — é o **NPC de serviço
posicionado dentro dele**:

| Função | NPC | Posição | Observação |
|---|---|---|---|
| Templo | Quentin | 32368,32240,z7 | 1 tile do town pos (32369,32241) — healer/priest |
| Depot | Benjamin | 32349,32219,z7 | post office/depot do quarteirão central |
| Loja de equipamento | Gorn | 32377,32200,z7 | shop keeper (compra/vende) |
| Loja de magia | Xodet | 32398,32222,z7 | vende runas/wands |
| Taverna | Frodo | 32358,32209,z7 | bar — NPC de rumor/lore |
| Banco | Lynda / Elane | 32331,32199 / 32342,32235 | serviços |
| Portões | Grof / Walter | 32372,32182 / 32338,32277 | guardas estáticos |
| Flavour | Towncryer, Sam, Bozo... | espalhados | lore ambiente, sem serviço |

31 âncoras de NPC na bbox da superfície de Thais (170×170 tiles em z=7).

### 1.4 Como NPCs são colocados (otservbr-npc.xml)

Mesma gramática do `otservbr-monster.xml` (ver `hunt_anatomy.md` §1.1), arquivo separado
com **2014 âncoras** no mundo:

```xml
<npc centerx="32368" centery="32214" centerz="7" radius="1">
    <npc name="Aruda" x="0" y="0" z="0" spawntime="60" />
</npc>
```

Diferenças em relação a monstros:

- `radius` é quase sempre **1** — NPC fica parado no posto (o passeio curto vem do
  `walkRadius` do próprio script Lua, não do spawn).
- O **comportamento mora fora do mapa**: cada `name` resolve para um arquivo
  `data-otservbr-global/npc/<nome>.lua` (1029 arquivos no datapack — `frodo.lua`,
  `quentin.lua`, `benjamin.lua`...), que declara keywords de diálogo, tabela de shop,
  módulos de travel etc. **Posição e comportamento são camadas 100% desacopladas** — o
  mapa só sabe o nome.

### 1.5 Zona segura

A Protection Zone não é um prédio: é flag por tile (`OTBM_ATTR_TILE_FLAGS` com bit PZ —
ver baseline `canary/systems/map.md` §36.1). Em Thais, o templo e o depot são PZ; a rua
não é. Ou seja: "cidade" ≠ "zona segura" — a segurança é pintada tile a tile por cima do
terreno, exatamente como o nosso `blocked` é uma máscara sobre o visual.

---

## 2. O que uma "cidade hub" do arena-fable precisaria

Tradução das camadas acima para o nosso modelo (fatia futura — nada disto entra na fatia
atual de prefabs):

1. **Âncora de spawn** (análogo do town pos/templo): o tile onde o jogador aparece entre
   runs. Hoje esse papel é do lobby/UI; um hub espacial daria a ele um lugar físico.
2. **Portal de expedition** (análogo funcional das escadas/barcos): o POI que inicia a run.
   Um prefab `role: hub` com um POI `portal` no lugar de `chests` cobre isso.
3. **Zona segura total**: o hub inteiro sem spawn de inimigos — no nosso modelo basta o
   floor do hub não ter waves (nem precisa de flag por tile).
4. **NPCs de serviço desacoplados do mapa** (a lição mais importante do §1.4): o prefab do
   hub carregaria só *posições nomeadas* (`npcs: [{ x, y, id: "npc:banner-keeper" }]`), e o
   comportamento (abrir gacha, dailies, reliquary) viveria no frontend/backend — espelhando
   posição-no-XML + comportamento-no-Lua do Canary. Mapeamento natural: banner keeper ≅
   loja, quartermaster de dailies ≅ towncryer, reliquary ≅ depot, healer/templo ≅ respawn.
5. **Recorte pequeno**: um quarteirão de Thais (~30×25, como o crop do §1.2) é suficiente
   para um hub — importar a cidade inteira (170×170×4 andares) não cabe no floor de run e
   não serve ao propósito.

**Fica fora mesmo na fatia futura:** casas de jogador (housing), multi-andar (nosso floor
é único por andar de run), economia de NPC (compra/venda por gold do Tibia).

---

## 3. Comandos usados na verificação

```powershell
# terreno (tools/map-importer)
node crop.mjs --x 32338 --y 32204 --z 7 --w 44 --h 42   # quarteirão depot+templo
node crop.mjs --x 32340 --y 32195 --z 7 --w 60 --h 50   # centro de Thais

# towns e NPCs: script ad-hoc sobre vendor/otbm2json.js (nó OTBM_TOWNS) e regex sobre
# otservbr-npc.xml (mesmo padrão do spawnsInBBox de lib/spawns.mjs, trocando <spawn> por <npc>)
```
