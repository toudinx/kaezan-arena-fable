# Authored Maps (OTBM → Prefabs) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-07-06-authored-maps-design.md` (aprovado 2026-07-06)

**Goal:** Importar crops autorais do mundo OTServBR (`otservbr.otbm`) como prefabs JSON commitados e estampá-los deterministicamente nos floors procedurais da expedition, com spawn theme por sala; mais 4 docs de anatomia (hunts, boss rooms, cidades, quests) que destravam as fatias futuras.

**Architecture:** Conversor offline em `tools/map-importer/` (Node, padrão convert-monsters) lê OTBM + monster.xml + flags de appearance e emite prefabs JSON em `backend/src/KaezanArenaFable.Api/Content/prefabs/`. `PrefabRegistry` (backend) carrega/valida no startup (fail fast). `DungeonGenerator.Generate` ganha um pool de prefabs: salas selecionadas viram prefabs (blocked estampado antes dos corredores; visuais depois do PaintTiles; corredores conectam pelas bocas declaradas). `GameWorld` usa o spawn theme da sala para composição de espécies, mantendo budget/wave por tier.

**Tech Stack:** Node 20 (ESM, `node --test`), OTBM2JSON vendorado, C# .NET 8 (xunit), AssetExtractor (C#) para flags/sprites, BalanceSim para golden/replay.

## Global Constraints

- **Determinismo do engine:** dentro do tick/geração, apenas o `Rng` da run. Proibido `Random`, `DateTime.Now`, `Guid.NewGuid()`, iteração sem ordem estável. Prefabs carregados com sort ordinal por id.
- **Constantes de simulação** só em `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`.
- **IDs estáveis:** ids `prefab:*` novos nunca serão renomeados depois de commitados; espécies usam os nomes já existentes no `MonsterRegistry`.
- **Código e strings de display em inglês** (comentários inclusive); docs de pesquisa (`docs/mapping/**`) podem ser em português.
- **C# novo/modificado sem `var`** — tipos explícitos sempre (preferência do usuário; o código legado com `var` fica como está).
- **Golden é rebaseline deliberado:** qualquer mudança no gerador exige `--golden` + commit do baseline; replays gravados antes da mudança deixam de re-simular (esperado — re-gravar a bateria).
- **Ao concluir cada task:** `dotnet build` (em `backend/src/KaezanArenaFable.Api`) limpo; commits pequenos direto na `main`, stage seletivo.
- Fonte OTBM (fora do repo, somente leitura): `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\world\otservbr.otbm`, `otservbr-monster.xml`, `otservbr-npc.xml`. Nunca commitar esses arquivos.

---

### Task 1: AssetExtractor `--dump-flags` (fonte de classificação de tiles)

**Model · Effort:** Sonnet 5 · low

**Files:**
- Modify: `tools/AssetExtractor/Program.cs`

**Interfaces:**
- Produces: `tools/map-importer/data/appearance-flags.json` — `{ "<appearanceId>": { "ground": bool, "unpass": bool, "top": bool, "clip": bool } }` para **todos** os object appearances (não só os extraídos). Task 2 e Task 5 consomem este arquivo.

O extractor já parseia `appearances-*.dat` (protobuf) e tem `BuildFlags(app)` (Program.cs:699). Este task adiciona um modo CLI que despeja as flags de todos os objetos.

- [x] **Step 1: Adicionar o modo `--dump-flags`**

No `Program.cs`, junto aos outros modos CLI (siga o padrão do dump `wall-candidates` em ~linha 816), adicionar:

```csharp
private static void DumpFlags(IEnumerable<Appearance> objects, string outPath)
{
    JsonObject root = new JsonObject();
    foreach (Appearance app in objects.OrderBy(a => a.Id))
    {
        JsonObject entry = new JsonObject();
        entry["ground"] = app.Flags?.Bank is not null;
        entry["unpass"] = app.Flags?.Unpass == true;
        entry["top"] = app.Flags?.Top == true;
        entry["clip"] = app.Flags?.Clip == true;
        root[app.Id.ToString()] = entry;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    Console.WriteLine($"appearance flags dumped: {root.Count} → {outPath}");
}
```

Ligar ao arg parsing: `--dump-flags <outPath>` roda `DumpFlags` sobre a lista completa de object appearances já carregada e sai (sem extrair sprites). Ajustar nomes de tipo conforme o código real (`Appearance` é o tipo do proto; conferir o nome usado no arquivo).

- [x] **Step 2: Rodar e verificar**

```powershell
dotnet run --project tools/AssetExtractor -- --dump-flags tools/map-importer/data/appearance-flags.json
```

Esperado: linha `appearance flags dumped: N → ...` com N na casa de dezenas de milhares; o JSON contém `"351":{"ground":true,...}` (chão de caverna conhecido do `Biomes.cs`).

- [x] **Step 3: Commit**

```powershell
git add tools/AssetExtractor/Program.cs tools/map-importer/data/appearance-flags.json
git commit -m "feat(tools): AssetExtractor --dump-flags emits appearance flags for map importer"
```

---

### Task 2: Scaffold do `tools/map-importer` + leitura OTBM + verificação de ids

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Create: `tools/map-importer/package.json`, `tools/map-importer/vendor/otbm2json.js`, `tools/map-importer/config.json`, `tools/map-importer/lib/otbm.mjs`, `tools/map-importer/lib/spawns.mjs`, `tools/map-importer/crop.mjs`, `tools/map-importer/spawns-query.mjs`
- Test: `tools/map-importer/test/otbm.test.mjs`

**Interfaces:**
- Consumes: `tools/map-importer/data/appearance-flags.json` (Task 1).
- Produces: `loadMap(config)` → índice `Map<"x,y,z", Tile>` onde `Tile = { ground: number, items: number[] }` (ids de appearance); `cropTiles(index, {x,y,z,w,h})` → array `w*h` de `Tile|null`; `spawnsInBBox(xmlPath, bbox)` → `[{ name, count }]` ordenado por count desc. `crop.mjs` (CLI) imprime ASCII do crop; `spawns-query.mjs` (CLI) imprime espécies da região. Tasks 3–5 consomem.

