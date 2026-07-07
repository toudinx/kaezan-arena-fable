# Map Beauty + Map Lab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Ao terminar a implementação de cada task, marque as checkboxes `[x]` dos steps e da task
> neste arquivo.** Ao fechar a fatia inteira (Task 14), marque também LM-09→LM-12 no
> `docs/roadmap/ongoing/roadmap_dungeons.md`. Nunca conclua sem atualizar os checkboxes.

**Spec:** `docs/superpowers/specs/2026-07-07-map-beauty-and-map-lab-design.md` (aprovado 2026-07-07)

**Goal:** Mapas procedurais bonitos (wall sets 47-blob, borders de chão, manchas coerentes, decor sem corte) usando os dados curados do Remere's Map Editor, mais a seção admin **Map Lab** (preview por seed/tier + editor de presets de bioma).

**Architecture:** Conversor offline em `tools/map-importer/` traduz os XMLs de materials do RME (checkout externo, nunca commitado) em `Content/tilesets.json` (commitado), validado por predição contra o `otservbr.otbm`. No backend, `TilesetRegistry` (fail-fast) alimenta o `BiomeDef` v2 (famílias nomeadas) e o `PaintTiles` v2 (manchas Voronoi + camada de border 2-slot + wall sets via `WallAutotile` existente). O Map Lab materializa LM-09→LM-12: endpoints admin + aba com canvas de preview e editor de preset (`ContentStore.ReplaceBiomes`).

**Tech Stack:** Node 20 (ESM, `node --test`), C# .NET 8 (xunit), Angular 21 standalone + signals, BalanceSim para golden/replay.

## Global Constraints

- **Determinismo do engine:** dentro da geração, apenas o `Rng` da run em ordem de varredura fixa. Proibido `Random`, `DateTime.Now`, `Guid.NewGuid()`, iteração sem ordem estável.
- **Constantes de simulação** só em `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`.
- **Código e strings de display em inglês** (comentários inclusive); docs em `docs/**` podem ser PT.
- **C# novo/modificado sem `var`** — tipos explícitos sempre (legado com `var` fica como está).
- **Fontes externas nunca commitadas:** checkout do RME e `otservbr.otbm` ficam fora do repo; caminhos só no `tools/map-importer/config.json`. Commitamos apenas o `tilesets.json` derivado.
- **Golden é rebaseline deliberado:** UM rebaseline nesta fatia (Task 9), junto com a regravação da bateria de replays. Nunca rebaselinar para "ficar verde" sem entender o diff.
- **Ao concluir cada task:** `dotnet build` limpo (backend) e/ou `npx ng build` limpo (frontend); commits pequenos direto na `main`, stage seletivo; **checkboxes da task marcadas `[x]` neste arquivo**.
- Fonte RME (fora do repo, somente leitura): clonar `https://github.com/opentibiabr/remeres-map-editor` em `C:\Kaezan\kaezan\remeres-map-editor` (os XMLs usados ficam em `data\materials\`). RME é GPL — dados lidos em tool-time, nunca copiados pro repo.

---

## Entrega ① — Tilesets (Tasks 1–4)

### [x] Task 1: Parser dos XMLs de materials do RME (`lib/rme.mjs`)

Resumo: parser RME criado para border sets e ground brushes, com config do checkout externo e testes Node cobrindo `grass`/`mountain`.

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Create: `tools/map-importer/lib/rme.mjs`
- Modify: `tools/map-importer/config.json` (chave `rmeMaterials`)
- Test: `tools/map-importer/test/rme.test.mjs`

**Interfaces:**
- Produces (Tasks 2–3 consomem):
  - `loadBorders(materialsDir)` → `Map<number, Record<string, number>>` — border id → `{ n, e, s, w, cnw, cne, csw, cse, dnw, dne, dsw, dse }` (edges ausentes omitidos; valores = item ids).
  - `loadGroundBrushes(materialsDir)` → `Map<string, GroundBrush>` onde `GroundBrush = { name, lookid, zOrder, items: number[], borders: [{ align: "outer"|"inner", to: string|null, id: number }] }` (ordem dos `<border>` preservada — o RME resolve por primeira regra que casa).

- [x] **Step 1: Config.** Adicionar ao `tools/map-importer/config.json`:

```json
"rmeMaterials": "C:/Kaezan/kaezan/remeres-map-editor/data/materials"
```

Clonar o repo se ainda não existir: `git clone --depth 1 https://github.com/opentibiabr/remeres-map-editor C:\Kaezan\kaezan\remeres-map-editor`

- [x] **Step 2: Teste que falha (`test/rme.test.mjs`)**

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { loadBorders, loadGroundBrushes } from "../lib/rme.mjs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("loadBorders parses the 12-edge sets", () => {
  const borders = loadBorders(config.rmeMaterials);
  assert.ok(borders.size > 100, `expected 100+ border sets, got ${borders.size}`);
  const b1 = borders.get(1); // border id 1: full 12-edge grass/dirt set (verified in repo)
  assert.ok(b1, "border id 1 must exist");
  for (const edge of ["n", "e", "s", "w", "cnw", "cne", "csw", "cse", "dnw", "dne", "dsw", "dse"])
    assert.ok(Number.isInteger(b1[edge]), `border 1 missing edge ${edge}`);
});

