# Map Beauty v2 — Audit + Root-Cause Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Ao terminar cada task, marque as checkboxes `[x]` dos steps e da task neste arquivo.**
> Nunca conclua sem atualizar os checkboxes.

**Spec:** `docs/superpowers/specs/2026-07-07-map-beauty-v2-audit-and-fixes-design.md` (aprovado 2026-07-07)

**Goal:** Zero artefatos objetivos nos mapas gerados (triângulos pretos, sprites cortados, terreno sem borda, estruturas ilegíveis) e costuras de terreno contínuas e orgânicas — mapas que pareçam feitos no Remere's.

**Architecture:** Task 1 audita os 5 tiers no Map Lab e fecha a causa raiz de cada classe de artefato num doc. As tasks seguintes consertam por causa: acentos de terreno (lava) viram família com borda via o `BorderAutotile` existente; manchas Voronoi ganham passe de suavização determinístico + teste de continuidade de costura; gaps de sprite e sprites cortados são fechados no extractor/conversor; estruturas autorais são consertadas no render e re-curadas; famílias de wall re-escolhidas onde a leitura é ruim. UM rebaseline deliberado de golden + replays no fim.

**Tech Stack:** C# .NET 8 (xunit), Node 20 ESM (`node --test`), Angular 21 standalone + signals, BalanceSim (golden/replay), Map Lab (aba admin já entregue).

## Global Constraints

- **Determinismo do engine:** dentro da geração só o `Rng` da run em ordem de varredura fixa; passes novos de resolução (smoothing, borders) são 100% rng-free. Proibido `Random`, `DateTime.Now`, `Guid.NewGuid()`, iteração sem ordem estável.
- **Constantes de simulação** só em `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`.
- **C# novo/modificado sem `var`** — tipos explícitos sempre (legado com `var` fica como está).
- **Código e strings de display em inglês** (comentários inclusive); docs em `docs/**` podem ser PT.
- **Fontes externas nunca commitadas:** checkout do RME (`C:/Kaezan/kaezan/remeres-map-editor`) e `otservbr.otbm` ficam fora do repo; caminhos só em `tools/map-importer/config.json`.
- **Golden é rebaseline deliberado:** UM rebaseline nesta fatia (Task 7). O caminho legado (biomas sem famílias) permanece byte-idêntico por construção; os floors com famílias mudam — esperado.
- **Ao concluir cada task:** `dotnet build backend/src/KaezanArenaFable.Api` limpo e/ou `npx ng build` limpo (em `frontend/`); commits pequenos direto na `main`, stage seletivo; checkboxes marcadas neste arquivo.
- Backend de verificação visual: `tools/run-backend.ps1` (Release, porta 5210); frontend `npx ng serve`. Screenshot fora de combate (o Map Lab não tem combate — sem risco de freeze de rAF).

## Fatos já verificados (2026-07-07, não re-derivar)

- `Content/tilesets.json` atual: 11 famílias, border sets só genéricos (`A->none` / `A->OPEN`), 3 wall sets 47-slot (`mountain`, `mossy wall mountain`, `crystal wall`).
- **Os ground brushes do RME não têm borders par-a-par** (`to="B"`): cada um tem 1 outer genérico + 1 inner `to=none`. As transições suaves do RME vêm do outer genérico do brush de z-order maior desenhado no vizinho — exatamente o que `BorderAutotile` já faz. Portanto o problema de costura NÃO é "faltam pair borders"; é continuidade (formas de mancha, sprites faltantes, cap de 2 slots) — a Task 1 fecha qual.
- Histograma no otservbr (2 regiões wilderness 64×64, z=7): células com peças de border = {1: 724, 2: 24, 3: 14, 4: 6} — **≥3 peças é raro (0,24%)**; os 2 slots (`BorderA`/`BorderB`) bastam. Não adicionar terceiro slot (YAGNI).
- **Lava no T4/T5 é `Accent` palette pintada como decor** (`PaintClusters` em `DungeonGenerator.cs:1114`) — fora do sistema de famílias, por isso quadrados sem borda. O brush `lava` existe no RME: z-order 7700, 6 items, 1 border.
- Pares de família por tier todos têm z-order distinto (cave 200/dirt 400; grass 3200/dirt 400; mossy floor 3500/rocky ground 2000; dark dirt 1200/rocky ground 2000; dark dirt 1200/rock soil 5) — costuras devem existir; se alguma não aparece, é bug/sprite, não z-order.
- Prefabs autorais: curadoria em `tools/map-importer/prefabs-config.json`, export via `node export.mjs` → `backend/src/KaezanArenaFable.Api/Content/prefabs/*.json` (8 crops hoje; a "casa de tijolos" confusa está num deles — provavelmente `mintwallin-block` ou `orc-warlord-throne`; a Task 1 identifica).
- Capturas de referência do estado ruim (2026-07-07): Echoing Den, Scaled Lair ×2, Shadowed Crypt, Uruk Fort — artefatos visíveis: triângulos pretos e boulders cortados (Uruk Fort), lava sem borda e crystal wall "chapado" (Scaled Lair), casa ilegível (Scaled Lair urbano).