- [x] **Step 1: Scaffold + vendorar OTBM2JSON**

`package.json`:

```json
{
  "name": "map-importer",
  "private": true,
  "type": "module",
  "scripts": { "test": "node --test test/" }
}
```

Vendorar o parser MIT de https://github.com/Inconcessus/OTBM2JSON (arquivo `otbm2json.js`) em `vendor/otbm2json.js`, preservando o header de licença. Baixe com:

```powershell
Invoke-WebRequest https://raw.githubusercontent.com/Inconcessus/OTBM2JSON/master/otbm2json.js -OutFile tools/map-importer/vendor/otbm2json.js
```

(Se o layout do repo upstream diferir — ex.: `lib/otbm2json.js` — ajuste o caminho; o critério é: o arquivo exporta `read(file)` que devolve a árvore de nós OTBM em JSON.)

`config.json` (caminhos externos ficam SÓ aqui):

```json
{
  "otbm": "C:/Kaezan/kaezan/canary-3.4.1/data-otservbr-global/world/otservbr.otbm",
  "monsterXml": "C:/Kaezan/kaezan/canary-3.4.1/data-otservbr-global/world/otservbr-monster.xml",
  "flags": "./data/appearance-flags.json",
  "monstersJson": "../../backend/src/KaezanArenaFable.Api/Content/monsters.json"
}
```

(Confirme o caminho real do `monsters.json` gerado pelo convert-monsters — veja `tools/convert-monsters/config.json` — e corrija a chave.)

- [x] **Step 2: Escrever o teste que falha (`test/otbm.test.mjs`)**

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { loadMap, cropTiles } from "../lib/otbm.mjs";
import { readFileSync } from "node:fs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("loadMap indexes tiles with ground ids", () => {
  const index = loadMap(config);
  assert.ok(index.size > 1_000_000, `expected a big world, got ${index.size} tiles`);
});

test("cropTiles returns w*h cells", () => {
  const index = loadMap(config);
  // Thais temple region (surface z=7) is guaranteed populated in otservbr
  const crop = cropTiles(index, { x: 32360, y: 32210, z: 7, w: 10, h: 10 });
  assert.equal(crop.length, 100);
  assert.ok(crop.some(t => t !== null && t.ground > 0), "expected at least one ground tile");
});
```

- [x] **Step 3: Rodar o teste e ver falhar**

```powershell
cd tools/map-importer; node --test test/
```

Esperado: FAIL (`Cannot find module '../lib/otbm.mjs'`).

- [x] **Step 4: Implementar `lib/otbm.mjs`**

Usando o vendor + a doc do formato (`C:\Kaezan\kaezan\mapping\baseline\canary\systems\map.md` §36.1): percorrer `OTBM_MAP_DATA → OTBM_TILE_AREA → OTBM_TILE`, coordenada final = `(base_x + offset_x, base_y + offset_y, base_z)`. Cachear o parse num módulo-level `let cached` (o arquivo tem dezenas de MB; parsear uma vez por processo).

```js
import { readFileSync } from "node:fs";
import otbm2json from "../vendor/otbm2json.js";

let cached = null;

export function loadMap(config) {
  if (cached) return cached;
  const data = otbm2json.read(config.otbm);
  const index = new Map();
  for (const area of tileAreas(data)) {
    for (const tile of area.tiles ?? []) {
      const x = area.position.x + tile.x, y = area.position.y + tile.y, z = area.position.z;
      const items = (tile.items ?? []).map(i => i.id);
      const ground = tile.tileid ?? 0; // OTBM_ATTR_ITEM ground; field name per vendor lib
      index.set(`${x},${y},${z}`, { ground, items });
    }
  }
  cached = index;
  return index;
}

export function cropTiles(index, { x, y, z, w, h }) {
  const out = new Array(w * h).fill(null);
  for (let ly = 0; ly < h; ly++)
    for (let lx = 0; lx < w; lx++)
      out[ly * w + lx] = index.get(`${x + lx},${y + ly},${z}`) ?? null;
  return out;
}
```

Atenção: os nomes de campo (`tileid`, `items`, `position`) variam conforme o vendor — inspecione a saída real do `otbm2json.read` num node REPL e ajuste. Tiles OTBM podem ter o ground como atributo OU como primeiro item com flag `ground` — normalizar aqui usando `appearance-flags.json` (se `ground === 0` e o primeiro item tem flag ground, promova-o).

- [x] **Step 5: Rodar o teste e ver passar**

```powershell
node --test test/
```

Esperado: 2 pass. (O primeiro run é lento — parse completo; ok.)

- [x] **Step 6: Verificação de semântica de ids (crítico)**

Os ids do OTBM do Canary 3.x são os mesmos ids do `items.xml` (que o convert-monsters já trata como appearance/client id — ver comentário em `tools/convert-monsters/convert.mjs:12`). Verificar empiricamente: escrever `crop.mjs` (CLI) que imprime o crop em ASCII (`#` blocked-candidato, `.` ground, id numérico opcional com `--ids`) e rodar sobre uma área de grama conhecida; conferir que os ids de ground batem com ids que existem em `appearance-flags.json` com `ground:true`.

```powershell
node crop.mjs --x 32360 --y 32210 --z 7 --w 20 --h 14 --ids
```

Esperado: grid 20×14 com ids de ground plausíveis (existentes no flags JSON como ground). Se NÃO baterem (ids sem entrada ou não-ground em massa), PARE e investigue o mapeamento server↔client id antes de prosseguir — o resto do pipeline depende disso.

- [x] **Step 7: `lib/spawns.mjs` + `spawns-query.mjs`**

Parser leve do XML (regex linha-a-linha, padrão do `loadItemNames` do convert-monsters — sem dependência):