test("loadGroundBrushes parses items, z-order and border refs", () => {
  const brushes = loadGroundBrushes(config.rmeMaterials);
  const grass = brushes.get("grass");
  assert.ok(grass, "grass brush must exist");
  assert.ok(grass.items.length >= 10, "grass has many item variants");
  assert.ok(grass.zOrder > 0);
  assert.ok(grass.borders.some(b => b.align === "outer"), "grass has an outer border");
  const mountain = brushes.get("mountain");
  assert.ok(mountain, "mountain brush must exist");
  assert.ok(mountain.borders.some(b => b.align === "inner"), "mountain has inner borders");
});
```

Rodar `node --test test/rme.test.mjs` (em `tools/map-importer/`) → FAIL (módulo inexistente).

- [x] **Step 3: Implementar `lib/rme.mjs`.** Parser regex linha-a-linha (padrão do `spawns.mjs` — sem dependência). Atenção: `borders.xml` e `brushs.xml` na raiz de materials são só `<include file="..."/>` — seguir os includes (ler `borders/borders.xml` e todos os XMLs em `brushs/`). Estruturas reais (verificadas no repo em 2026-07-07):

```xml
<border id="1">
  <borderitem edge="n" item="4445"/> ... (12 edges)
</border>

<brush name="grass" type="ground" lookid="4515" z-order="3200">
  <item id="4515" chance="2500"/> ...
  <border align="outer" id="2"/>
  <border align="inner" to="none" id="1"/>
  <optional id="120"/>
</brush>
```

```js
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

export function loadBorders(materialsDir) {
  const xml = readFileSync(join(materialsDir, "borders", "borders.xml"), "utf8");
  const out = new Map();
  const borderRe = /<border id="(\d+)"[^>]*>([\s\S]*?)<\/border>/g;
  const itemRe = /<borderitem edge="(\w+)" item="(\d+)"\/>/g;
  let b;
  while ((b = borderRe.exec(xml)) !== null) {
    const edges = {};
    let it;
    while ((it = itemRe.exec(b[2])) !== null) edges[it[1]] = Number(it[2]);
    out.set(Number(b[1]), edges);
  }
  return out;
}

export function loadGroundBrushes(materialsDir) {
  const dir = join(materialsDir, "brushs");
  const out = new Map();
  for (const file of readdirSync(dir).filter(f => f.endsWith(".xml")).sort()) {
    const xml = readFileSync(join(dir, file), "utf8");
    const brushRe = /<brush name="([^"]+)" type="ground"[^>]*?lookid="(\d+)"[^>]*?z-order="(\d+)"[^>]*>([\s\S]*?)<\/brush>/g;
    let m;
    while ((m = brushRe.exec(xml)) !== null) {
      const body = m[4];
      const items = [...body.matchAll(/<item id="(\d+)"[^>]*\/>/g)].map(x => Number(x[1]));
      const borders = [...body.matchAll(/<border align="(\w+)"(?: to="([^"]+)")? id="(\d+)"\/>/g)]
        .map(x => ({ align: x[1], to: x[2] ?? null, id: Number(x[3]) }));
      out.set(m[1], { name: m[1], lookid: Number(m[2]), zOrder: Number(m[3]), items, borders });
    }
  }
  return out;
}
```

Atenção: a ordem/presença dos atributos (`lookid` antes de `z-order`?) pode variar — inspecionar os XMLs reais e ajustar as regex (atributos opcionais, qualquer ordem). Se um brush usar `server_lookid` em vez de `lookid`, aceitar ambos.

- [x] **Step 4: Rodar testes e ver passar:** `node --test test/rme.test.mjs` → 2 pass.
- [x] **Step 5: Commit**

```powershell
git add tools/map-importer/lib/rme.mjs tools/map-importer/test/rme.test.mjs tools/map-importer/config.json
git commit -m "feat(tools): RME materials XML parser (borders + ground brushes)"
```

---

### Task 2: Tradutor RME → tilesets (`lib/tilesets.mjs`)

**Model · Effort:** Fable 5 · high

**Files:**
- Create: `tools/map-importer/lib/tilesets.mjs`, `tools/map-importer/tilesets-config.json`
- Test: `tools/map-importer/test/tilesets.test.mjs`

**Interfaces:**
- Consumes: `loadBorders`/`loadGroundBrushes` (Task 1), `appearance-flags.json`.
- Produces: `buildTilesets(config)` → `{ tilesets, report }` onde `tilesets` é o objeto final do JSON (schema abaixo) e `report` lista gaps/sintéticos. **Schema do `tilesets.json`** (Tasks 3–5 dependem, verbatim):

```json
{
  "families": {
    "grass":    { "kind": "ground",   "items": [4515, 4516], "zOrder": 3200 },
    "mountain": { "kind": "mountain", "items": [1128], "zOrder": 9900 }
  },
  "borderSets": {
    "grass->none": { "n": 4445, "e": 4446, "s": 4447, "w": 4448, "cnw": 4449, "cne": 4450, "cse": 4451, "csw": 4452, "dnw": 4453, "dne": 4454, "dse": 4455, "dsw": 4456 },
    "mountain->OPEN": { "s": 873 }
  },
  "wallSets": {
    "mountain": { "16": 874, "4": 876, "20": 879, "0": 1128 }
  }
}
```

Convenções: `borderSets` usa os 12 edge names do RME, chave `"A->B"` = border da família A desenhado quando o vizinho é B (`"A->none"` = vizinho é qualquer terreno sem par específico; `"A->OPEN"` = outer border de montanha sobre chão aberto). `wallSets` usa o **mask blob canônico** (mesma numeração do `WallAutotile`: bit 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW de vizinho ABERTO) → item id; slot `"0"` (fechado) = tile de corpo da montanha.

- [ ] **Step 1: `tilesets-config.json`** — a curadoria de quais brushes viram famílias (nomes = os do RME):

```json
{
  "grounds": ["grass", "earth", "cave", "stone floor", "mossy floor"],
  "mountains": ["mountain", "earth mountain", "mossy wall mountain"]
}
```

(Ajustar os nomes exatos conferindo `brushes.get(...)` — os biomas atuais precisam de: chão de caverna/terra t1, grama+terra t2, pedra com musgo t3, pedra escura t4–5, e 2–3 montanhas. Listar os brushes disponíveis com um `node -e` sobre `loadGroundBrushes` e escolher os equivalentes; anotar a escolha em comentário no próprio JSON via chave `"_notes"`.)

- [ ] **Step 2: Teste que falha (`test/tilesets.test.mjs`)**

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { buildTilesets } from "../lib/tilesets.mjs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("buildTilesets emits families, border sets and 47-slot wall sets", () => {
  const { tilesets, report } = buildTilesets(config);
  const grass = tilesets.families["grass"];
  assert.ok(grass && grass.items.length >= 10);
  assert.ok(tilesets.borderSets["grass->none"], "grass needs a ->none border set");
  const mountainWalls = tilesets.wallSets["mountain"];
  assert.ok(mountainWalls, "mountain wall set must exist");
  const filled = Object.keys(mountainWalls).length;
  assert.ok(filled >= 40, `expected >=40/47 blob cases, got ${filled} (synthetics count)`);
  assert.ok(report.synthetic.length < 20, "too many synthetic wall slots");
});
```