---

### [x] Task 1: Auditoria — causa raiz verificada por classe de artefato

> **Concluída 2026-07-07.** Doc: `docs/superpowers/specs/2026-07-07-map-beauty-v2-audit.md`. Descoberta-chave:
> renderer/sprites são fiéis (provado renderizando dados REAIS do Tibia) — todo artefato é de dados do
> gerador/preview, não de render. Correções à premissa: 0 gaps de manifest; guarda 1×1 só vale p/ borders
> de CHÃO (walls/`*->OPEN` são multi-tile legítimos); ilhas Voronoi raríssimas (1/15 mapas); costura nua
> real = cap de 2 slots vencido pelo foot-border de parede. 26 screenshots em `audit-shots/`.

**Model · Effort:** Fable 5 · high

**Files:**
- Create: `docs/superpowers/specs/2026-07-07-map-beauty-v2-audit.md`
- Create (descartável): screenshots em `docs/superpowers/specs/audit-shots/` (commitados — são a referência antes/depois da fatia)

**Interfaces:**
- Produces: o doc de auditoria com UMA seção por classe de artefato, cada uma com: evidência (screenshot + tier/seed), causa raiz **confirmada no código/dados** (não hipótese), e a task deste plano que a conserta (2–6). As Tasks 4, 5 e 6 leem este doc como entrada.

- [x] **Step 1: Gap report programático.** Em `tools/map-importer/`:

```powershell
node convert-tilesets.mjs --report-only
node export.mjs --report-only
```

Anotar TODOS os ids sem sprite no manifest (`frontend/public/assets/tibia/manifest.json`). Depois, cross-check dos ids que o backend usa fora do tilesets: escrever um script one-off (não commitado) que carrega `backend/src/KaezanArenaFable.Api/Content/tilesets.json`, os `Content/prefabs/*.json` e os defaults de `Domain/Biomes.cs` (palettes `Decor`/`Accent`/`Ground`/`BossGround` — copiar os arrays na mão se preciso) e lista: (a) ids ausentes do manifest; (b) ids com `w>1||h>1` no `Content/appearance-sizes.json`. Colar o resultado no doc de auditoria.

- [x] **Step 2: Screenshots sistemáticos no Map Lab.** Subir backend (`tools/run-backend.ps1`) + frontend (`npx ng serve` em `frontend/`). Na aba admin Map Lab: gerar **5 tiers × seeds {101, 202, 303}**, floor normal, zoom 2×; +1 boss floor por tier no seed 101. Screenshot de cada um em `docs/superpowers/specs/audit-shots/t<tier>-s<seed>[-boss].png` (20 arquivos). Usar as ferramentas de preview do harness; não pedir screenshot manual ao usuário.

- [x] **Step 3: Confirmar/refutar cada hipótese, no código.** Para cada classe, achar a instância no screenshot, identificar o item id envolvido (o preview desenha por id — inspecionar via o JSON do endpoint `POST /mapgen/preview` com o mesmo tier/seed e ler `ground/borderA/borderB/decor/wall` da célula), e rastrear a causa:
  - **Triângulos pretos (Uruk Fort):** o id da célula está no manifest? Se não → gap de extração (Task 4). Se sim, o sprite é 64px (`appearance-sizes.json`)? → corte de render (Task 4). Se 32px e presente → bug de draw order no renderer (Task 4, com repro mínimo anotado).
  - **Boulders/pedras cortadas:** de onde vem o id — palette `Decor` de bioma, decor de prefab, ou slot de wall set? (procurar o id nos três). Anotar a superfície que passou por fora da guarda 1×1 da fatia anterior (Task 4).
  - **Casa ilegível:** identificar o prefab (comparar screenshot com os 8 `Content/prefabs/*.json` — w/h e tema). O render bate com o Tibia real (paredes com topo, 64px inteiros)? Se não → defeito de render (Task 5); se sim mas continua confuso → re-curar o crop (Task 5).
  - **Costuras "tiles justapostos":** seguir 3 costuras longas por tier no zoom 2×; para cada quebra de continuidade anotar o padrão: (i) peça ausente (célula de costura com `borderA == 0`), (ii) mancha serrilhada de 1 célula (ruído Voronoi), (iii) peça errada (corner onde devia edge), (iv) peça dropada pelo cap de 2 slots (célula com 3+ famílias maiores na vizinhança). Contar ocorrências por padrão — a Task 3 prioriza pelo ranking.
  - **Crystal wall chapado (T4/T5):** confirmar que o wall set desenha corpo sem faces nos masks N/W-only (esperado — Tibia não tem face N/W) e avaliar se o problema é a falta do foot border (`crystal wall->OPEN` existe no tilesets.json — está sendo pintado? conferir `borderA` das células abertas ao pé da parede) ou a arte da família em si (Task 6).