```js
import { readFileSync } from "node:fs";

// <spawn centerx=".." centery=".." centerz=".." radius=".."> <monster name=".." .../> </spawn>
export function spawnsInBBox(xmlPath, { x, y, z, w, h }) {
  const xml = readFileSync(xmlPath, "utf8");
  const counts = new Map();
  const spawnRe = /<spawn centerx="(\d+)" centery="(\d+)" centerz="(\d+)" radius="(-?\d+)">([\s\S]*?)<\/spawn>/g;
  const monsterRe = /<monster name="([^"]+)"/g;
  let s;
  while ((s = spawnRe.exec(xml)) !== null) {
    const [cx, cy, cz, radius] = [Number(s[1]), Number(s[2]), Number(s[3]), Number(s[4])];
    if (cz !== z) continue;
    const r = Math.max(radius, 0);
    if (cx + r < x || cx - r >= x + w || cy + r < y || cy - r >= y + h) continue;
    let m;
    while ((m = monsterRe.exec(s[5])) !== null)
      counts.set(m[1], (counts.get(m[1]) ?? 0) + 1);
  }
  return [...counts.entries()].map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
}
```

`spawns-query.mjs` (CLI): mesmos args de bbox do crop.mjs; imprime a tabela de espécies. Adicionar um teste em `test/otbm.test.mjs` com uma bbox grande (ex.: 2000×2000 em z=7 ao redor de Thais) assertando `length > 0`.

- [x] **Step 8: Testes + commit**

```powershell
node --test test/
git add tools/map-importer
git commit -m "feat(tools): map-importer scaffold - OTBM reader, crop/spawn query CLIs"
```

---

### Task 3: Docs de anatomia — hunts + boss rooms (com tabela curada de prefabs)

**Model · Effort:** Fable 5 · high

**Files:**
- Create: `docs/mapping/hunt_anatomy.md`, `docs/mapping/boss_room_anatomy.md`