Rodar → FAIL.

- [ ] **Step 3: Implementar a tradução.** Núcleo de `lib/tilesets.mjs`:

1. **Famílias:** cada brush listado no `tilesets-config.json` vira `families[name] = { kind, items, zOrder }` (`kind: "mountain"` para os de montanha).
2. **Border sets de chão:** para cada ground brush A, para cada `<border>` dele: `align === "outer"` sem `to` → `"A->none"`; com `to: "B"` → `"A->B"`. (Ignorar `align === "inner"` de grounds nesta fatia — o RME usa inner para transições invertidas; o painter v1 só consome outer + `->none`.) Copiar os 12 edges do border set referenciado.
3. **Wall sets de montanha:** os `align === "inner"` da montanha são as faces de rocha DESENHADAS NO TILE DA MONTANHA. Traduzir edge → mask blob de vizinhança aberta:

```js
// RME inner edge -> canonical blob mask of OPEN neighbours (bit 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW)
const EDGE_TO_MASK = {
  n: 1, e: 4, s: 16, w: 64,               // one open side
  cnw: 1 | 64, cne: 1 | 4, cse: 4 | 16, csw: 16 | 64, // two open sides (corner)
  dnw: 128, dne: 2, dse: 8, dsw: 32,      // diagonal-only open
};
```

   Depois preencher os 47 casos canônicos: os que têm tradução direta usam o item do edge; slot `"0"` = primeiro item do brush (corpo); os restantes caem no **fallback por distância de Hamming** pro mask preenchido mais próximo (desempate: menor mask), registrados em `report.synthetic`.
4. **Outer de montanha:** `align === "outer"` da montanha → `"<family>->OPEN"` em `borderSets` (border desenhado no chão aberto vizinho — dá a sombra/transição ao pé da rocha).
5. `report`: famílias com contagem de items, sets emitidos, `synthetic[]`, edges ausentes.