- [x] **Step 4: Escrever o doc de auditoria** com o formato: `## <classe>` → Evidência / Causa raiz confirmada / Fix (task N) / Notas. Incluir o ranking de padrões de quebra de costura e a lista de ids com gap.
- [x] **Step 5: Commit**

```powershell
git add docs/superpowers/specs/2026-07-07-map-beauty-v2-audit.md docs/superpowers/specs/audit-shots
git commit -m "docs: map beauty v2 audit - verified root causes per artifact class"
```

---

### [ ] Task 2: Lava (e acentos de terreno) viram família com borda

**Model · Effort:** Fable 5 · high

**Files:**
- Modify: `tools/map-importer/tilesets-config.json`, `backend/src/KaezanArenaFable.Api/Content/tilesets.json` (regenerado), `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs`, `backend/src/KaezanArenaFable.Api/Content/ContentStore.cs` (ShouldSeedBiomes), `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`, `tools/map-importer/test/tilesets.test.mjs`

**Interfaces:**
- Consumes: `BorderAutotile.Paint(DungeonFloor, int[] familyOf, string[] groundFamilies, string wallFamily)` (existente — famílias extra entram como índices ao fim do array `groundFamilies`).
- Produces: `BiomeDef` ganha `string AccentFamily = ""` (após `GroundFamilies`); `familyOf` passa a poder conter o índice `GroundFamilies.Length` (célula de acento). Tasks 3 e 7 dependem.

- [ ] **Step 1: Conversor.** Adicionar `"lava"` a `grounds` em `tools/map-importer/tilesets-config.json`. Estender `test/tilesets.test.mjs` com:

```js
test("lava family is emitted with a border set", () => {
  const { tilesets } = buildTilesets(config);
  assert.ok(tilesets.families["lava"], "lava family must exist");
  assert.equal(tilesets.families["lava"].kind, "ground");
  assert.ok(tilesets.borderSets["lava->none"], "lava needs its generic outer border set");
});
```

Rodar `npm test` em `tools/map-importer/` → o teste novo FALHA; após editar o config, PASSA (o brush `lava` tem 1 outer genérico — se vier como `inner`, inspecionar o XML e ajustar a curadoria, não o parser). Rodar `node convert-tilesets.mjs` (sem `--report-only`; se reportar sprite gap dos items/borders de lava, adicionar os ids a um grupo `"ground.lava"` em `tools/AssetExtractor/content-config.json`, re-rodar o extractor conforme o README raiz, e então converter). Commit parcial:

```powershell
git add tools/map-importer/tilesets-config.json tools/map-importer/test/tilesets.test.mjs backend/src/KaezanArenaFable.Api/Content/tilesets.json
git commit -m "feat(content): lava ground family with border set"
```

(Se houve extração: incluir `tools/AssetExtractor/content-config.json` e `frontend/src/assets` no stage.)

- [ ] **Step 2: Teste C# que falha.** Em `DungeonGeneratorTests.cs` (a collection já carrega o `tilesets.json` real):

```csharp
[Fact]
public void AccentFamilyPaintsBorderedTerrainPatches()
{
    BiomeDef biome = Biomes.Resolve(Biomes.ForTier(4)); // T4 gains AccentFamily "lava" in this task
    Rng rng = new Rng(4242);
    DungeonFloor floor = DungeonGenerator.Generate(rng, 1, false, biome, PrefabRegistry.ForTier(4));

    ushort[] lavaItems = TilesetRegistry.Family("lava").Items;
    int size = floor.W;
    int lavaCells = 0;
    int borderedSeams = 0;
    int nakedSeams = 0;
    for (int i = 0; i < size * floor.H; i++)
    {
        if (floor.Blocked[i] || !lavaItems.Contains(floor.Ground[i])) continue;
        lavaCells++;
        int x = i % size, y = i / size;
        foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
        {
            int nx = x + dx, ny = y + dy;
            if (!floor.InBounds(nx, ny) || floor.Blocked[ny * size + nx]) continue;
            int ni = ny * size + nx;
            if (lavaItems.Contains(floor.Ground[ni])) continue;
            // open non-lava 4-neighbour: lava (z 7700, highest) must border over it
            if (floor.BorderA[ni] != 0) borderedSeams++; else nakedSeams++;
        }
    }
    Assert.True(lavaCells > 0, "T4 floor must contain lava terrain patches");
    Assert.Equal(0, nakedSeams);
    Assert.True(borderedSeams > 0);
}
```

Rodar `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter AccentFamilyPaintsBorderedTerrainPatches` → FAIL (campo não existe).