**Interfaces:**
- Consumes: `crop.mjs` / `spawns-query.mjs` (Task 2); sites de curadoria ([TibiaWiki Hunting Places](https://tibia.fandom.com/wiki/Hunting_Places), [intibia](https://intibia.com/hunts), [tibiaroute](https://tibiaroute.com/hunting-places), [TibiaWiki BR](https://www.tibiawiki.com.br/wiki/Locais_de_Ca%C3%A7a)); minimapa visual em [tibiamaps/tibia-map-data](https://github.com/tibiamaps/tibia-map-data) para achar coordenadas.
- Produces: **Tabela curada de 15–20 hunts candidatas** em `hunt_anatomy.md` com colunas exatas: `nome · x · y · z · w · h · tema · tier (1-5) · espécies (nomes do monsters.json) · espécies faltantes · role (mob|treasure|boss)`. Task 5 lê essa tabela para montar o `prefabs-config.json`.

- [x] **Step 1: Levantar candidatas**

Cruzar os sites de hunt (nível baixo→alto) com espécies **já presentes** no `monsters.json` do backend (listar com `node -e` sobre o JSON). Priorizar hunts cujas espécies já existem no jogo (rotworms, orcs, minotaurs, dragons, etc. — conferir no arquivo real). Para cada candidata, localizar coordenadas no otservbr via tibiamaps (as coordenadas do Tibia real valem no otservbr, que replica o mapa global) e confirmar com `crop.mjs` (a ASCII deve mostrar a dungeon) e `spawns-query.mjs` (as espécies devem bater com o site).

- [x] **Step 2: Escrever `hunt_anatomy.md`**

Seções obrigatórias: (1) **Como uma hunt é montada no OTServBR** — zona de spawn (center/radius/densidade do monster.xml), layout (corredores vs salas, chokepoints, escadas), relação nível↔faixa de dificuldade; use 3 exemplos dissecados com ASCII de crops reais e a saída do spawns-query. (2) **Tabela curada** (schema acima, 15–20 linhas, cobrindo tiers 1–5, pelo menos 2 temas por tier onde possível). (3) **Critérios de seleção** usados. Documento em PT; ids e nomes de espécie verbatim.

- [x] **Step 3: Escrever `boss_room_anatomy.md`**

Seções: (1) padrões de arena de boss no otservbr (formato, tamanho típico, gating por alavanca/teleport — dissecar 2–3 exemplos com crops ASCII); (2) o que traduzimos para prefab `role: boss` (arena aberta + entrada única ao sul, compatível com o `CarveAmphitheater` atual) e o que fica fora (mecânica de alavanca = fatia futura); (3) 3–5 candidatas a boss room na tabela (mesmo schema).

- [x] **Step 4: Commit**

```powershell
git add docs/mapping/hunt_anatomy.md docs/mapping/boss_room_anatomy.md
git commit -m "docs(mapping): hunt + boss room anatomy with curated prefab candidates"
```

---

### Task 4: Docs de anatomia — cidades + quests (fatias futuras)

**Model · Effort:** Fable 5 · medium

**Files:**
- Create: `docs/mapping/city_anatomy.md`, `docs/mapping/quest_treasure_anatomy.md`

**Interfaces:**
- Consumes: `crop.mjs`, `spawns-query.mjs`, `otservbr-npc.xml`, pasta `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\world\quest\`, baseline `canary/gameplay/quests.md` e `canary/systems/rewards.md` (repo kaezan).
- Produces: docs de referência para as fatias futuras "cidade hub" e "quests/tesouro". Nenhum código depende deles nesta fatia.

- [x] **Step 1: `city_anatomy.md`** — dissecar 1 cidade (ex.: Thais): estrutura (templo, depot, lojas, guildhall), como NPCs são colocados (`otservbr-npc.xml`: posição + arquivo de comportamento), o que uma "cidade hub" do arena-fable precisaria (zonas seguras, portal de expedition, NPCs de lore). Crops ASCII ilustrando.

- [x] **Step 2: `quest_treasure_anatomy.md`** — padrões de sala de tesouro/quest: baús com actionid/uniqueid (cite `ATTR_ACTION_ID`/`ATTR_UNIQUE_ID` do baseline map.md), storages como gating, salas seladas atrás de portas/alavancas; mapear 2 quests clássicas do otservbr; traduzir para o modelo do arena-fable (sala `role: treasure` com `BenefitChests` hoje; quest chain = fatia futura).

- [x] **Step 3: Commit**

```powershell
git add docs/mapping/city_anatomy.md docs/mapping/quest_treasure_anatomy.md
git commit -m "docs(mapping): city + quest/treasure anatomy for future slices"
```

---

### Task 5: Exportador de prefabs (`export.mjs`) + `prefabs-config.json`

**Model · Effort:** Fable 5 · high

**Files:**
- Create: `tools/map-importer/prefabs-config.json`, `tools/map-importer/export.mjs`, `tools/map-importer/lib/prefab.mjs`
- Test: `tools/map-importer/test/prefab.test.mjs`
- Create (gerados): `backend/src/KaezanArenaFable.Api/Content/prefabs/*.json`

**Interfaces:**
- Consumes: `loadMap`/`cropTiles`/`spawnsInBBox` (Task 2), `appearance-flags.json` (Task 1), tabela curada (Task 3), manifest do frontend (localizar via glob `frontend/src/assets/**/manifest.json` — é o arquivo que o AssetExtractor emite com os appearance ids extraídos), `monsters.json` do backend.
- Produces: **Schema do prefab JSON** consumido pela Task 7 (verbatim):

```json
{
  "id": "prefab:rotworm-cave",
  "role": "mob",
  "tier": 1,
  "theme": "cave",
  "w": 24, "h": 18,
  "ground": [351, 352, 0, ...],
  "wall":   [0, 0, 356, ...],
  "decor":  [0, 1047, 0, ...],
  "blocked": [0, 0, 1, ...],
  "mouths": [{ "x": 0, "y": 9 }, { "x": 23, "y": 8 }],
  "chests": [{ "x": 5, "y": 4 }],
  "spawnTheme": ["Rotworm", "Carrion Worm"],
  "source": { "map": "otservbr", "x": 32100, "y": 32200, "z": 10 }
}
```

Arrays `w*h` row-major (`i = y*w + x`), ids de appearance (ushort), `blocked` 0|1. Todo prefab: ≥1 mouth em célula de borda aberta; células abertas 4-conectadas; `spawnTheme` não-vazio para `role: mob`.

- [x] **Step 1: Teste que falha (`test/prefab.test.mjs`)**

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { buildPrefab } from "../lib/prefab.mjs";
import { readFileSync } from "node:fs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("buildPrefab produces a valid connected prefab", () => {
  // use a linha 1 da tabela curada do hunt_anatomy.md (substituir coords reais)
  const entry = { id: "prefab:test", role: "mob", tier: 1, theme: "cave",
                  x: 32100, y: 32200, z: 10, w: 20, h: 14 };
  const { prefab, gaps } = buildPrefab(config, entry);
  assert.equal(prefab.ground.length, prefab.w * prefab.h);
  assert.equal(prefab.blocked.length, prefab.w * prefab.h);
  assert.ok(prefab.mouths.length >= 1, "needs at least one mouth");
  assert.ok(openCellsConnected(prefab), "open cells must be 4-connected");
});

function openCellsConnected(p) {
  const start = p.blocked.indexOf(0);
  if (start < 0) return false;
  const seen = new Set([start]);
  const stack = [start];
  while (stack.length) {
    const i = stack.pop(), x = i % p.w, y = (i / p.w) | 0;
    for (const [dx, dy] of [[-1,0],[1,0],[0,-1],[0,1]]) {
      const nx = x + dx, ny = y + dy;
      if (nx < 0 || nx >= p.w || ny < 0 || ny >= p.h) continue;
      const ni = ny * p.w + nx;
      if (!seen.has(ni) && p.blocked[ni] === 0) { seen.add(ni); stack.push(ni); }
    }
  }
  return seen.size === p.blocked.filter(b => b === 0).length;
}
```

Rodar `node --test test/` → FAIL (módulo inexistente).

- [x] **Step 2: Implementar `lib/prefab.mjs`**

`buildPrefab(config, entry)` → `{ prefab, gaps }`:

1. `cropTiles` da região; para cada célula classificar com `appearance-flags.json`:
   - tile `null` ou sem ground → `blocked=1`, ground/wall/decor 0 (void; o gerador pinta bedrock ao redor).
   - ground do tile → `ground[i]`.
   - itens do tile: id com `unpass && !ground` → `wall[i] = id`, `blocked[i] = 1`; senão → `decor[i] = id` (último vence; ignorar ids com `top`/`clip` puros de borda — são splashes/decoração de transição, mantêm como decor).
   - id `2472` (chest do jogo) OU baú clássico do Tibia na região → registrar em `chests` e **não** emitir como decor (o engine desenha o chest via POI).
2. **Connectivity fix-up:** manter apenas o maior componente 4-conectado de células abertas (células abertas fora dele viram `blocked=1` com o wall/bedrock da vizinhança) — espelha o flood-fill do `DungeonGenerator`.
3. **Mouths:** células abertas na borda do crop; se `entry.mouths` existir no config, usar verbatim (override manual). Se nenhuma → gap fatal.
4. **spawnTheme:** `spawnsInBBox` na região; mapear nomes case-insensitive contra as espécies do `monsters.json`; presentes → `spawnTheme`; ausentes → `gaps.missingSpecies`.
5. **gaps.missingSprites:** todo id emitido (ground/wall/decor) ausente do manifest do frontend.

- [x] **Step 3: Teste passa**

```powershell
node --test test/
```

Esperado: PASS (com as coords reais da tabela curada no teste).

- [x] **Step 4: `export.mjs` + `prefabs-config.json`**

`prefabs-config.json`: array de entries (schema do teste acima) — preencher com **6–10 linhas da tabela curada** (Task 3): mix de `mob`/`treasure` em 2–3 temas + 1–2 `boss`. `export.mjs`: para cada entry roda `buildPrefab`; se qualquer `gaps.missingSprites` → imprime relatório consolidado (`== GAP REPORT ==` com ids por prefab + espécies faltantes) e **exit 1 sem escrever nada**; com `--report-only` só imprime; sem gaps de sprite → escreve `backend/src/KaezanArenaFable.Api/Content/prefabs/<id-sem-prefixo>.json` (ex.: `rotworm-cave.json`). Espécies faltantes NÃO são fatais (vão pro relatório; spawnTheme sai só com as presentes, desde que ≥1).

- [x] **Step 5: Rodar com `--report-only` e commitar**

```powershell
node export.mjs --report-only
git add tools/map-importer
git commit -m "feat(tools): map-importer prefab exporter with gap report"
```

(Os prefabs JSON em si só serão escritos/commitados na Task 6, com sprites resolvidos.)

---

### Task 6: Extração de sprites + prefabs commitados

**Model · Effort:** Sonnet 5 · low

**Files:**
- Modify: `tools/AssetExtractor/content-config.json`
- Create: `backend/src/KaezanArenaFable.Api/Content/prefabs/*.json` (gerados pelo export)

**Interfaces:**
- Consumes: gap report da Task 5.
- Produces: manifest do frontend com todos os ids dos prefabs; prefabs JSON commitados — input da Task 7.

- [x] **Step 1:** Rodar `node export.mjs --report-only`; copiar os ids faltantes para grupos `semantic` novos em `content-config.json`, nomeados `"prefab.<tema>"` (ex.: `"prefab.cave-extra": [...]`) — o padrão dos grupos existentes (`"ground.cave"`, `"wall.stone"`).
- [x] **Step 2:** Re-rodar o AssetExtractor (instruções no `README.md` raiz — mesmo fluxo usado para monstro/bioma novo) e verificar que o manifest do frontend agora contém os ids.
- [x] **Step 3:** `node export.mjs` → agora escreve os prefabs. Conferir 6–10 arquivos em `backend/src/KaezanArenaFable.Api/Content/prefabs/`.
- [x] **Step 4:** Espécies faltantes que a curadoria considerar essenciais (ex.: o boss temático de uma hunt): adicionar o `.lua` em `tools/convert-monsters/config.json` e re-rodar `node convert.mjs` (fluxo documentado no README). Opcional nesta fatia; senão, deixar registrado no relatório.
- [x] **Step 5: Commit**

```powershell
git add tools/AssetExtractor/content-config.json backend/src/KaezanArenaFable.Api/Content/prefabs frontend/src/assets
git commit -m "feat(content): first authored prefabs + sprite extraction for prefab themes"
```

---

### Task 7: `PrefabRegistry` no backend (load + validação fail-fast)

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Content/PrefabRegistry.cs`
- Modify: `backend/src/KaezanArenaFable.Api/KaezanArenaFable.Api.csproj`, `backend/src/KaezanArenaFable.Api/Program.cs`, `backend/src/KaezanArenaFable.Api/Domain/MonsterRegistry.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/PrefabRegistryTests.cs`

**Interfaces:**
- Consumes: prefabs JSON (Task 6), `MonsterRegistry` existente.
- Produces (Tasks 8–9 dependem):

```csharp
namespace KaezanArenaFable.Api.Content;

public sealed record PrefabPoi(int X, int Y);

public sealed record PrefabDef(
    string Id, string Role, int Tier, string Theme, int W, int H,
    ushort[] Ground, ushort[] Wall, ushort[] Decor, bool[] Blocked,
    PrefabPoi[] Mouths, PrefabPoi[] Chests, string[] SpawnTheme);

public static class PrefabRegistry
{
    public static IReadOnlyList<PrefabDef> All { get; }
    public static void LoadFrom(string dir, Func<string, bool> speciesExists); // throws InvalidDataException
    public static IReadOnlyList<PrefabDef> ForTier(int tier); // sorted ordinal by Id
}
```

E em `MonsterRegistry`: `public bool Has(string name)` (mesmo lookup do `Get`, sem lançar).

- [x] **Step 1: Teste que falha (`PrefabRegistryTests.cs`)**

```csharp
using KaezanArenaFable.Api.Content;

namespace KaezanArenaFable.Api.Tests;

public class PrefabRegistryTests
{
    private static string WritePrefab(string dir, string json)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "test.json");
        File.WriteAllText(path, json);
        return dir;
    }

    // 4x3 prefab: linha do meio aberta, mouth na borda esquerda (0,1)
    private const string Valid = """
    { "id": "prefab:test", "role": "mob", "tier": 1, "theme": "cave", "w": 4, "h": 3,
      "ground": [0,0,0,0, 351,351,351,351, 0,0,0,0],
      "wall":   [356,356,356,356, 0,0,0,0, 356,356,356,356],
      "decor":  [0,0,0,0, 0,0,0,0, 0,0,0,0],
      "blocked":[1,1,1,1, 0,0,0,0, 1,1,1,1],
      "mouths": [{ "x": 0, "y": 1 }],
      "chests": [],
      "spawnTheme": ["Rotworm"],
      "source": { "map": "otservbr", "x": 0, "y": 0, "z": 0 } }
    """;

    [Fact]
    public void loads_valid_prefab()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        PrefabRegistry.LoadFrom(WritePrefab(dir, Valid), name => name == "Rotworm");
        Assert.Single(PrefabRegistry.All);
        Assert.Equal("prefab:test", PrefabRegistry.All[0].Id);
        Assert.Single(PrefabRegistry.ForTier(1));
        Assert.Empty(PrefabRegistry.ForTier(2));
    }

    [Fact]
    public void unknown_species_fails_fast()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        WritePrefab(dir, Valid);
        Assert.Throws<InvalidDataException>(() => PrefabRegistry.LoadFrom(dir, _ => false));
    }

    [Fact]
    public void disconnected_open_cells_fail_fast()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string broken = Valid.Replace("\"blocked\":[1,1,1,1, 0,0,0,0, 1,1,1,1]",
                                      "\"blocked\":[1,1,1,1, 0,1,1,0, 1,1,1,1]");
        WritePrefab(dir, broken);
        Assert.Throws<InvalidDataException>(() => PrefabRegistry.LoadFrom(dir, name => true));
    }
}
```

Rodar: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter PrefabRegistry` → FAIL (tipo não existe).