A lista canônica dos 47 masks: gerar programaticamente — todos os 256 masks passados por `Canonical` (mesma regra do C#: diagonal só sobrevive se as duas edges adjacentes estão abertas), dedup:

```js
export function canonical(mask) {
  const n = mask & 1, e = mask & 4, s = mask & 16, w = mask & 64;
  if (!(n && e)) mask &= ~2;
  if (!(s && e)) mask &= ~8;
  if (!(s && w)) mask &= ~32;
  if (!(n && w)) mask &= ~128;
  return mask;
}
export const CANONICAL_MASKS = [...new Set(Array.from({ length: 256 }, (_, m) => canonical(m)))].sort((a, b) => a - b);
```

- [ ] **Step 4: Testes passam:** `node --test test/tilesets.test.mjs` → PASS.
- [ ] **Step 5: Commit**

```powershell
git add tools/map-importer/lib/tilesets.mjs tools/map-importer/tilesets-config.json tools/map-importer/test/tilesets.test.mjs
git commit -m "feat(tools): translate RME brushes into blob wall sets + border sets"
```

---

### Task 3: Gate de predição contra o mapa real + CLI `convert-tilesets.mjs`

**Model · Effort:** Fable 5 · high

**Files:**
- Create: `tools/map-importer/convert-tilesets.mjs`
- Test: `tools/map-importer/test/predict.test.mjs`
- Create (gerado): `backend/src/KaezanArenaFable.Api/Content/tilesets.json`

**Interfaces:**
- Consumes: `buildTilesets` (Task 2), `loadMap`/`cropTiles` (map-importer core), `appearance-flags.json`.
- Produces: `tilesets.json` commitado no caminho acima; CLI com `--report-only`.

- [ ] **Step 1: Teste de predição (o GATE desta entrega — `test/predict.test.mjs`).** Escolher 2 regiões do otservbr com montanha + grama/terra conhecidas (usar `crop.mjs` para achar; ex.: arredores de Thais z=7). Para cada célula da região:
  - célula **impassável de montanha** (item ∈ família): computar o mask blob de vizinhança aberta e conferir que o wall set traduzido prevê um item que **está presente na célula real** (a célula real tem o corpo + as faces como items);
  - célula **aberta na costura** (vizinho de família ≠): conferir que os border ids previstos pelo border set estão entre os items reais da célula.

```js
test("translated tilesets predict the real map (>=95%)", () => {
  // load map, crop region, walk cells, compare predicted vs actual item ids
  // assert hits / total >= 0.95 with a per-mask miss report on failure
});
```

(Implementar de verdade — o esqueleto acima é o contrato; o corpo usa `loadMap`, a região fixa e imprime os misses agrupados por mask no `assert` message.) **Se a acurácia ficar < 95%, PARE: a convenção de edge→mask está errada (provável inversão N/S ou inner/outer) — corrija a tabela `EDGE_TO_MASK` antes de prosseguir.** Nada depois faz sentido com a tradução errada.

- [ ] **Step 2: Rodar e calibrar até passar:** `node --test test/predict.test.mjs` → PASS com ≥95%.
- [ ] **Step 3: CLI `convert-tilesets.mjs`:** roda `buildTilesets`, imprime o report (famílias, cobertura, sintéticos, ids sem sprite no manifest do frontend — mesmo check de gap do `export.mjs`); `--report-only` só imprime; sem `--report-only` escreve `backend/src/KaezanArenaFable.Api/Content/tilesets.json` (ordenado por chave, `JSON.stringify(_, null, 2)` — diffs legíveis).
- [ ] **Step 4: Rodar `node convert-tilesets.mjs --report-only`** e revisar o gap report (ids de sprite faltantes vão pra Task 4).
- [ ] **Step 5: Commit**

```powershell
git add tools/map-importer/convert-tilesets.mjs tools/map-importer/test/predict.test.mjs
git commit -m "feat(tools): tileset converter CLI gated by real-map prediction test"
```

---

### Task 4: Extração de sprites + `tilesets.json` commitado + width/height no flags

**Model · Effort:** Sonnet 5 · low

**Files:**
- Modify: `tools/AssetExtractor/Program.cs` (dump de flags ganha `w`/`h`), `tools/AssetExtractor/content-config.json`
- Create (gerado): `backend/src/KaezanArenaFable.Api/Content/tilesets.json`, sprites novos em `frontend/src/assets/`

- [ ] **Step 1: width/height no `--dump-flags`.** No `DumpFlags` (Program.cs, adicionado na fatia authored maps), acrescentar ao entry de cada appearance as dimensões do sprite em tiles (o extractor já conhece o tamanho ao renderizar — reusar a mesma fonte, ex.: `SpriteInfo` do frame group; conferir o nome real no código):

```csharp
entry["w"] = spriteTilesWide;  // 1 for 32px, 2 for 64px
entry["h"] = spriteTilesHigh;
```

Re-rodar: `dotnet run --project tools/AssetExtractor -- --dump-flags tools/map-importer/data/appearance-flags.json`

- [ ] **Step 2: Grupos semantic novos.** Copiar os ids do gap report da Task 3 para `content-config.json` em grupos `"wallset.<familia>"` e `"border.<par>"` (padrão dos grupos existentes). Re-rodar o extractor (fluxo do README raiz) e conferir os ids no manifest do frontend.
- [ ] **Step 3: `node convert-tilesets.mjs`** (sem `--report-only`) → escreve o `tilesets.json`. Conferir o arquivo (famílias esperadas, wall sets com 47 chaves).
- [ ] **Step 4: Commit**

```powershell
git add tools/AssetExtractor/Program.cs tools/AssetExtractor/content-config.json tools/map-importer/data/appearance-flags.json backend/src/KaezanArenaFable.Api/Content/tilesets.json frontend/src/assets
git commit -m "feat(content): tilesets.json from RME + sprite extraction + size flags"
```

---

## Entrega ② — Painter v2 (Tasks 5–10)

### Task 5: `TilesetRegistry` no backend (load + validação fail-fast)

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Content/TilesetRegistry.cs`
- Modify: `backend/src/KaezanArenaFable.Api/KaezanArenaFable.Api.csproj` (Content include se `tilesets.json` não for coberto pelo glob de prefabs), `backend/src/KaezanArenaFable.Api/Program.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/TilesetRegistryTests.cs`

**Interfaces:**
- Consumes: `tilesets.json` (Task 4).
- Produces (Tasks 6–9 dependem, verbatim):

```csharp
namespace KaezanArenaFable.Api.Content;

public sealed record TileFamily(string Name, string Kind, ushort[] Items, int ZOrder);
public sealed record BorderSet(IReadOnlyDictionary<string, ushort> Edges); // 12 RME edge names

public static class TilesetRegistry
{
    public static void LoadFrom(string path); // throws InvalidDataException; missing file = empty registry
    public static bool HasFamily(string name);
    public static TileFamily Family(string name);                  // throws KeyNotFoundException
    public static WallTileSet? WallSet(string family);             // null when family has no wall set
    public static BorderSet? Borders(string from, string to);      // exact pair, else "from->none"; null = no border
    public static IReadOnlyList<string> FamilyNames { get; }       // sorted ordinal (determinism)
}
```

Padrão do `PrefabRegistry`: parse `System.Text.Json` com DTO privado, validações → `InvalidDataException` com caminho+motivo: wall set com chave não-numérica ou fora dos 47 masks canônicos; família referenciada por borderSet inexistente (exceto sufixos `none`/`OPEN`); items vazios. Sem `var`.

- [ ] **Step 1: Teste que falha** (padrão `PrefabRegistryTests`: escrever JSON temporário, `LoadFrom`, asserts de família/border/wallset + 2 casos inválidos com `Assert.Throws<InvalidDataException>`).
- [ ] **Step 2: Implementar; wiring no `Program.cs`** logo antes do `PrefabRegistry.LoadFrom` existente:

```csharp
TilesetRegistry.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json"));
```

E no `.csproj`, garantir copy: `<Content Include="Content\tilesets.json" CopyToOutputDirectory="PreserveNewest" />`

- [ ] **Step 3: Testes + build:** `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter TilesetRegistry` → PASS; `dotnet build backend/src/KaezanArenaFable.Api` limpo.
- [ ] **Step 4: Commit** (`feat(content): TilesetRegistry with fail-fast validation`)

---

### Task 6: `BiomeDef` v2 — famílias nomeadas + reseed do ContentStore

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs`, `backend/src/KaezanArenaFable.Api/Content/ContentStore.cs` (ShouldSeedBiomes)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/BiomesTests.cs` (novo)

**Interfaces:**
- Produces (Tasks 7–9, 11 dependem): `BiomeDef` ganha, mantendo TODOS os campos atuais:

```csharp
public sealed record BiomeDef(
    ushort[] Ground, ushort[] BossGround, ushort Bedrock,
    ushort WallH, ushort WallV, ushort WallPole, ushort WallCorner,
    ushort[] Decor, double DecorChance,
    ushort[] Accent, double AccentChance,
    BiomeAtmosphere Atmosphere,
    WallTileSet? WallSet = null,
    string WallFamily = "",           // tilesets.json family; "" = legacy 4-piece fallback
    string[]? GroundFamilies = null); // 1..3 named families; null/empty = legacy Ground palette
```

E um resolver estático: `Biomes.Resolve(BiomeDef def)` → devolve o `def` com `WallSet` preenchido de `TilesetRegistry.WallSet(def.WallFamily)` quando `WallFamily != ""` (chamado pelo `RunFactory` e pelo preview — nunca no tick).

- [ ] **Step 1: Testes que falham:** (a) `Resolve` com `WallFamily` válida preenche `WallSet`; (b) `WallFamily` inexistente → `InvalidDataException`; (c) `Resolve` com `WallFamily == ""` devolve o def intacto (legado byte-idêntico).
- [ ] **Step 2: Implementar** os campos + `Resolve`. Atualizar os 5 defaults do `Biomes` com as famílias mineradas (ex.: Cave → `WallFamily: "mountain"`, `GroundFamilies: ["cave", "earth"]`; escolher pelo `tilesets.json` real — anotar os pares escolhidos em comentário). `Ground` legado permanece preenchido (fallback e compat de serialização).
- [ ] **Step 3: Reseed do ContentStore.** Em `ShouldSeedBiomes`, adicionar: `|| biomes.Any(b => string.IsNullOrEmpty(b.Def.WallFamily))` — um `biomes.json` antigo em disco força reseed dos defaults novos (edições antigas de admin são perdidas: aceitável, documentado no commit).
- [ ] **Step 4: `RunFactory.cs:66`** passa a resolver: `BiomeDef biome = Biomes.Resolve(content.Biome(tierDef.Tier) ?? Biomes.ForTier(tierDef.Tier));`
- [ ] **Step 5: Testes + build + commit** (`feat(engine): BiomeDef v2 with named tile families`)

---

### Task 7: Manchas coerentes de chão (Voronoi jitterado)

**Model · Effort:** Fable 5 · medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (PaintTiles), `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs` (novos casos)

**Interfaces:**
- Produces: `PaintTiles` interno ganha um array `familyOf` (índice de família por célula) que a Task 8 consome na passada de borders. Expor como retorno interno privado (`int[] PaintGround(...)`) — não muda assinatura pública.

- [ ] **Step 1: Constantes em `GameConfig.cs`:**

```csharp
// Map beauty (2026-07-07): coherent ground patches (jittered-Voronoi family regions)
public const int GroundPatchCellSize = 6;        // Voronoi grid cell in tiles
public const double GroundPrimaryFamilyBias = 0.55; // chance a patch uses families[0]
```

- [ ] **Step 2: Testes que falham:** com um `BiomeDef` de teste com `GroundFamilies = ["a","b"]` (registry fake via `TilesetRegistry.LoadFrom` de um JSON temp): (a) determinismo — mesmo seed → mesmo `Ground` array; (b) um floor 48×48 contém ids das 2 famílias; (c) toda célula aberta tem id ∈ união das famílias; (d) biome legado (`GroundFamilies` null) → byte-idêntico ao comportamento atual (comparar com hash pré-mudança de um seed fixo — capturar o hash ANTES de mexer e fixá-lo no teste).
- [ ] **Step 3: Implementar.** Em `PaintTiles`, antes do loop de células: se `biome.GroundFamilies is { Length: > 0 }`, montar a grade Voronoi — para cada célula da grade `(gx, gy)` em ordem fixa y,x: centro jitterado (`rng.Range` dentro da célula) + família (`rng.Chance(GroundPrimaryFamilyBias)` → família 0, senão `rng.Next` uniforme nas restantes; 1 família → sempre 0). Para cada célula aberta do mapa, família = a do centro mais próximo (distância euclidiana², desempate por índice de grade menor). Id da célula: `rng.Pick(family.Items)`. Boss hall continua `BossGround` (células de sala boss ficam fora do patch/border). Caminho legado (`GroundFamilies` null/vazio) **não consome nenhum draw novo** (guard antes de qualquer `rng.*` novo — mesmo padrão LM-08).
- [ ] **Step 4: Testes passam + build + commit** (`feat(engine): coherent ground patches via jittered voronoi`)

---

### Task 8: Camada de border (2 slots) + passada de borders

**Model · Effort:** Fable 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (DungeonFloor + PaintTiles + StampVisuals), `backend/src/KaezanArenaFable.Api/Engine/GameDtos.cs` (MapDto)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Produces: `DungeonFloor` ganha `public required ushort[] BorderA; public required ushort[] BorderB;` (0 = none). `MapDto` ganha `ushort[] BorderA, ushort[] BorderB` (após `Decor`). Task 10 (renderer) e Task 11 (preview) consomem.

- [ ] **Step 1: Testes que falham:** com 2 famílias fake com border set `"a->b"` completo (12 edges): (a) célula de família A com vizinho N de família B → `BorderA` = item do edge `"n"`; (b) A com vizinhos N e W de B → edge `"cnw"` (canto côncavo, 1 item só); (c) A com vizinho N de B e diagonal SE de B → 2 slots preenchidos (`"n"` + `"dse"`); (d) célula interna (vizinhança toda A) → 0/0; (e) determinismo (mesmo seed → mesmos arrays); (f) mapa legado (sem famílias) → arrays zerados.
- [ ] **Step 2: Implementar a resolução de edges.** Nova passada no fim do `PaintTiles` (depois de decor), usando o `familyOf` da Task 7. Para cada célula aberta de família A (fora de sala boss e fora de rect de prefab): para cada família B ≠ A presente na vizinhança-8 **com zOrder maior que A** (o terreno "de cima" borda sobre o de baixo — regra RME) e com `Borders(B, A) ?? Borders(B, "none")` não-nulo... **atenção à direção**: no RME o brush B de zOrder maior desenha o SEU border (outer) sobre o tile do A. Ou seja: célula de A recebe pieces do set de B, com edges relativos a ONDE está o B:

```csharp
// pieces resolved for ONE neighbouring family B around an open cell (bit i set = B at that neighbour)
private static void ResolveBorderPieces(int maskOfB, BorderSet set, List<ushort> outPieces)
{
    bool n = (maskOfB & 1) != 0, e = (maskOfB & 4) != 0, s = (maskOfB & 16) != 0, w = (maskOfB & 64) != 0;
    // concave corners swallow their two edges; remaining edges emit; then lone diagonals
    if (n && w) Add(set, "cnw", outPieces); if (n && e) Add(set, "cne", outPieces);
    if (s && w) Add(set, "csw", outPieces); if (s && e) Add(set, "cse", outPieces);
    if (n && !w && !e) Add(set, "n", outPieces);
    if (s && !w && !e) Add(set, "s", outPieces);
    if (w && !n && !s) Add(set, "w", outPieces);
    if (e && !n && !s) Add(set, "e", outPieces);
    if (!n && !w && (maskOfB & 128) != 0) Add(set, "dnw", outPieces);
    if (!n && !e && (maskOfB & 2) != 0) Add(set, "dne", outPieces);
    if (!s && !e && (maskOfB & 8) != 0) Add(set, "dse", outPieces);
    if (!s && !w && (maskOfB & 32) != 0) Add(set, "dsw", outPieces);
}
```

(`Add` ignora edge ausente no set.) Famílias B iteradas em ordem de zOrder desc, depois nome ordinal (determinismo); os 2 primeiros pieces vencem (`BorderA`, `BorderB`). Células bloqueadas de montanha adjacentes contam como família da montanha (o `"mountain->OPEN"` da Task 2 entra por aqui — é o pé-de-rocha). Nenhum draw de rng nesta passada (resolução pura).

- [ ] **Step 3: `StampVisuals` zera borders** no rect do prefab (aberto e bloqueado): `floor.BorderA[fi] = 0; floor.BorderB[fi] = 0;` — crops autorais carregam as próprias bordas como decor.
- [ ] **Step 4: `MapDto.FromFloor`** inclui os 2 arrays. `GenerateTrainingRoom` e `Generate` inicializam os arrays (required).
- [ ] **Step 5: Testes passam + build + commit** (`feat(engine): 2-slot ground border layer from RME border sets`)

---

### Task 9: Wall sets ligados + guarda de decor multi-tile + REBASELINE

**Model · Effort:** Fable 5 · medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Content/TilesetRegistry.cs` (guarda de decor), `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` (se ajuste de família), `tools/BalanceSim/Golden.cs`, `docs/balance/golden_dungeon.txt`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/TilesetRegistryTests.cs`

- [ ] **Step 1: Guarda de decor >1×1.** O `appearance-flags.json` agora tem `w`/`h` (Task 4). Copiá-lo (ou um extrato `Content/appearance-sizes.json` gerado pelo conversor) para o backend e, no `TilesetRegistry.LoadFrom` + num check estático `Biomes.ValidateDefaults()` chamado no `Program.cs`: toda palette de `Decor`/`Accent` dos defaults com id de `w>1||h>1` → `InvalidDataException` listando os ids. Corrigir as palettes dos defaults removendo os ids grandes (as pedras 64px atuais de `CaveRocks` — conferir os tamanhos reais no flags e substituir por variantes 1×1 da mesma família se existirem).
- [ ] **Step 2: Golden.** `Golden.Compute` já carrega prefabs; adicionar `TilesetRegistry.LoadFrom(<repo>/backend/src/KaezanArenaFable.Api/Content/tilesets.json)` e resolver o biome com `Biomes.Resolve(Biomes.ForTier(tier))` (espelha o RunFactory). Incluir `BorderA`/`BorderB` na string hasheada do floor.
- [ ] **Step 3: Testes + build completos:** `dotnet test backend/tests/KaezanArenaFable.Api.Tests` → tudo PASS.
- [ ] **Step 4: REBASELINE deliberado (o único da fatia):**

```powershell
dotnet run --project tools/BalanceSim -- --golden
dotnet run --project tools/BalanceSim -- --golden-check
```

Inspecionar o diff do baseline (TODOS os floors mudam — esperado: painter novo). Regravar a bateria de replays (apagar `.replay.json.gz` antigos, rodar o BalanceSim normal, depois):

```powershell
dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays
```

Esperado: 0 divergências.

- [ ] **Step 5: Commit** (`feat(engine): wire mined wall sets + ban multi-tile decor (deliberate golden rebaseline)` — inclui `golden_dungeon.txt` e replays novos)

---

### Task 10: Renderer — camadas de border + types

**Model · Effort:** GPT-5.5 (Codex) · low

**Files:**
- Modify: `frontend/src/app/core/types.ts` (MapDto), `frontend/src/app/core/renderer.ts`

- [ ] **Step 1:** `types.ts`: `MapDto` ganha `borderA: number[]; borderB: number[];` (espelhar casing dos campos existentes do JSON — conferir como `ground` chega hoje).
- [ ] **Step 2:** `renderer.ts` (~linha 795): desenhar na ordem ground → borderA → borderB → decor (walls continuam na passada própria ~915):

```ts
const ground = map.ground[i];
if (ground) this.assets.drawObject(ctx, ground, sx(x), sy(y), SCALE, x, y, nowPerf);
const borderA = map.borderA[i];
if (borderA) this.assets.drawObject(ctx, borderA, sx(x), sy(y), SCALE, x, y, nowPerf);
const borderB = map.borderB[i];
if (borderB) this.assets.drawObject(ctx, borderB, sx(x), sy(y), SCALE, x, y, nowPerf);
const decor = map.decor[i];
if (decor) this.assets.drawObject(ctx, decor, sx(x), sy(y), SCALE, x, y, nowPerf);
```

- [ ] **Step 3:** `npx ng build` limpo. Verificação visual: backend Release (`tools/run-backend.ps1`) + frontend, run tier 2 — chão em manchas com borders, paredes contínuas. Screenshot fora de combate (freeze de rAF em combate — ver memória de verificação de HUD).
- [ ] **Step 4: Commit** (`feat(frontend): render ground border layers`)

---

## Entrega ③ — Map Lab (Tasks 11–14)

### Task 11: Endpoints admin (biomes GET/PUT · mapgen/preview · tilesets)

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Api/MetaEndpoints.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/MapLabEndpointTests.cs` (ou teste direto das funções de validação/preview extraídas)

**Interfaces:**
- Produces (Task 12 consome; grupo `admin` já existe em MetaEndpoints.cs:350):

```csharp
admin.MapGet("/content/biomes", (ContentStore content) => Results.Ok(content.Biomes));
admin.MapPut("/content/biomes", (List<BiomeRow> rows, ContentStore content) => ...);
// validation: 5 rows tiers 1..5; every WallFamily/GroundFamilies exists in TilesetRegistry;
// DecorChance/AccentChance in [0, 0.2]; invalid -> Results.BadRequest(new { error })
admin.MapPost("/mapgen/preview", (MapPreviewRequest req, ContentStore content) => ...);
admin.MapGet("/tilesets", () => ...); // families + set names + coverage (synthetic slots)

public sealed record MapPreviewRequest(
    int Tier, long Seed, int FloorIndex, bool BossFloor, BiomeDef? Biome);
```

Preview: `BiomeDef resolved = Biomes.Resolve(req.Biome ?? content.Biome(req.Tier) ?? Biomes.ForTier(req.Tier)); Rng rng = new Rng((ulong)req.Seed); DungeonFloor floor = DungeonGenerator.Generate(rng, req.FloorIndex, req.BossFloor, resolved, PrefabRegistry.ForTier(req.Tier)); return Results.Ok(MapDto.FromFloor(floor, resolved.Atmosphere, req.FloorIndex, []));` — nenhuma run criada; POIs vazios (o preview mostra chests/sanctuaries pelos `Rooms`, suficiente pra v1).

- [ ] **Step 1: Testes que falham** (determinismo do preview: mesmo request → DTOs iguais campo a campo; PUT inválido → erro com família inexistente).
- [ ] **Step 2: Implementar os 4 endpoints** (padrão dos admin existentes — MetaEndpoints.cs:424 `content/tiers` é o gêmeo do GET/PUT).
- [ ] **Step 3: Testes + build + commit** (`feat(api): Map Lab admin endpoints (biomes CRUD + mapgen preview)`)

---

### Task 12: Map Lab — aba admin com canvas de preview

**Model · Effort:** GPT-5.5 (Codex) · medium

**Files:**
- Create: `frontend/src/app/pages/admin/map-lab.ts`
- Modify: `frontend/src/app/pages/admin/admin.ts` (tab), `frontend/src/app/core/api.service.ts`, `frontend/src/app/core/types.ts`

**Interfaces:**
- Consumes: endpoints da Task 11; `MapDto` (types.ts, com borders da Task 10); `AssetsService.drawObject(ctx, id, px, py, scale, x, y, now)`.
- Produces: componente `<app-map-lab />`; métodos `api.adminBiomes()`, `api.adminSaveBiomes(rows)`, `api.adminMapPreview(req)`, `api.adminTilesets()` (padrão dos métodos admin existentes no api.service.ts).

- [ ] **Step 1:** Tab nova em `admin.ts`: `AdminMode` ganha `'maplab'`; botão "Map Lab" nas page-tabs; `@else if (pageMode() === 'maplab') { <app-map-lab /> }`.
- [ ] **Step 2:** `map-lab.ts` (standalone, template inline, signals — padrão `role-tuning-editor.ts`): controles tier (1–5), seed (number input + botão Reroll = seed aleatório client-side, só UX), floor (Normal/Boss), botão Generate. Ao gerar: `adminMapPreview` → desenhar o `MapDto` num `<canvas>`: para cada célula, ground → borderA → borderB → decor → wall via `AssetsService.drawObject` (pinta 1×, sem rAF loop; `now = 0`). Antes de desenhar, aguardar o carregamento de assets do jeito que `game.ts` faz (inspecionar `assets.service.ts` para a API de ready/load e reusar). Zoom simples: fator 1×/2× (re-render). Overlay checkbox: blocked (retângulo semi-transparente vermelho), rooms (contorno + label do role; prefab room com contorno distinto — `RoomDto` já tem `role`).
- [ ] **Step 3:** `npx ng build` limpo; verificação manual: tier 2, dois seeds, boss floor, overlays.
- [ ] **Step 4: Commit** (`feat(admin): Map Lab tab with seeded floor preview canvas`)

---

### Task 13: Map Lab — editor de preset com Preview/Save

**Model · Effort:** GPT-5.5 (Codex) · medium

**Files:**
- Modify: `frontend/src/app/pages/admin/map-lab.ts`

**Interfaces:**
- Consumes: `api.adminBiomes()` / `adminSaveBiomes` / `adminTilesets` (Task 12), `MapPreviewRequest.Biome` (Task 11).

- [ ] **Step 1:** Coluna direita do Map Lab: formulário do `BiomeRow` do tier selecionado — `WallFamily` (select das famílias `kind === "mountain"` do `adminTilesets`), `GroundFamilies` (multi-select ordenado, 1–3, das famílias `ground`), `DecorChance`/`AccentChance` (range sliders 0–0.2), palettes `Decor`/`Accent` (chips de id com thumbnail — reusar o padrão de thumbnail de item do `item-editor.ts`; sem picker novo nesta fatia: input de id + add), atmosfera (inputs numéricos/color — dado puro).
- [ ] **Step 2:** Botões: **Preview draft** (envia o `BiomeDef` editado inline no `MapPreviewRequest.Biome`, mesmo seed — NÃO salva), **Save** (PUT completo dos 5 rows com o row editado substituído; mostrar erro de validação do backend), **Reset row** (recarrega do GET).
- [ ] **Step 3:** `npx ng build` limpo; verificação manual: editar densidade de decor → Preview draft muda com mesmo seed → Save → GET devolve o salvo → iniciar uma run real do tier e confirmar o preset aplicado.
- [ ] **Step 4: Commit** (`feat(admin): Map Lab biome preset editor with draft preview`)

---

### Task 14: Verificação fim-a-fim, docs, roadmap e push

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `README.md` (seção "Map beauty & Map Lab" curta: fluxo RME→convert→extract, onde ficam os JSONs, o que o Map Lab faz), `docs/roadmap/ongoing/roadmap_dungeons.md` (marcar LM-09→LM-12 `[x]` com 1 linha cada; anotar em LM-13 o que o convert já cobre), este arquivo (checkboxes)

- [ ] **Step 1: Builds completos:** `dotnet build backend/src/KaezanArenaFable.Api` + `cd frontend; npx ng build` — ambos limpos; `dotnet test backend/tests/KaezanArenaFable.Api.Tests` e `node --test test/` (map-importer) — tudo PASS.
- [ ] **Step 2: Verificação visual (aceite da fatia):** backend Release + frontend: (a) Map Lab tier 2 seed fixo — chão em manchas com borders, paredes contínuas, zero pedra cortada; **screenshot antes/depois** vs. capturas de 2026-07-07; (b) fluxo editar→Preview draft→Save→run real aplicando o preset; (c) runs tiers 1 e 3–5 sem buraco de sprite/magenta (screenshot fora de combate).
- [ ] **Step 3: Docs + checkboxes:** README, roadmap_dungeons (LM-09→LM-12 `[x]`), e TODAS as checkboxes deste plano marcadas.
- [ ] **Step 4: Commit final + push**

```powershell
git add README.md docs/roadmap/ongoing/roadmap_dungeons.md docs/superpowers/plans/2026-07-07-map-beauty-and-map-lab.md
git commit -m "docs: map beauty + Map Lab delivered; roadmap LM-09..LM-12 closed"
git push origin main
```

---

## Ordem e dependências

```
T1 (RME parser) ─→ T2 (tradutor) ─→ T3 (gate predição + CLI) ─→ T4 (sprites + tilesets.json)
                                                                     │
T5 (TilesetRegistry) ←───────────────────────────────────────────────┘
   │
T6 (BiomeDef v2) ─→ T7 (manchas) ─→ T8 (borders) ─→ T9 (walls + decor + REBASELINE) ─→ T10 (renderer)
                                                                                          │
T11 (endpoints) ←─────────────────────────────────────────────────────────────────────────┘
   │
T12 (aba + canvas) ─→ T13 (editor de preset) ─→ T14 (verificação + docs + push)
```

Entregas commitáveis independentes: ① T1–T4 (dados prontos) · ② T5–T10 (o jogo fica bonito) · ③ T11–T14 (ferramenta). Se ③ atrasar, ① e ② já resolvem a motivação original.

## Riscos conhecidos

- **Convenção edge→mask invertida** (T3): o gate de predição pega; corrigir `EDGE_TO_MASK`/direção outer antes de prosseguir — nada depois faz sentido com a tradução errada.
- **Nomes de atributo/estrutura dos XMLs do RME** (T1): variações (`server_lookid`, ordem de atributos) — inspecionar os arquivos reais e ajustar as regex.
- **`biomes.json` antigo em disco** (T6): reseed forçado descarta edições de admin anteriores — deliberado e documentado.
- **Golden/replay**: um único rebaseline (T9). Replays gravados antes deixam de re-simular (esperado — regravar a bateria).
- **Masks de wall com múltiplas faces** (ridge de 1 célula, pilares): resolvidos por prioridade + Hamming; se lerem mal no preview, ajustar o slot no `tilesets.json` na mão (override manual legítimo desta fatia).