- [ ] **Step 3: `BiomeDef` + defaults + reseed.** Em `Biomes.cs`: adicionar `string AccentFamily = ""` ao record (após `GroundFamilies`). T4 (Scaled Lair) e T5 (Echoing Abyss): `AccentFamily: "lava"` (manter `Accent`/`AccentChance` como estão — a chance continua dimensionando os clusters; a palette `Lava` legada fica para o caminho sem famílias). `Biomes.Resolve`: validar `AccentFamily` não-vazio contra `TilesetRegistry.HasFamily` → `InvalidDataException` se desconhecida. Em `ContentStore.ShouldSeedBiomes`: acrescentar `|| biomes.Any(b => b.Def.Tier is 4 or 5 && string.IsNullOrEmpty(b.Def.AccentFamily))` — **atenção:** conferir a forma real do row (se `BiomeRow` não expõe `Tier`, usar o critério mais simples `biomes.Any(b => string.IsNullOrEmpty(b.Def.WallFamily))`-style: qualquer row com `WallFamily` preenchido e `AccentFamily` vazio quando o default daquele tier tem acento). Reseed descarta edições de admin — aceitável, documentado no commit.
- [ ] **Step 4: Painter.** Em `DungeonGenerator.PaintTiles` (linha ~1104), substituir o loop de clusters:

```csharp
HashSet<(int X, int Y)> reserved = ReservedCells(floor);
bool accentAsTerrain = familyOf is not null && biome.AccentFamily.Length > 0;
foreach (Room room in floor.Rooms)
{
    if (accentAsTerrain)
        PaintAccentPatches(floor, room, rng, biome, familyOf!, reserved);
    else
        PaintClusters(floor, room, rng, biome.Accent, biome.AccentChance, GameConfig.AccentClusterRadius, reserved);
    PaintClusters(floor, room, rng, biome.Decor, biome.DecorChance, GameConfig.DecorClusterRadius, reserved);
}
```

E o método novo (espelha `PaintClusters`, mas escreve GROUND + família, não decor):

```csharp
/// <summary>
/// Terrain accents (e.g. lava pools): same clustered placement as PaintClusters, but the cells
/// join the family system — ground item from the accent family, familyOf index appended after
/// the ground families — so BorderAutotile gives the pools proper RME borders. Skips blocked,
/// boss-hall (familyOf < 0), reserved (POI) and decorated cells.
/// </summary>
private static void PaintAccentPatches(
    DungeonFloor floor, Room room, Rng rng, BiomeDef biome, int[] familyOf,
    HashSet<(int X, int Y)> reserved)
{
    if (biome.AccentChance <= 0) return;
    ushort[] items = TilesetRegistry.Family(biome.AccentFamily).Items;
    int accentIndex = biome.GroundFamilies!.Length;
    int size = floor.W;
    int clusters = (int)Math.Round(room.W * room.H * biome.AccentChance * GameConfig.DecorDensityScale);
    for (int c = 0; c < clusters; c++)
    {
        int cx = rng.Range(room.X, room.X + room.W - 1);
        int cy = rng.Range(room.Y, room.Y + room.H - 1);
        for (int dy = -GameConfig.AccentClusterRadius; dy <= GameConfig.AccentClusterRadius; dy++)
        {
            for (int dx = -GameConfig.AccentClusterRadius; dx <= GameConfig.AccentClusterRadius; dx++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (!room.Contains(x, y)) continue;
                int i = y * size + x;
                if (floor.Blocked[i] || familyOf[i] < 0 || floor.Decor[i] != 0 || reserved.Contains((x, y))) continue;
                int ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (ring > 0 && !rng.Chance(1.0 - ring * GameConfig.ClusterFalloff)) continue;
                floor.Ground[i] = rng.Pick(items);
                familyOf[i] = accentIndex;
            }
        }
    }
}
```

Passe de borders (fim de `PaintTiles`) passa a incluir a família de acento:

```csharp
if (familyOf is not null && biome.GroundFamilies is { Length: > 0 } familyNames)
{
    string[] borderFamilies = biome.AccentFamily.Length > 0
        ? [.. familyNames, biome.AccentFamily]
        : familyNames;
    BorderAutotile.Paint(floor, familyOf, borderFamilies, biome.WallFamily);
}
```

(`BorderAutotile` não muda: a família extra entra como índice `familyNames.Length`, z-order via registry — lava 7700 borda sobre tudo.)

- [ ] **Step 5: Testes + build.** `dotnet test backend/tests/KaezanArenaFable.Api.Tests` → tudo PASS (inclusive o teste do Step 2 e os testes legados de byte-identidade — o caminho sem famílias não consome draw novo). `dotnet build` limpo.
- [ ] **Step 6: Verificação visual rápida.** Map Lab T4 seed 101: poças de lava com borda em toda a volta (comparar com `audit-shots/t4-s101.png`). Screenshot novo ao lado do antigo no doc de auditoria (seção lava, "depois").
- [ ] **Step 7: Commit**

```powershell
git add backend/src/KaezanArenaFable.Api/Domain/Biomes.cs backend/src/KaezanArenaFable.Api/Content/ContentStore.cs backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs
git commit -m "feat(engine): terrain accents join the family system (bordered lava pools)"
```

---

### [ ] Task 3: Continuidade de costura — suavização de manchas + teste de seam