- [x] **Step 2: Implementar `PrefabRegistry.cs`**

Parse com `System.Text.Json` (`PropertyNameCaseInsensitive = true`; `blocked` chega como `int[]` → converter para `bool[]`; usar um DTO intermediário privado). Validações (todas → `InvalidDataException` com o caminho do arquivo e o motivo):
- `Id` começa com `"prefab:"`; `Role` ∈ {`mob`,`treasure`,`boss`}; `Tier` ∈ 1..5; `W`,`H` ≥ 4.
- Todos os 4 grids com length `W*H`.
- ≥1 mouth; cada mouth em célula de borda (`x==0||y==0||x==W-1||y==H-1`) e aberta.
- Células abertas 4-conectadas (flood-fill — copiar o padrão do `Flood` de `DungeonGeneratorTests`).
- `Role=="mob"` → `SpawnTheme.Length > 0`; toda espécie passa em `speciesExists`.
`LoadFrom` lê `Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal)`, e `All`/`ForTier` devolvem listas ordenadas por `Id` ordinal (determinismo). Diretório inexistente = lista vazia (não é erro: permite rodar sem prefabs).
Sem `var`; tipos explícitos.

- [x] **Step 3: `MonsterRegistry.Has` + wiring**

Adicionar `public bool Has(string name)` ao `MonsterRegistry` (mesma normalização de nome do `Get`). Em `Program.cs`, após o `MonsterRegistry` existir e antes de registrar o `RunManager`:

```csharp
PrefabRegistry.LoadFrom(
    Path.Combine(AppContext.BaseDirectory, "Content", "prefabs"),
    monsterRegistry.Has);
```

No `.csproj`:

```xml
<ItemGroup>
  <Content Include="Content\prefabs\**\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [x] **Step 4: Testes passam + build**

```powershell
dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter PrefabRegistry
dotnet build backend/src/KaezanArenaFable.Api
```

Esperado: 3 pass; build limpo; o backend sobe com os prefabs da Task 6 carregados (rodar `dotnet run` rápido e conferir ausência de exceção).

- [x] **Step 5: Commit**

```powershell
git add backend/src/KaezanArenaFable.Api/Content/PrefabRegistry.cs backend/src/KaezanArenaFable.Api/KaezanArenaFable.Api.csproj backend/src/KaezanArenaFable.Api/Program.cs backend/src/KaezanArenaFable.Api/Domain/MonsterRegistry.cs backend/tests/KaezanArenaFable.Api.Tests/PrefabRegistryTests.cs
git commit -m "feat(content): PrefabRegistry with fail-fast validation"
```

---

### Task 8: Stamping no `DungeonGenerator` + golden

**Model · Effort:** Fable 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs`, `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`, `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (call site do Generate), `tools/BalanceSim/Golden.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs` (novos casos)

**Interfaces:**
- Consumes: `PrefabDef`/`PrefabRegistry` (Task 7).
- Produces: `Room` ganha `public string PrefabId = ""; public string[] SpawnTheme = []; public PrefabDef? Prefab;` (Task 9 lê `SpawnTheme`). Assinatura nova:

```csharp
public static DungeonFloor Generate(Rng rng, int floorIndex, bool isBossFloor, BiomeDef biome,
    IReadOnlyList<PrefabDef>? prefabs = null)
```

- [x] **Step 1: Constantes em `GameConfig.cs`**

```csharp
// LM-08 authored prefabs (OTBM crops stamped into procedural floors)
public const int PrefabMaxPerFloor = 1;      // authored room slots per normal floor (0 disables)
public const double PrefabRoomChance = 0.6;  // chance each slot actually attempts a prefab
public const double PrefabBossChance = 0.5;  // chance the boss hall uses an authored boss prefab
```

- [x] **Step 2: Testes que falham (adicionar a `DungeonGeneratorTests.cs`)**

Helper de prefab de teste (arena 12×10 com anel de parede, mouth a oeste, construído em código — sem arquivo). Adicionar `using KaezanArenaFable.Api.Content;` ao topo do arquivo de testes:

```csharp
private static PrefabDef TestPrefab(string role = "mob")
{
    const int w = 12, h = 10;
    ushort[] ground = new ushort[w * h];
    ushort[] wall = new ushort[w * h];
    ushort[] decor = new ushort[w * h];
    bool[] blocked = new bool[w * h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
            blocked[i] = edge;
            ground[i] = 351;
            if (edge) wall[i] = 356;
        }
    int mouth = (h / 2) * w; // (0, h/2)
    blocked[mouth] = false; wall[mouth] = 0;
    return new PrefabDef($"prefab:test-{role}", role, 1, "cave", w, h,
        ground, wall, decor, blocked,
        [new PrefabPoi(0, h / 2)], [], ["Rotworm"]);
}

[Theory]
[InlineData(1L)]
[InlineData(42L)]
public void prefab_floor_is_deterministic_and_connected(long seed)
{
    PrefabDef[] pool = [TestPrefab()];
    Rng rngA = new Rng((ulong)seed);
    Rng rngB = new Rng((ulong)seed);
    DungeonFloor a = DungeonGenerator.Generate(rngA, 0, isBossFloor: false, Biomes.ForTier(1), pool);
    DungeonFloor b = DungeonGenerator.Generate(rngB, 0, isBossFloor: false, Biomes.ForTier(1), pool);
    Assert.Equal(a.Blocked, b.Blocked);
    Assert.Equal(a.Ground, b.Ground);
    // if a prefab room landed, its open interior must be reachable from entry
    Room? prefabRoom = a.Rooms.FirstOrDefault(r => r.PrefabId != "");
    if (prefabRoom is not null)
    {
        bool[] live = Flood(a);
        Assert.True(live[prefabRoom.CenterY * a.W + prefabRoom.CenterX],
            "prefab interior unreachable from entry");
    }
}