**Model · Effort:** Fable 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (AssignGroundPatches), `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Consumes: `familyOf` de `AssignGroundPatches` (Task 2 não muda a assinatura); ranking de padrões de quebra do doc de auditoria (Task 1).
- Produces: costuras contínuas — critério do spec: seguir qualquer costura no Map Lab sem achar quebra. Task 7 valida no aceite final.

- [ ] **Step 1: Constante nova em `GameConfig.cs`:**

```csharp
// Map beauty v2 (2026-07-07): majority-filter passes that erase 1-cell noise on the
// jittered-Voronoi family patches so terrain seams read as continuous organic lines.
public const int GroundPatchSmoothPasses = 2;
```

- [ ] **Step 2: Testes que falham.** Em `DungeonGeneratorTests.cs`:

```csharp
[Fact]
public void GroundPatchesHaveNoSingleCellIslands()
{
    BiomeDef biome = Biomes.Resolve(Biomes.ForTier(2));
    Rng rng = new Rng(101);
    DungeonFloor floor = DungeonGenerator.Generate(rng, 1, false, biome, PrefabRegistry.ForTier(2));
    // reconstruct family per cell from the ground item (families are disjoint id sets)
    ushort[][] items = biome.GroundFamilies!.Select(f => TilesetRegistry.Family(f).Items).ToArray();
    int size = floor.W;
    int islands = 0;
    for (int y = 0; y < floor.H; y++)
    {
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            if (floor.Blocked[i]) continue;
            int family = Array.FindIndex(items, set => set.Contains(floor.Ground[i]));
            if (family < 0) continue; // boss ground / accent / prefab cell
            int sameNeighbours = 0, openNeighbours = 0;
            foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                int nx = x + dx, ny = y + dy;
                if (!floor.InBounds(nx, ny) || floor.Blocked[ny * size + nx]) continue;
                int ni = ny * size + nx;
                int nf = Array.FindIndex(items, set => set.Contains(floor.Ground[ni]));
                if (nf < 0) continue;
                openNeighbours++;
                if (nf == family) sameNeighbours++;
            }
            if (openNeighbours >= 3 && sameNeighbours == 0) islands++;
        }
    }
    Assert.Equal(0, islands);
}