[Fact]
public void prefab_room_stamps_its_ground_ids()
{
    // seed escolhida para garantir placement (PrefabRoomChance=0.6): iterar seeds até achar uma
    for (long seed = 1; seed < 50; seed++)
    {
        Rng rng = new Rng((ulong)seed);
        DungeonFloor f = DungeonGenerator.Generate(rng, 0, false, Biomes.ForTier(1), [TestPrefab()]);
        Room? room = f.Rooms.FirstOrDefault(r => r.PrefabId != "");
        if (room is null) continue;
        // interior cell (center) must carry the prefab's ground id
        Assert.Equal(351, f.Ground[room.CenterY * f.W + room.CenterX]);
        Assert.Equal(["Rotworm"], room.SpawnTheme);
        return;
    }
    Assert.Fail("no seed in 1..49 placed a prefab — placement is broken");
}
```

Rodar `dotnet test --filter DungeonGenerator` → FAIL (assinatura/`PrefabId` inexistentes).

- [x] **Step 3: Implementar no `DungeonGenerator.cs`**

Mudanças, na ordem do `Generate` atual:

1. **Room:** adicionar os 3 campos (`PrefabId`, `SpawnTheme`, `Prefab`) — ver Interfaces.
2. **Placement:** no fluxo multi-room (não single-arena), após a PRIMEIRA room aleatória ser colocada (garante `Rooms[0]` = entry procedural), tentar `GameConfig.PrefabMaxPerFloor` slots: cada slot com `rng.Chance(GameConfig.PrefabRoomChance)` sorteia um prefab de `prefabs` com role `mob`/`treasure` (`rng.Next(count)`) e tenta posições (`GameConfig.RoomPlacementAttempts`, mesmo overlap check +2 dos rects). Sucesso → `floor.Rooms.Add(new Room { X, Y, W = p.W, H = p.H, Role = p.Role, PrefabId = p.Id, SpawnTheme = p.SpawnTheme, Prefab = p })`. Boss floor: se existe prefab `role: boss` e `rng.Chance(GameConfig.PrefabBossChance)`, a boss hall (hoje `w=11;h=9` fixo, linha ~78) usa `p.W/p.H` e recebe os campos de prefab. **Consumo de Rng idêntico com pool vazio** — com `prefabs` null/vazio nenhum draw extra acontece (guard antes de qualquer `rng.*`), preservando os goldens até o rebaseline deliberado.
3. **Carve:** no loop de carve/erosão, prefab room → estampar `p.Blocked` (offset da room) e `continue` (sem erosão).
4. **Corredores:** extrair de `CarveCorridor(floor, Room, Room, rng)` um core por pontos `CarveCorridor(floor, (int X, int Y) from, (int X, int Y) to, rng)`; o wrapper de rooms escolhe os endpoints via:

```csharp
private static (int X, int Y) ConnectionPoint(Room room, Room toward)
{
    if (room.Prefab is not { } p || p.Mouths.Length == 0) return (room.CenterX, room.CenterY);
    PrefabPoi best = p.Mouths[0];
    int bestDist = int.MaxValue;
    foreach (PrefabPoi m in p.Mouths)
    {
        int d = Math.Abs(room.X + m.X - toward.CenterX) + Math.Abs(room.Y + m.Y - toward.CenterY);
        if (d < bestDist) { bestDist = d; best = m; }
    }
    return (room.X + best.X, room.Y + best.Y);
}
```

5. **AssignRoles:** prefab rooms mantêm o `Role` do prefab — pular na redistribuição de detour roles; a seleção de exit (`ladder`/`boss`) ignora prefab rooms, EXCETO boss prefab no boss floor (que É o exit). POIs de chest do prefab: após `AssignRoles`, adicionar `p.Chests` (offset) a `floor.Chests`, e a `floor.BenefitChests` quando `Role == "treasure"`.
6. **Visual stamp (após `PaintTiles`, antes do `Validate`):**

```csharp
private static void StampVisuals(DungeonFloor floor, Room room, PrefabDef p)
{
    int size = floor.W;
    for (int ly = 0; ly < p.H; ly++)
        for (int lx = 0; lx < p.W; lx++)
        {
            int fi = (room.Y + ly) * size + (room.X + lx);
            int pi = ly * p.W + lx;
            if (!p.Blocked[pi])
            {
                floor.Ground[fi] = p.Ground[pi];
                floor.Decor[fi] = p.Decor[pi];
                floor.Wall[fi] = 0;
            }
            else if (floor.Blocked[fi])
            {
                if (p.Ground[pi] != 0) floor.Ground[fi] = p.Ground[pi];
                floor.Wall[fi] = p.Wall[pi];
            }
            // prefab-blocked cell punched open by a corridor: keep the painter's corridor tiles
        }
}
```

7. **Call sites:** `GameWorld` passa `PrefabRegistry.ForTier(<tier da run>)` (achar o call site de `DungeonGenerator.Generate` e o número do tier — `Tier.Tier`); `GenerateTrainingRoom` não muda.

- [x] **Step 4: Testes passam**

```powershell
dotnet test backend/tests/KaezanArenaFable.Api.Tests
```

Esperado: todos os existentes + novos PASS (os existentes chamam `Generate` sem prefabs → comportamento byte-idêntico).

- [x] **Step 5: `Golden.cs` — incluir prefabs**

Em `tools/BalanceSim/Golden.cs`: no `Compute`, carregar `PrefabRegistry.LoadFrom(Path.Combine(RepoRoot(), "backend", "src", "KaezanArenaFable.Api", "Content", "prefabs"), _ => true)` uma vez e passar `PrefabRegistry.ForTier(tier)` ao `Generate` (espelha o GameWorld). Incluir `PrefabId` na string hasheada da sequência de Rooms (junto de `Role`).

- [x] **Step 6: Rebaseline deliberado + verificação**

```powershell
dotnet run --project tools/BalanceSim -- --golden
dotnet run --project tools/BalanceSim -- --golden-check
```

Esperado: baseline reescrito (`docs/balance/golden_dungeon.txt`), check verde. Diff do baseline vai no commit (mudança deliberada).

- [x] **Step 7: Commit**

```powershell
git add backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs tools/BalanceSim/Golden.cs docs/balance/golden_dungeon.txt
git commit -m "feat(engine): stamp authored prefabs into procedural floors (deliberate golden rebaseline)"
```

---

### Task 9: Spawn theme no `GameWorld`

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Engine/SpawnSelection.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (linhas ~499, ~520, ~541)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/SpawnSelectionTests.cs`