[Fact]
public void EverySeamCellCarriesABorderPiece()
{
    BiomeDef biome = Biomes.Resolve(Biomes.ForTier(2));
    Rng rng = new Rng(202);
    DungeonFloor floor = DungeonGenerator.Generate(rng, 1, false, biome, PrefabRegistry.ForTier(2));
    ushort[][] items = biome.GroundFamilies!.Select(f => TilesetRegistry.Family(f).Items).ToArray();
    int[] zOrders = biome.GroundFamilies!.Select(f => TilesetRegistry.Family(f).ZOrder).ToArray();
    int size = floor.W;
    int naked = 0;
    for (int y = 0; y < floor.H; y++)
    {
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            if (floor.Blocked[i]) continue;
            int family = Array.FindIndex(items, set => set.Contains(floor.Ground[i]));
            if (family < 0) continue;
            bool higherNeighbour = false;
            foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                int nx = x + dx, ny = y + dy;
                if (!floor.InBounds(nx, ny) || floor.Blocked[ny * size + nx]) continue;
                int nf = Array.FindIndex(items, set => set.Contains(floor.Ground[ny * size + nx]));
                if (nf >= 0 && nf != family && zOrders[nf] > zOrders[family]) higherNeighbour = true;
            }
            if (higherNeighbour && floor.BorderA[i] == 0) naked++;
        }
    }
    Assert.Equal(0, naked);
}
```

Nota: o segundo teste pode já passar (o `BorderAutotile` cobre 4-vizinhos edge-a-edge); ele vira o guardião de regressão. O primeiro DEVE falhar antes do smoothing (ruído Voronoi produz ilhas). Rodar com `--filter GroundPatches` e `--filter EverySeamCell` e registrar o estado.

- [ ] **Step 3: Implementar o smoothing.** No fim de `AssignGroundPatches` (antes do `return familyOf;`), passe de maioria com double-buffer (rng-free, ordem-independente):

```csharp
// Majority filter (map beauty v2): a cell surrounded by a foreign family flips to the local
// majority, erasing 1-cell islands and jagged peninsulas so seams read as continuous lines.
// Double-buffered and rng-free: deterministic regardless of scan order.
for (int pass = 0; pass < GameConfig.GroundPatchSmoothPasses; pass++)
{
    int[] next = (int[])familyOf.Clone();
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            if (familyOf[i] < 0) continue;
            Span<int> counts = stackalloc int[familyCount];
            counts.Clear();
            int openNeighbours = 0;
            foreach ((int dx, int dy) in NeighbourOffsets4)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int nf = familyOf[ny * size + nx];
                if (nf < 0) continue;
                openNeighbours++;
                counts[nf]++;
            }
            if (openNeighbours < 3 || counts[familyOf[i]] > 0) continue;
            int bestFamily = 0;
            for (int f = 1; f < familyCount; f++)
            {
                if (counts[f] > counts[bestFamily]) bestFamily = f;
            }
            next[i] = bestFamily;
        }
    }
    Array.Copy(next, familyOf, familyOf.Length);
}
```

Com o campo estático (junto dos offsets existentes da classe):

```csharp
private static readonly (int Dx, int Dy)[] NeighbourOffsets4 = [(0, -1), (1, 0), (0, 1), (-1, 0)];
```

Regra deliberadamente conservadora: só vira célula cuja família NÃO aparece em nenhum 4-vizinho aberto (ilha pura) — não mexe em penínsulas legítimas. Se o teste de ilhas ainda falhar com 2 passes, subir `GroundPatchSmoothPasses` para 3 (não mais — platôs grandes demais matam a variedade).

- [ ] **Step 4: Padrões restantes do ranking da auditoria.** Abrir o doc da Task 1, seção costuras. Regras de decisão:
  - padrão (i) *peça ausente* com id no manifest → é bug de resolução: reproduzir num teste unitário com o mask exato e corrigir `ResolvePieces`/`BorderAutotile` (anexar o caso ao teste `EverySeamCellCarriesABorderPiece`);
  - padrão (i) com id FORA do manifest → já é escopo da Task 4 (não duplicar aqui);
  - padrão (iii) *peça errada* → conferir `normalizeRmeEdges`/`RME_CORNER_DIAGONAL_SWAP` para o border set específico contra o otservbr (estender `test/predict.test.mjs` com uma região que contenha aquele par) e corrigir no conversor;
  - padrão (iv) *dropada pelo cap* → confirmado raro (0,24% no otservbr); só agir se a auditoria mostrar ocorrência visualmente gritante, e a ação é priorizar as peças da família de MAIOR z-order (já é a ordem atual) — documentar no doc e não mexer.
- [ ] **Step 5: Testes + build:** `dotnet test backend/tests/KaezanArenaFable.Api.Tests` → tudo PASS; `dotnet build` limpo. Se mexeu no conversor: `npm test` em `tools/map-importer` PASS e regenerar/commitar `tilesets.json`.
- [ ] **Step 6: Verificação visual.** Map Lab T2 e T4, seeds 101/202: seguir 3 costuras longas por mapa no zoom 2× — nenhuma quebra de continuidade (critério do spec). Screenshots "depois" no doc de auditoria.
- [ ] **Step 7: Commit**

```powershell
git add backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs
git commit -m "feat(engine): majority-filter patch smoothing + seam continuity guard tests"
```

(Incluir arquivos do conversor no stage se o Step 4 os tocou.)

---

### [ ] Task 4: Gaps de sprite + sprites cortados (triângulos pretos, boulders)

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `tools/AssetExtractor/content-config.json` (grupos novos), `frontend/src/assets` (sprites extraídos), e conforme a auditoria: `tools/map-importer/lib/tilesets.mjs` (validação 1×1), `backend/src/KaezanArenaFable.Api/Content/PrefabRegistry.cs` ou `frontend/src/app/core/renderer.ts`
- Test: `tools/map-importer/test/tilesets.test.mjs` (validação 1×1) e/ou xunit conforme a superfície

**Interfaces:**
- Consumes: doc de auditoria (Task 1) — lista de ids com gap e a causa confirmada de triângulos pretos e boulders cortados.

- [ ] **Step 1: Fechar TODOS os gaps de manifest.** Para cada id listado na auditoria (Step 1 dela): adicionar a um grupo semântico em `tools/AssetExtractor/content-config.json` (padrão dos grupos `wallset.*`/`border.*` existentes), re-rodar o extractor (fluxo do README raiz), conferir os ids no `frontend/public/assets/tibia/manifest.json`.
- [ ] **Step 2: Validação permanente de 1×1 no conversor.** Em `lib/tilesets.mjs`, ao emitir border sets e wall sets, validar cada item contra `config.flags` (appearance-flags com `w`/`h` da fatia anterior): peça com `w>1||h>1` → lançar `Error` com família/edge/id (border e wall pieces do RME são 1×1; um 64px aqui é curadoria errada). Teste em `tilesets.test.mjs`:

```js
test("every border and wall piece is 1x1", () => {
  const { tilesets } = buildTilesets(config);
  const flags = JSON.parse(readFileSync(new URL(config.flags, import.meta.url), "utf8"));
  const oversize = [];
  for (const [key, set] of Object.entries(tilesets.borderSets))
    for (const [edge, id] of Object.entries(set)) {
      const f = flags[id];
      if (f && ((f.w ?? 1) > 1 || (f.h ?? 1) > 1)) oversize.push(`${key}.${edge}=${id}`);
    }
  for (const [family, slots] of Object.entries(tilesets.wallSets))
    for (const [mask, id] of Object.entries(slots)) {
      const f = flags[id];
      if (f && ((f.w ?? 1) > 1 || (f.h ?? 1) > 1)) oversize.push(`wall.${family}[${mask}]=${id}`);
    }
  assert.deepEqual(oversize, []);
});
```

(Ajustar a leitura do flags ao formato real do arquivo — inspecionar `data/appearance-flags.json` antes; o caminho relativo pode precisar de `new URL(..., import.meta.url)` diferente.)

- [ ] **Step 3: Boulders cortados — fix pela causa confirmada na auditoria.** Regras de decisão:
  - id multi-tile em palette `Decor`/`Accent` de bioma → já é barrado por `Biomes.ValidateDefaults()`; se passou, o id entrou depois — remover da palette e trocar por variante 1×1 da mesma família;
  - id multi-tile em **decor de prefab** → estender a validação do `PrefabRegistry` (ou do `buildPrefab` no export) para rejeitar decor `w>1||h>1` fora de âncora correta, OU corrigir o renderer para desenhar 64px com offset correto (o `AssetsService.drawObject` sabe o tamanho?) — escolher o lado que a auditoria apontar como quebrado (dado errado → validar; render errado → renderizar);
  - id em slot de wall set → o Step 2 já barra; re-curar o slot no `tilesets-config.json`/override manual.
- [ ] **Step 4: Testes + builds:** `npm test` (map-importer) PASS; `dotnet test` PASS; `npx ng build` limpo se o renderer mudou. Regenerar e commitar `tilesets.json`/prefabs se a curadoria mudou.
- [ ] **Step 5: Verificação visual:** Map Lab T2 (Uruk Fort) seeds 101/202/303 — zero triângulos pretos, zero pedra cortada. Screenshots "depois" no doc de auditoria.
- [ ] **Step 6: Commit** (`fix(content): close sprite gaps + enforce 1x1 border/wall pieces`)

---

### [ ] Task 5: Estruturas legíveis — render de prefab + re-curadoria de crops

**Model · Effort:** Fable 5 · medium

**Files:**
- Modify conforme auditoria: `frontend/src/app/core/renderer.ts` (se defeito de render), `tools/map-importer/prefabs-config.json` + `backend/src/KaezanArenaFable.Api/Content/prefabs/*.json` (se re-curadoria)
- Test: caso de render mínimo em teste existente do frontend se houver harness; senão verificação visual documentada

**Interfaces:**
- Consumes: doc de auditoria (Task 1) — qual prefab é a "casa ilegível" e se o render bate com o Tibia real.

- [ ] **Step 1: Fix de render (se a auditoria confirmou defeito).** Comparar a célula problemática do prefab no jogo vs o mesmo trecho no Remere's/otservbr (o prefab JSON guarda os item ids — abrir o crop original com `node crop.mjs` na região da `prefabs-config.json`). Padrões prováveis: item de parede desenhado sem o item de topo que o acompanha na célula real (prefab só guarda 1 item por camada?), ou ordem de desenho errada entre `decor` e `wall` no renderer. Corrigir no lado confirmado (exporter guarda camadas insuficientes → `lib/prefab.mjs` + re-export; renderer desenha em ordem errada → `renderer.ts`).
- [ ] **Step 2: Re-curadoria.** Para cada crop que continuar ilegível com render correto: escolher crop substituto no otservbr (usar `node crop.mjs` para explorar; manter tema/tier do original — a `prefabs-config.json` documenta região e tema), atualizar a entry, `node export.mjs`, conferir `--report-only` sem gaps. **IDs de prefab são estáveis** (`prefab:*`): substituir o CONTEÚDO da entry mantendo o id.
- [ ] **Step 3: Builds + testes:** `npm test` (map-importer) PASS; `npx ng build` limpo se renderer mudou; `dotnet test` PASS (PrefabRegistry valida os JSONs novos no load).
- [ ] **Step 4: Verificação visual:** Map Lab nos tiers que usam os prefabs trocados (3 seeds) — estrutura legível à primeira vista (critério: dá pra dizer "é uma casa/forte/cripta" sem legenda). Screenshots "depois" no doc de auditoria.
- [ ] **Step 5: Commit** (`fix(content): legible authored structures (render fix + crop re-curation)`)

---

### [ ] Task 6: Curadoria de famílias — crystal wall e pares por tier

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` (defaults), possivelmente `tools/map-importer/tilesets-config.json` + `backend/src/KaezanArenaFable.Api/Content/tilesets.json` (se minerar família nova)
- Test: `backend/tests/KaezanArenaFable.Api.Tests` (os testes existentes de Resolve cobrem)

**Interfaces:**
- Consumes: doc de auditoria (Task 1) — veredito sobre o crystal wall (foot border não pintado vs arte ruim) e quebras de costura por curadoria.

- [ ] **Step 1: Crystal wall (T4/T5).** Pela auditoria:
  - se o foot border (`crystal wall->OPEN`) não está sendo pintado → é bug (voltar à Task 3/BorderAutotile com um teste do mask exato);
  - se está pintado mas a leitura continua "chapada" → trocar a família: candidatas no RME com inner border set (pré-requisito do wall autotiler) — listar com `node -e` sobre `loadGroundBrushes` filtrando brushes de montanha com `align === "inner"`. Testar as candidatas trocando `WallFamily` no editor do Map Lab (Preview draft, sem salvar) e escolher a de melhor leitura para lair/abyss. Se a escolhida não estiver no `tilesets.json`, adicionar a `mountains` no `tilesets-config.json`, reconverter, re-extrair sprites (fluxo da Task 2 Step 1).
- [ ] **Step 2: Pares de chão por tier.** No Map Lab, revisar os 5 tiers com os fixes das Tasks 2–5 aplicados: algum par de famílias ainda lê mal (contraste baixo demais, borda genérica feia entre elas)? Trocar as `GroundFamilies` do tier no editor (Preview draft) até a leitura ficar boa; consolidar a escolha final nos defaults de `Biomes.cs` (comentário com o porquê, padrão da fatia anterior). Reseed se necessário (mesmo mecanismo da Task 2 Step 3).
- [ ] **Step 3: Testes + build:** `dotnet test` PASS; `dotnet build` limpo.
- [ ] **Step 4: Verificação visual:** Map Lab 5 tiers seed 101 lado a lado com `audit-shots/` — leitura de parede e chão aprovada. Screenshots "depois" no doc de auditoria.
- [ ] **Step 5: Commit** (`feat(content): re-curated wall/ground families per tier`)

---

### [ ] Task 7: Rebaseline deliberado + aceite final + docs + push

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `docs/balance/golden_dungeon.txt` (rebaseline), `backend/src/KaezanArenaFable.Api/.data/replays/` (bateria regravada), `README.md` (se comportamento visível mudou), `docs/superpowers/specs/2026-07-07-map-beauty-v2-audit.md` (seção "Resultado"), este arquivo (checkboxes)

- [ ] **Step 1: Bateria completa de testes/builds:** `dotnet test backend/tests/KaezanArenaFable.Api.Tests` PASS; `npm test` em `tools/map-importer` PASS; `dotnet build backend/src/KaezanArenaFable.Api` e `npx ng build` (em `frontend/`) limpos.
- [ ] **Step 2: REBASELINE deliberado (o único da fatia):**

```powershell
dotnet run --project tools/BalanceSim -- --golden
dotnet run --project tools/BalanceSim -- --golden-check
```

Inspecionar o diff: floors de tiers com famílias mudam (smoothing + acentos + curadoria — esperado); floors legados NÃO mudam. Regravar a bateria de replays (apagar `.replay.json.gz` antigos, rodar o BalanceSim normal) e:

```powershell
dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays
```

Esperado: 0 divergências.

- [ ] **Step 3: Aceite final do spec (checklist, com screenshot de cada item no doc de auditoria):**
  - [ ] 5 tiers × seeds {101, 202, 303} no Map Lab: zero triângulo preto, zero sprite cortado, zero terreno sem borda;
  - [ ] seguir qualquer costura no zoom 2× sem quebra de continuidade;
  - [ ] estruturas autorais legíveis à primeira vista;
  - [ ] uma run real T2 e uma T4 (backend Release + frontend) confirmando que o jogo reflete o Map Lab.
- [ ] **Step 4: Docs.** Seção "Resultado" no doc de auditoria (antes/depois por classe); `README.md` só se comportamento visível mudou além do já documentado na fatia anterior; TODAS as checkboxes deste plano marcadas.
- [ ] **Step 5: Commit final + push**

```powershell
git add -A docs docs/balance backend/src/KaezanArenaFable.Api/.data/replays README.md
git commit -m "docs: map beauty v2 delivered (deliberate golden rebaseline + replay battery)"
git push origin main
```

(Stage seletivo se houver mudanças alheias no working tree — conferir `git status` antes do `-A`.)

---

## Ordem e dependências

```
T1 (auditoria) ─→ T2 (lava/acentos) ─→ T3 (continuidade)
      │                                      │
      ├──────────→ T4 (sprites) ←────────────┤   (T4 pode rodar em paralelo a T2/T3;
      ├──────────→ T5 (estruturas)           │    depende só da auditoria)
      │                                      │
      └──────────→ T6 (curadoria) ←──────────┘   (T6 por último entre os fixes: avalia
                                                  o visual COM os demais fixes aplicados)
T7 (rebaseline + aceite + push) depois de TODAS.
```

## Riscos conhecidos

- **Smoothing agressivo demais** (T3): a regra é conservadora (só ilhas puras); se ainda comer variedade, reduzir passes — nunca trocar por regra de maioria simples (produz platôs).
- **Reseed do `biomes.json`** (T2/T6): edições de admin anteriores são descartadas — deliberado, documentado nos commits.
- **Formato do appearance-flags** (T4): inspecionar o arquivo real antes de escrever a validação 1×1 (chaves podem ser string).
- **Prefab exporter com camadas insuficientes** (T5): se a causa da casa ilegível for o exporter guardar 1 item por camada, o fix mexe em formato de prefab → revalidar TODOS os 8 prefabs no `PrefabRegistry` e no visual.
- **Golden/replay:** um único rebaseline (T7). Replays gravados antes deixam de re-simular (esperado — bateria regravada).