**Interfaces:**
- Consumes: `Room.SpawnTheme` (Task 8), `DungeonTier` (`GameConfig.cs:1349`).
- Produces: `public static class SpawnSelection { public static string CommonSpecies(Rng rng, Room room, DungeonTier tier); }`

- [x] **Step 1: Teste que falha**

```csharp
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

public class SpawnSelectionTests
{
    private static DungeonTier Tier() =>
        new DungeonTier(1, "T1", "", ["Rat", "Bug"], ["Cave Rat"], "Boss", 0, 1.0);

    [Fact]
    public void themed_room_picks_only_from_theme()
    {
        Rng rng = new Rng(42UL);
        Room room = new Room { SpawnTheme = ["Rotworm", "Carrion Worm"] };
        for (int i = 0; i < 50; i++)
            Assert.Contains(SpawnSelection.CommonSpecies(rng, room, Tier()), room.SpawnTheme);
    }

    [Fact]
    public void unthemed_room_uses_tier_pool()
    {
        Rng rng = new Rng(42UL);
        Room room = new Room();
        for (int i = 0; i < 50; i++)
            Assert.Contains(SpawnSelection.CommonSpecies(rng, room, Tier()), Tier().CommonMobs);
    }
}
```

Rodar `dotnet test --filter SpawnSelection` → FAIL.

- [x] **Step 2: Implementar `SpawnSelection.cs`**

```csharp
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>Species selection for wave spawns. Prefab rooms carry an authored species theme
/// (LM-08); themed picks still draw from the run rng so replays stay bit-perfect. Difficulty is
/// untouched: budget/wave logic and tier stat multipliers apply to themed species as usual.</summary>
public static class SpawnSelection
{
    public static string CommonSpecies(Rng rng, Room room, DungeonTier tier) =>
        room.SpawnTheme.Length > 0 ? rng.Pick(room.SpawnTheme) : rng.Pick(tier.CommonMobs);
}
```

- [x] **Step 3: Wiring no `GameWorld.cs`**

Nos call sites de spawn comum onde a `room` está disponível (linhas ~499, ~520, ~541 — `_rng.Pick(Tier.CommonMobs)`), trocar por `SpawnSelection.CommonSpecies(_rng, room, Tier)`. Elites, miniboss e boss continuam no pool do tier (spec: só a composição comum é temática nesta fatia).

- [x] **Step 4: Testes + build + commit**

```powershell
dotnet test backend/tests/KaezanArenaFable.Api.Tests
dotnet build backend/src/KaezanArenaFable.Api
git add backend/src/KaezanArenaFable.Api/Engine/SpawnSelection.cs backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs backend/tests/KaezanArenaFable.Api.Tests/SpawnSelectionTests.cs
git commit -m "feat(engine): prefab rooms spawn from authored species theme"
```

---

### Task 10: Verificação fim-a-fim, replay battery, docs e push

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `README.md`, `docs/FABLE_TRACK.md` (marcar a feature `[x]` com 1 linha), `docs/balance/golden_dungeon.txt` (se rebaseline adicional)
- Delete: replays obsoletos em `backend/src/KaezanArenaFable.Api/.data/replays/` (gravados antes da mudança do gerador — não re-simulam mais, esperado)

- [ ] **Step 1: Builds completos**

```powershell
dotnet build backend/src/KaezanArenaFable.Api
cd frontend; npx ng build
```

Esperado: ambos sem erros.

- [ ] **Step 2: Regravar a bateria de replays + replay-check**

A mudança no gerador invalida replays antigos (mesma seed → mapa diferente). Apagar os `.replay.json.gz` antigos, rodar o BalanceSim normal (ele grava replay de cada run terminada — ver `tools/BalanceSim/Program.cs:118` e os args no help do próprio Program.cs), e então:

```powershell
dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays
```

Esperado: `== replay-check: N replay(s) ==` com 0 divergências.

- [ ] **Step 3: Verificação visual no preview**

Subir backend (fluxo canônico `tools/run-backend.ps1`, Release) + frontend, iniciar uma run tier 1 e confirmar: (a) em alguma seed um prefab aparece (visual distinto do procedural, sem buracos de sprite/magenta); (b) monstros da sala prefab são do spawn theme; (c) corredor conecta pela boca (sem parede atravessada). Screenshot como prova. Lembrete operacional: screenshot trava durante combate por freeze de rAF — capturar fora de combate ou usar o truque de rAF hold (ver memória de verificação de HUD).

- [ ] **Step 4: Docs**

- `README.md`: seção curta "Authored prefabs" — o que é, como gerar (`--dump-flags` → curadoria → `export.mjs` → extractor), onde ficam.
- `docs/FABLE_TRACK.md`: marcar a feature `[x]` com resumo de 1 linha.

- [ ] **Step 5: Commit final + push**

```powershell
git add README.md docs/FABLE_TRACK.md backend/src/KaezanArenaFable.Api/.data/replays
git commit -m "docs: authored prefabs pipeline documented; refresh replay battery"
git push origin main
```

---

## Ordem e dependências

```
T1 (flags) ─→ T2 (importer core) ─→ T3 (docs hunts/boss) ─→ T5 (export) ─→ T6 (sprites+prefabs) ─→ T7 (registry) ─→ T8 (stamping) ─→ T9 (spawn theme) ─→ T10 (verificação)
                      └─→ T4 (docs cidades/quests — pode rodar em paralelo a T5–T9)
```

## Riscos conhecidos

- **Semântica de item id no OTBM** (T2 Step 6): se os ids do otservbr.otbm não forem os appearance ids diretos, será preciso um mapeamento adicional (items.otb legacy). O gate está no plano; nada depois dele faz sentido sem isso resolvido.
- **Campos do vendor OTBM2JSON** (T2 Step 4): nomes de campo da árvore JSON podem diferir do esboço — validar no REPL antes de fixar o código.
- **Golden/replay**: dois rebaselines deliberados (T8 e T10). Nunca rebaselinar para "ficar verde" sem entender o diff.
- **Objetos 64px/multi-tile inéditos no renderer** (T10 Step 3): se aparecerem cortados, tratar como decor excluído no export (config de exclusão por id) e re-exportar.
