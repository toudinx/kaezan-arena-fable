# Onda 2 — Geração de Mapas v2 (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Matar o "quadradão raso" — macro-forma orgânica por lóbulos, pilares de cover, câmaras laterais, autotiling blob e profundidade visual barata — sem quebrar determinismo, com rebaseline do golden explícito e por último.

**Architecture:** Todas as mudanças de layout vivem em `DungeonGenerator` (Engine) e consomem apenas o `Rng` da run em ordem fixa de scan. O autotiling vira infraestrutura (`WallAutotile`) com fallback comportamentalmente idêntico ao atual (golden verde) e um slot de 47 casos que a Onda 4 preenche com tilesets autorais. A profundidade visual extra é 100% client-side (função pura de `map.blocked`), então nunca toca determinismo. Um validador BFS falha ruidosamente se qualquer floor sair desconectado.

**Tech Stack:** .NET 8 (xUnit em `backend/tests/KaezanArenaFable.Api.Tests`), Angular 21 + Vitest, BalanceSim (`--golden` / `--replay-check`).

**Pré-requisito:** Onda 1 concluída — este plano usa o projeto de testes `backend/tests/KaezanArenaFable.Api.Tests` criado na Task 2 da Onda 1 e o script `tools/run-backend.ps1`.

## Global Constraints

- **Determinismo:** dentro do gerador use apenas o `Rng` recebido, com ordem de draws fixa (loops em ordem y,x; nunca iterar coleção sem ordem estável). Proibido `Random`, `DateTime.Now`, `Guid.NewGuid()`.
- **Constantes novas SEMPRE em `Domain/GameConfig.cs`** — nenhum número mágico no gerador.
- **Golden quebra de propósito nas Tasks 1–3 e 5**: NÃO rode `--golden` (rebaseline) até a Task 6. Rodar `--golden-check` entre elas vai falhar — isso é esperado e documentado por task.
- **Task 4 (autotile) é behavior-preserving**: nela o `--golden-check` DEVE continuar como estava ao fim da Task 3 (a task não altera nenhum hash).
- **Replays FF-01:** mudança no gerador invalida replays salvos. Limpar e regravar é passo explícito da Task 6 — nunca antes.
- **IDs estáveis** (`ChestId=2472`, `SanctuaryId=2478`, `LadderDownId=386`, ids de tile dos biomas) não mudam.
- **Idioma:** código e comentários em inglês; docs em PT.
- Ao final de cada task: `dotnet build` (backend) limpo; tasks com frontend: `npx ng build` limpo.

## Modelos & quando usar

> Rubrica completa no plano da Onda 1 (`2026-07-06-wave1-performance-reliability.md`).
> Resumo: Codex executa código já dado; Opus integra o que toca gerador/golden; Fable só risco
> cross-cutting (nenhuma task desta onda precisa).

| Task | Modelo | Effort |
|---|---|---|
| 1 — Macro-forma por lóbulos | Opus 4.8 | medium |
| 2 — Pilares + anfiteatro | Opus 4.8 | medium |
| 3 — Pockets laterais | Opus 4.8 | medium |
| 4 — Autotile blob (behavior-preserving) | Codex | medium |
| 5 — Profundidade visual client-side | Codex | medium |
| 6 — Validador + rebaseline | Opus 4.8 | high |

**Desvio consciente do spec (item 2 da Onda 2):** o spec sugeria "reativar `RoomsFloorN > 1`" para câmaras laterais. Este plano NÃO reativa o caminho multi-room legado: ele esculpe bolsões (pockets) na rocha da arena única, conectados por gargantas orgânicas. Motivo: o gameplay atual de arena única (chest-drop por kills, teleport de saída no último mob, autopilot que orbita a pilha — ver `AssignSingleArena` e a direção de gameplay registrada em memória) depende de `Rooms.Count == 1`; reativar multi-room reintroduziria ladder/roles/navegação entre salas que foram removidos por feedback em 2026-06-29. Os pockets entregam o mesmo valor (câmaras laterais com risco/recompensa) sem tocar o fluxo de combate.

---

### Task 1: Macro-forma por lóbulos (substitui o noise uniforme da arena)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium — gerador + determinismo

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (método `ErodeArena`, ~linha 242)
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (~linha 451, bloco da arena)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs` (criar)

**Interfaces:**
- Consumes: `Rng` (`Range(min,max)` inclusivo, `NextDouble()`, `Chance(p)`), `DungeonFloor`, `Room`, `GameConfig`.
- Produces: `ErodeArena` com a mesma assinatura (`private static void ErodeArena(DungeonFloor, Room, Rng)`), agora seedado por lóbulos; helper `private static bool[] SeedArenaRock(int w, int h, Rng rng)`; helper compartilhado `private static void ApplyRockToFloor(DungeonFloor floor, Room room, bool[] rock)` (CA + core + flood-fill + write-back extraídos — a Task 2 reusa). Constantes `GameConfig.ArenaLobesMin/Max`, `ArenaLobeRadiusMinFrac/MaxFrac`, `ArenaLobeCore`, `ArenaEdgeNoiseProb`.

- [x] **Step 1: Escrever os testes que falham**

Criar `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`:

```csharp
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

public class DungeonGeneratorTests
{
    private static DungeonFloor Generate(long seed, bool boss = false)
    {
        var rng = new Rng((ulong)seed);
        return DungeonGenerator.Generate(rng, boss ? 1 : 0, isBossFloor: boss, Biomes.ForTier(1));
    }

    /// <summary>4-way flood from entry over open cells (mirrors nav connectivity).</summary>
    private static bool[] Flood(DungeonFloor f)
    {
        var live = new bool[f.W * f.H];
        var (ex, ey) = f.Entry;
        if (f.IsBlocked(ex, ey)) return live;
        var stack = new Stack<int>();
        live[ey * f.W + ex] = true;
        stack.Push(ey * f.W + ex);
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            int x = idx % f.W, y = idx / f.W;
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= f.W || ny < 0 || ny >= f.H) continue;
                var ni = ny * f.W + nx;
                if (live[ni] || f.Blocked[ni]) continue;
                live[ni] = true;
                stack.Push(ni);
            }
        }
        return live;
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(42L)]
    [InlineData(2654435761L)]
    public void generate_is_deterministic(long seed)
    {
        var a = Generate(seed);
        var b = Generate(seed);
        Assert.Equal(a.Blocked, b.Blocked);
        Assert.Equal(a.Ground, b.Ground);
        Assert.Equal(a.Wall, b.Wall);
        Assert.Equal(a.Entry, b.Entry);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(123L)]
    [InlineData(99999L)]
    public void arena_is_fully_connected_from_entry(long seed)
    {
        var f = Generate(seed);
        var live = Flood(f);
        for (var i = 0; i < f.Blocked.Length; i++)
            if (!f.Blocked[i])
                Assert.True(live[i], $"open cell {i % f.W},{i / f.W} unreachable from entry");
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(123L)]
    public void arena_outline_is_not_a_rectangle(long seed)
    {
        // The old uniform-noise arena kept most of the room rect open. With lobes the
        // corners of the room rect must be rock: sample the 4 corner 3x3 blocks.
        var f = Generate(seed);
        var room = f.Rooms[0];
        int RockIn3x3(int ox, int oy)
        {
            var rock = 0;
            for (var dy = 0; dy < 3; dy++)
                for (var dx = 0; dx < 3; dx++)
                    if (f.Blocked[(oy + dy) * f.W + ox + dx]) rock++;
            return rock;
        }
        var corners =
            RockIn3x3(room.X, room.Y) +
            RockIn3x3(room.X + room.W - 3, room.Y) +
            RockIn3x3(room.X, room.Y + room.H - 3) +
            RockIn3x3(room.X + room.W - 3, room.Y + room.H - 3);
        Assert.True(corners >= 18, $"expected mostly-rock corners, got {corners}/36 rock cells");
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(123L)]
    public void arena_open_fraction_is_playable(long seed)
    {
        var f = Generate(seed);
        var open = f.Blocked.Count(b => !b);
        var frac = open / (double)(f.W * f.H);
        Assert.InRange(frac, 0.25, 0.75);
    }
}
```

- [x] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter DungeonGeneratorTests`
Expected: `arena_outline_is_not_a_rectangle` FALHA (o noise uniforme atual deixa cantos abertos); os demais podem passar.

> **Nota (execução 2026-07-06):** os 4 métodos (13 casos) passaram já no gerador ANTIGO — o
> `arena_outline_is_not_a_rectangle` NÃO falhou como previsto. Motivo: o `ErodeArena` antigo já
> rodava a CA com out-of-bounds contando como rocha, o que já erodia os cantos do retângulo para
> rocha (≥18/36). O teste segue sendo um guard válido da propriedade desejada (a versão por lóbulos
> mantém verde), então segui em frente sem "quebrar de propósito" o gerador só para ver o vermelho.

- [x] **Step 3: Constantes novas em GameConfig**

Em `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`, substituir a constante `ArenaFillProb` (linha ~451) por este bloco (apagar `ArenaFillProb` — fica sem uso após o passo 4):

```csharp
    // --- Arena macro-shape (Wave 2): 2-4 overlapping elliptical lobes seed the open mass; the CA
    // then sculpts the coastline. Chokepoints/bays become a consequence of the shape, not noise. ---
    /// <summary>Minimum number of elliptical lobes unioned into the arena's open mass.</summary>
    public const int ArenaLobesMin = 2;
    /// <summary>Maximum number of elliptical lobes.</summary>
    public const int ArenaLobesMax = 4;
    /// <summary>Lobe semi-axis as a fraction of the room dimension — lower bound.</summary>
    public const double ArenaLobeRadiusMinFrac = 0.28;
    /// <summary>Lobe semi-axis as a fraction of the room dimension — upper bound.</summary>
    public const double ArenaLobeRadiusMaxFrac = 0.42;
    /// <summary>Normalized ellipse distance below which a cell is guaranteed open (lobe interior).</summary>
    public const double ArenaLobeCore = 0.55;
    /// <summary>Rock probability in the lobe rim band (between core and rim) — the noisy coastline
    /// the CA smooths into bays and headlands.</summary>
    public const double ArenaEdgeNoiseProb = 0.45;
```

- [x] **Step 4: Implementar seeding por lóbulos + extrair helper compartilhado**

Em `DungeonGenerator.cs`, substituir o corpo de `ErodeArena` e adicionar dois helpers logo abaixo dele:

```csharp
    /// <summary>
    /// Wave 2 macro-shape: instead of uniform per-cell noise (which reads as an eroded rectangle),
    /// the arena's open mass is the union of 2-4 deterministically-placed elliptical lobes. Lobe
    /// interiors are guaranteed open; the rim band gets noise the CA sculpts into a coastline, so
    /// chokepoints and bays are a consequence of the shape. Deterministic: lobes drawn first, then
    /// one Chance per rim cell in fixed y,x scan order.
    /// </summary>
    private static void ErodeArena(DungeonFloor floor, Room room, Rng rng)
    {
        var rock = SeedArenaRock(room.W, room.H, rng);
        ApplyRockToFloor(floor, room, rock);
    }

    private static bool[] SeedArenaRock(int w, int h, Rng rng)
    {
        var rock = new bool[w * h];
        Array.Fill(rock, true);

        var lobes = rng.Range(GameConfig.ArenaLobesMin, GameConfig.ArenaLobesMax);
        var ellipses = new (double cx, double cy, double rx, double ry)[lobes];
        for (var l = 0; l < lobes; l++)
        {
            // centres confined to the middle band so the lobes always overlap into one mass
            var cx = w * (0.30 + 0.40 * rng.NextDouble());
            var cy = h * (0.30 + 0.40 * rng.NextDouble());
            var span = GameConfig.ArenaLobeRadiusMaxFrac - GameConfig.ArenaLobeRadiusMinFrac;
            var rx = w * (GameConfig.ArenaLobeRadiusMinFrac + span * rng.NextDouble());
            var ry = h * (GameConfig.ArenaLobeRadiusMinFrac + span * rng.NextDouble());
            ellipses[l] = (cx, cy, rx, ry);
        }

        for (var ly = 0; ly < h; ly++)
            for (var lx = 0; lx < w; lx++)
            {
                // normalized squared ellipse distance to the NEAREST lobe: <core open, <1 noisy rim
                var d = double.MaxValue;
                foreach (var (cx, cy, rx, ry) in ellipses)
                {
                    var dx = (lx + 0.5 - cx) / rx;
                    var dy = (ly + 0.5 - cy) / ry;
                    d = Math.Min(d, dx * dx + dy * dy);
                }
                var i = ly * w + lx;
                if (d <= GameConfig.ArenaLobeCore) rock[i] = false;
                else if (d <= 1.0) rock[i] = rng.Chance(GameConfig.ArenaEdgeNoiseProb);
            }
        return rock;
    }

    /// <summary>
    /// Shared tail of the arena carvers: CA smoothing (4-5 rule, double-buffered), forced-open
    /// central core, flood-fill from the centre keeping only the connected component, then
    /// write-back into <see cref="DungeonFloor.Blocked"/>. Extracted from the original ErodeArena
    /// so the boss amphitheatre (Wave 2 Task 2) reuses it verbatim.
    /// </summary>
    private static void ApplyRockToFloor(DungeonFloor floor, Room room, bool[] rock)
    {
        int w = room.W, h = room.H;
        var next = new bool[w * h];
        for (var it = 0; it < GameConfig.OrganicCaIterations; it++)
        {
            for (var ly = 0; ly < h; ly++)
                for (var lx = 0; lx < w; lx++)
                {
                    var rocky = 0;
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = lx + dx, ny = ly + dy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h || rock[ny * w + nx]) rocky++;
                        }
                    var i = ly * w + lx;
                    next[i] = rocky >= GameConfig.OrganicWallThreshold ? true
                        : rocky <= GameConfig.OrganicFloorThreshold ? false
                        : rock[i];
                }
            (rock, next) = (next, rock);
        }

        // Open central core (Chebyshev disk): guarantees a broad stage before flood-fill.
        int cx = w / 2, cy = h / 2;
        var core = Math.Max(2, Math.Min(w, h) / 3);
        for (var dy = -core; dy <= core; dy++)
            for (var dx = -core; dx <= core; dx++)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx >= 0 && nx < w && ny >= 0 && ny < h && Math.Max(Math.Abs(dx), Math.Abs(dy)) <= core)
                    rock[ny * w + nx] = false;
            }

        var reached = new bool[w * h];
        var stack = new Stack<int>();
        stack.Push(cy * w + cx);
        reached[cy * w + cx] = true;
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            int lx = idx % w, ly = idx / w;
            Span<(int dx, int dy)> steps = [(-1, 0), (1, 0), (0, -1), (0, 1)];
            foreach (var (dx, dy) in steps)
            {
                int nx = lx + dx, ny = ly + dy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                var ni = ny * w + nx;
                if (reached[ni] || rock[ni]) continue;
                reached[ni] = true;
                stack.Push(ni);
            }
        }

        var size = floor.W;
        for (var ly = 0; ly < h; ly++)
            for (var lx = 0; lx < w; lx++)
                floor.Blocked[(room.Y + ly) * size + (room.X + lx)] = !reached[ly * w + lx];
    }
```

(O corpo antigo de `ErodeArena` — noise uniforme + CA + core + flood — é removido; CA/core/flood viram `ApplyRockToFloor`.)

- [x] **Step 5: Rodar os testes e ver passar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter DungeonGeneratorTests`
Expected: PASS (4 test methods, todas as seeds). ✅ 13/13 (e 28/28 no projeto inteiro).

Nota (revisão 2026-07-06): `arena_open_fraction_is_playable` mede a fração sobre o floor
INTEIRO (que inclui a banda de margem fora da room), então seeds azaradas podem encostar no
limite inferior de 0.25. Se falhar por margem pequena, ajuste o range do TESTE conscientemente
(ex.: 0.20) e registre no commit — nunca "conserte" mexendo no gerador para passar o teste.

- [x] **Step 6: Build + commit**

```bash
dotnet build backend/src/KaezanArenaFable.Api
git add backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs
git commit -m "feat(mapgen): lobe-based macro-shape for the single arena"
```

Nota: `--golden-check` agora FALHA — esperado; rebaseline só na Task 6.

---

### Task 2: Pilares de cover + anfiteatro do boss

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (método `Generate`, dispatch da arena; novos métodos)
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Consumes: `ApplyRockToFloor(floor, room, rock)` da Task 1; `Rng`; `GameConfig`.
- Produces: `private static void PlacePillars(DungeonFloor, Room, Rng)`; `private static void CarveAmphitheater(DungeonFloor, Room, Rng)`. Constantes `PillarDensity`, `PillarLargeChance`, `PillarPlacementAttemptsFactor`, `PillarCoreExclusion`, `AmphitheaterRimNoiseBand`, `AmphitheaterRimNoiseProb`.

- [x] **Step 1: Testes que falham**

Adicionar em `DungeonGeneratorTests.cs`:

```csharp
    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(123L)]
    public void arena_has_freestanding_pillars(long seed)
    {
        // a free-standing pillar = a rock cell whose full 8-ring is open floor
        var f = Generate(seed);
        var pillars = 0;
        for (var y = 2; y < f.H - 2; y++)
            for (var x = 2; x < f.W - 2; x++)
            {
                if (!f.Blocked[y * f.W + x]) continue;
                var ringOpen = true;
                for (var dy = -1; dy <= 1 && ringOpen; dy++)
                    for (var dx = -1; dx <= 1 && ringOpen; dx++)
                        if ((dx != 0 || dy != 0) && f.Blocked[(y + dy) * f.W + x + dx]) ringOpen = false;
                if (ringOpen) pillars++;
            }
        Assert.True(pillars >= 1, "expected at least one free-standing 1x1 pillar (2x2 pillars have no fully-open ring per cell)");
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(42L)]
    [InlineData(99999L)]
    public void boss_floor_is_connected_and_elliptical(long seed)
    {
        var f = Generate(seed, boss: true);
        var live = Flood(f);
        for (var i = 0; i < f.Blocked.Length; i++)
            if (!f.Blocked[i]) Assert.True(live[i], "boss arena has unreachable open cells");
        // corners of the ROOM rect must be rock (ellipse, not square)
        var room = f.Rooms[0];
        Assert.True(f.Blocked[room.Y * f.W + room.X], "NW room corner should be rock");
        Assert.True(f.Blocked[room.Y * f.W + room.X + room.W - 1], "NE room corner should be rock");
        Assert.True(f.Blocked[(room.Y + room.H - 1) * f.W + room.X], "SW room corner should be rock");
        Assert.True(f.Blocked[(room.Y + room.H - 1) * f.W + room.X + room.W - 1], "SE room corner should be rock");
    }
```

- [x] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter DungeonGeneratorTests`
Expected: os 2 novos FALHAM.

> **Nota (execução 2026-07-06):** só `arena_has_freestanding_pillars` falhou (3 casos). O
> `boss_floor_is_connected_and_elliptical` já passava no gerador por lóbulos da Task 1 (o
> `ErodeArena` antigo do boss floor já produzia uma forma conectada com cantos de rocha). Falha
> vermelha confirmada onde importava (pilares); segui.

- [x] **Step 3: Constantes**

Adicionar ao bloco da arena em `GameConfig.cs`:

```csharp
    // --- Cover pillars (Wave 2): free-standing rock clumps the autopilot orbits around. ---
    /// <summary>Pillar count target as a fraction of the room area (0.010 = ~1 pillar / 100 cells).</summary>
    public const double PillarDensity = 0.010;
    /// <summary>Chance a pillar is a 2x2 clump instead of 1x1.</summary>
    public const double PillarLargeChance = 0.35;
    /// <summary>Placement attempts per target pillar before giving up (crowded arenas place fewer).</summary>
    public const int PillarPlacementAttemptsFactor = 8;
    /// <summary>Chebyshev radius around the room centre kept pillar-free (the battle stage + altar).</summary>
    public const int PillarCoreExclusion = 4;

    // --- Boss amphitheatre (Wave 2): elliptical hall with a stepped rim and pillar arcs. ---
    /// <summary>Normalized ellipse distance where the stepped-rim noise band begins.</summary>
    public const double AmphitheaterRimNoiseBand = 0.86;
    /// <summary>Rock probability inside the rim band (reads as broken amphitheatre steps).</summary>
    public const double AmphitheaterRimNoiseProb = 0.35;
```

- [x] **Step 4: Implementar**

Em `DungeonGenerator.cs`. Primeiro o dispatch — no método `Generate`, o loop de carve (linhas ~92-100) vira:

```csharp
        var singleArena = floor.Rooms.Count == 1;
        foreach (var room in floor.Rooms)
        {
            for (var yy = room.Y; yy < room.Y + room.H; yy++)
                for (var xx = room.X; xx < room.X + room.W; xx++)
                    floor.Blocked[yy * size + xx] = false;
            if (singleArena && isBossFloor) CarveAmphitheater(floor, room, rng);
            else if (singleArena)
            {
                ErodeArena(floor, room, rng);
                PlacePillars(floor, room, rng);
            }
            else ErodeRoom(floor, room, rng);
        }
```

Novos métodos (depois de `ApplyRockToFloor`):

```csharp
    /// <summary>
    /// Free-standing pillar clusters: cover for the orbit-and-AoE autopilot. Each pillar is a 1x1 or
    /// 2x2 rock stamp committed only where its full surrounding ring is open, so a pillar can never
    /// split the arena (an obstacle fully surrounded by open floor preserves 4-way connectivity).
    /// Deterministic: fixed attempt loop, all draws from the run rng.
    /// </summary>
    private static void PlacePillars(DungeonFloor floor, Room room, Rng rng)
    {
        var size = floor.W;
        int ccx = room.CenterX, ccy = room.CenterY;
        var target = (int)Math.Round(room.W * room.H * GameConfig.PillarDensity);
        var placed = 0;
        var maxAttempts = target * GameConfig.PillarPlacementAttemptsFactor;
        for (var attempt = 0; attempt < maxAttempts && placed < target; attempt++)
        {
            var pw = rng.Chance(GameConfig.PillarLargeChance) ? 2 : 1;
            var x = rng.Range(room.X + 2, room.X + room.W - 2 - pw);
            var y = rng.Range(room.Y + 2, room.Y + room.H - 2 - pw);
            // keep the battle stage + Echo altar clear
            if (Math.Max(Math.Abs(x - ccx), Math.Abs(y - ccy)) <= GameConfig.PillarCoreExclusion + pw) continue;
            var clear = true;
            for (var dy = -1; dy <= pw && clear; dy++)
                for (var dx = -1; dx <= pw && clear; dx++)
                    if (floor.Blocked[(y + dy) * size + (x + dx)]) clear = false;
            if (!clear) continue;
            for (var dy = 0; dy < pw; dy++)
                for (var dx = 0; dx < pw; dx++)
                    floor.Blocked[(y + dy) * size + (x + dx)] = true;
            placed++;
        }
    }

    /// <summary>
    /// Boss hall as an amphitheatre: an ellipse filling the room with a noisy stepped rim, two
    /// symmetric pillar arcs framing the stage, and a guaranteed-open south apron (the entry side).
    /// Reuses the CA+flood tail so the rim reads organic. Deterministic (run rng, fixed scan order).
    /// </summary>
    private static void CarveAmphitheater(DungeonFloor floor, Room room, Rng rng)
    {
        int w = room.W, h = room.H;
        double cx = w / 2.0, cy = h / 2.0;
        double rx = w * 0.5 - 1.5, ry = h * 0.5 - 1.5;

        var rock = new bool[w * h];
        for (var ly = 0; ly < h; ly++)
            for (var lx = 0; lx < w; lx++)
            {
                var dx = (lx + 0.5 - cx) / rx;
                var dy = (ly + 0.5 - cy) / ry;
                var d = dx * dx + dy * dy;
                rock[ly * w + lx] = d > 1.0
                    || (d > GameConfig.AmphitheaterRimNoiseBand && rng.Chance(GameConfig.AmphitheaterRimNoiseProb));
            }

        // two symmetric pillar arcs framing the boss stage (E/W of the centre)
        Span<int> signs = [-1, 1];
        foreach (var sign in signs)
            for (var k = -2; k <= 2; k++)
            {
                var px = (int)(cx + sign * rx * 0.55);
                var py = (int)(cy + k * ry * 0.30);
                if (px >= 0 && px < w && py >= 0 && py < h) rock[py * w + px] = true;
            }

        // south entry apron: a 3-wide guaranteed-open lane from the rim to the centre
        for (var ly = (int)cy; ly < h - 1; ly++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var lx = (int)cx + dx;
                if (lx >= 0 && lx < w) rock[ly * w + lx] = false;
            }

        ApplyRockToFloor(floor, room, rock);
    }
```

- [x] **Step 5: Rodar testes**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter DungeonGeneratorTests`
Expected: PASS (todos, incluindo os da Task 1 — pilares não podem quebrar conectividade). ✅ 19/19 (e 34/34 no projeto inteiro).

- [x] **Step 6: Build + commit**

```bash
dotnet build backend/src/KaezanArenaFable.Api
git add -A backend/src/KaezanArenaFable.Api backend/tests
git commit -m "feat(mapgen): cover pillars and boss amphitheatre"
```

Nota: `--golden-check` segue FALHANDO — esperado; rebaseline só na Task 6.

---

### Task 3: Câmaras laterais (pockets) com garganta orgânica

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Consumes: `DungeonFloor.Chests`/`BenefitChests`; dispatch da Task 2.
- Produces: `private static void CarveSidePockets(DungeonFloor, Room, Rng)`, chamado no floor 0 (não-boss) depois de `ErodeArena` e ANTES de `PlacePillars`. Constantes `ArenaPocketsMin/Max`, `PocketDepth`, `PocketRadiusMin/Max`, `PocketPlacementAttempts`.

- [x] **Step 1: Teste que falha**

```csharp
    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(123L)]
    [InlineData(4242L)]
    public void arena_has_reachable_benefit_pocket(long seed)
    {
        var f = Generate(seed);
        Assert.True(f.BenefitChests.Count >= 1, "expected at least one side-pocket benefit chest");
        var live = Flood(f);
        foreach (var (x, y) in f.BenefitChests)
        {
            Assert.False(f.Blocked[y * f.W + x], $"benefit chest at ({x},{y}) sits on rock");
            Assert.True(live[y * f.W + x], $"benefit chest at ({x},{y}) unreachable from entry");
        }
    }
```

- [x] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter arena_has_reachable_benefit_pocket`
Expected: FAIL (`BenefitChests` vazio no floor 0 hoje). ✅ 4/4 falharam ("expected at least one side-pocket benefit chest").

- [x] **Step 3: Constantes**

```csharp
    // --- Side pockets (Wave 2): chambers carved into the arena rock, joined by a 2-wide throat.
    // They deliver the "side chamber" beat without reactivating the legacy multi-room path (the
    // single-arena combat flow — chest drops on kills, teleport exit — depends on Rooms.Count==1). ---
    public const int ArenaPocketsMin = 1;
    public const int ArenaPocketsMax = 2;
    /// <summary>Tiles the pocket centre is pushed into the rock from its coastline anchor.</summary>
    public const int PocketDepth = 4;
    public const int PocketRadiusMin = 2;
    public const int PocketRadiusMax = 3;
    public const int PocketPlacementAttempts = 80;
```

- [x] **Step 4: Implementar**

> **Desvio consciente (execução 2026-07-06):** a implementação do plano (dart-throw no band
> `[room.X+4 .. room.X+W-5]` + `PocketDepth` fixo de 4) **não carvava nenhum pocket** — evidência
> instrumentada: `onRock=0, noAnchor≈78/80`. Motivo geométrico: o *forced-open core* de
> `ApplyRockToFloor` (raio `min(w,h)/3`=6) enche o interior, então a coastline fica colada na borda
> da room (fora do band de amostragem) e o anel de rocha tem só ~4 tiles — `PocketDepth=4` estoura a
> margem da room (`noFit`). Correção: (1) montar a **lista completa de âncoras de coastline** por scan
> determinístico (ordem y,x,dir) e sortear com o rng — acha âncora sempre que existe; (2) **ajuste
> adaptativo** que encaixa o maior pocket (raio→profundidade) que cabe na room com centro em rocha,
> encolhendo em anéis finos. `PocketRadiusMin` 2→1 (anéis finos só comportam pocket pequeno). Verde
> nos 4 seeds + 38/38 no projeto.

No dispatch (Task 2 Step 4), o branch não-boss vira:

```csharp
            else if (singleArena)
            {
                ErodeArena(floor, room, rng);
                CarveSidePockets(floor, room, rng);
                PlacePillars(floor, room, rng);
            }
```

Novo método:

```csharp
    /// <summary>
    /// Side chambers: 1-2 round pockets carved into the rock just past the arena's coastline, each
    /// joined by a short 2-wide throat and holding a benefit chest (never a mimic — the reward for
    /// the detour). Anchors are open cells whose 4-neighbour toward the rock is blocked; the pocket
    /// centre sits PocketDepth tiles into that rock. Carving only ever OPENS cells, so connectivity
    /// can only grow. Deterministic: fixed attempt loop, all draws from the run rng.
    /// </summary>
    private static void CarveSidePockets(DungeonFloor floor, Room room, Rng rng)
    {
        var size = floor.W;
        var target = rng.Range(GameConfig.ArenaPocketsMin, GameConfig.ArenaPocketsMax);
        var carved = 0;
        Span<(int dx, int dy)> dirs = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        for (var attempt = 0; attempt < GameConfig.PocketPlacementAttempts && carved < target; attempt++)
        {
            var x = rng.Range(room.X + 4, room.X + room.W - 5);
            var y = rng.Range(room.Y + 4, room.Y + room.H - 5);
            if (floor.Blocked[y * size + x]) continue;
            var (dx, dy) = dirs[rng.Next(4)];
            if (!floor.Blocked[(y + dy) * size + (x + dx)]) continue; // not a coastline anchor

            var r = rng.Range(GameConfig.PocketRadiusMin, GameConfig.PocketRadiusMax);
            var pcx = x + dx * GameConfig.PocketDepth;
            var pcy = y + dy * GameConfig.PocketDepth;
            // pocket disk must stay inside the room rect with a 1-tile rock margin
            if (pcx - r < room.X + 1 || pcx + r > room.X + room.W - 2 ||
                pcy - r < room.Y + 1 || pcy + r > room.Y + room.H - 2) continue;

            for (var oy = -r; oy <= r; oy++)
                for (var ox = -r; ox <= r; ox++)
                    if (ox * ox + oy * oy <= r * r)
                        floor.Blocked[(pcy + oy) * size + (pcx + ox)] = false;

            // 2-wide throat from the anchor to the pocket centre
            for (var step = 0; step <= GameConfig.PocketDepth; step++)
            {
                int tx = x + dx * step, ty = y + dy * step;
                floor.Blocked[ty * size + tx] = false;
                floor.Blocked[(ty + Math.Abs(dx)) * size + (tx + Math.Abs(dy))] = false;
            }

            floor.Chests.Add((pcx, pcy));
            floor.BenefitChests.Add((pcx, pcy));
            carved++;
        }
    }
```

- [x] **Step 5: Rodar testes**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter DungeonGeneratorTests`
Expected: PASS (todos — pockets só abrem células, então conectividade e pilares seguem válidos). ✅ 23/23 (e 38/38 no projeto inteiro).

- [x] **Step 6: Build + commit**

```bash
dotnet build backend/src/KaezanArenaFable.Api
git add -A backend/src/KaezanArenaFable.Api backend/tests
git commit -m "feat(mapgen): side pockets with benefit chests carved into the arena rock"
```

---

### Task 4: Autotiling blob (infra 47 casos + fallback behavior-preserving)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium — infra pura com oracle test bit-exato; golden não pode mudar nesta task

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Engine/WallAutotile.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` (novo param opcional em `BiomeDef`)
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (`PaintTiles` usa `WallAutotile`; `ClassifyWall` é removido)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/WallAutotileTests.cs` (criar)

**Interfaces:**
- Consumes: `DungeonFloor`, `BiomeDef` (WallH/WallV/WallPole/WallCorner).
- Produces: `WallAutotile.Mask(DungeonFloor floor, int x, int y): int` (máscara blob canônica, bits 0..7 = N,NE,E,SE,S,SW,W,NW de chão ABERTO; diagonal só conta com as duas arestas adjacentes abertas — 47 classes canônicas); `WallAutotile.Resolve(int mask, BiomeDef biome): ushort`; `BiomeDef` ganha `WallTileSet? WallSet = null` (a Onda 4 preenche com tilesets autorais). **Esta task NÃO muda nenhum hash do golden** — o fallback reproduz `ClassifyWall` bit a bit.

- [x] **Step 1: Testes que falham**

Criar `backend/tests/KaezanArenaFable.Api.Tests/WallAutotileTests.cs`:

```csharp
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

public class WallAutotileTests
{
    private static DungeonFloor FloorFrom(string[] rows)
    {
        // '#' = blocked, '.' = open
        int h = rows.Length, w = rows[0].Length;
        var f = new DungeonFloor
        {
            Index = 0, W = w, H = h,
            Ground = new ushort[w * h], Wall = new ushort[w * h], Decor = new ushort[w * h],
            Blocked = new bool[w * h], Rooms = []
        };
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                f.Blocked[y * w + x] = rows[y][x] == '#';
        return f;
    }

    [Fact]
    public void diagonal_only_counts_with_both_adjacent_edges_open()
    {
        // centre '#': NE diagonal open but N and E blocked -> canonical mask must be 0
        var f = FloorFrom(new[]
        {
            "##.",
            "###",
            "###",
        });
        Assert.Equal(0, WallAutotile.Mask(f, 1, 1));
    }

    [Fact]
    public void edges_and_valid_diagonals_set_their_bits()
    {
        // centre '#': N,E open and NE open -> bits N(1) + NE(2) + E(4) = 7
        var f = FloorFrom(new[]
        {
            "#..",
            "#*.".Replace('*', '#'),
            "###",
        });
        Assert.Equal(1 + 2 + 4, WallAutotile.Mask(f, 1, 1));
    }

    [Fact]
    public void fallback_matches_legacy_classify_on_all_masks()
    {
        var biome = Biomes.ForTier(1);
        for (var mask = 0; mask < 256; mask++)
        {
            var canonical = WallAutotile.Canonical(mask);
            Assert.Equal(LegacyClassify(canonical, biome), WallAutotile.Resolve(canonical, biome));
        }
    }

    /// <summary>Oracle: the exact decision table of the old DungeonGenerator.ClassifyWall.</summary>
    private static ushort LegacyClassify(int mask, BiomeDef biome)
    {
        var n = (mask & 1) != 0; var e = (mask & 4) != 0; var s = (mask & 16) != 0; var w = (mask & 64) != 0;
        var vertAxis = n || s;
        var horizAxis = e || w;
        if (vertAxis && horizAxis)
            return (n && s) || (e && w) ? biome.WallPole : biome.WallCorner;
        if (vertAxis) return biome.WallH;
        if (horizAxis) return biome.WallV;
        return biome.WallCorner;
    }

    [Fact]
    public void authored_wall_set_wins_over_fallback()
    {
        var baseBiome = Biomes.ForTier(1);
        var biome = baseBiome with { WallSet = new WallTileSet(new Dictionary<int, ushort> { [1] = 9999 }) };
        Assert.Equal((ushort)9999, WallAutotile.Resolve(1, biome));
        Assert.Equal(WallAutotile.Resolve(4, baseBiome), WallAutotile.Resolve(4, biome)); // missing slot falls back
    }
}
```

- [x] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter WallAutotileTests`
Expected: FAIL — `WallAutotile`/`WallTileSet` não existem (erro de compilação do projeto de testes).
✅ Confirmado vermelho: erros CS0103 (`WallAutotile`) e CS0117/CS0246 (`WallSet`/`WallTileSet`).

- [x] **Step 3: Implementar**

`Biomes.cs` — adicionar param opcional ao record (último, com default, para não quebrar os construtores existentes) e o record novo no fim do arquivo:

```csharp
public sealed record BiomeDef(
    ushort[] Ground, ushort[] BossGround, ushort Bedrock,
    ushort WallH, ushort WallV, ushort WallPole, ushort WallCorner,
    ushort[] Decor, double DecorChance,
    ushort[] Accent, double AccentChance,
    BiomeAtmosphere Atmosphere,
    WallTileSet? WallSet = null);

/// <summary>Authored 47-case blob wall set (Wave 4 tilesets), keyed by canonical blob mask.
/// Missing slots fall back to the biome's 4-piece family via <see cref="Engine.WallAutotile"/>.</summary>
public sealed record WallTileSet(Dictionary<int, ushort> Tiles);
```

Criar `backend/src/KaezanArenaFable.Api/Engine/WallAutotile.cs`:

```csharp
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Blob autotiling for wall cells. The 8-bit neighbourhood mask of OPEN floor (bit set = open)
/// is canonicalized with the blob rule — a diagonal only counts when both of its adjacent edges
/// are open — reducing 256 raw masks to the canonical 47 blob cases. Resolution prefers an
/// authored per-biome 47-slot wall set (Wave 4 tilesets); the fallback maps onto the biome's
/// 4-piece family with the exact decision table of the legacy heuristic, so adopting the
/// autotiler changes no golden hash until a WallSet exists.
/// Bit layout: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW.
/// </summary>
public static class WallAutotile
{
    public static int Mask(DungeonFloor floor, int x, int y)
    {
        bool Open(int dx, int dy)
        {
            int nx = x + dx, ny = y + dy;
            return floor.InBounds(nx, ny) && !floor.Blocked[ny * floor.W + nx];
        }
        var raw = 0;
        if (Open(0, -1)) raw |= 1;
        if (Open(1, -1)) raw |= 2;
        if (Open(1, 0)) raw |= 4;
        if (Open(1, 1)) raw |= 8;
        if (Open(0, 1)) raw |= 16;
        if (Open(-1, 1)) raw |= 32;
        if (Open(-1, 0)) raw |= 64;
        if (Open(-1, -1)) raw |= 128;
        return Canonical(raw);
    }

    /// <summary>Blob canonicalization: drop any diagonal bit whose two adjacent edge bits are not both set.</summary>
    public static int Canonical(int mask)
    {
        var n = (mask & 1) != 0; var e = (mask & 4) != 0; var s = (mask & 16) != 0; var w = (mask & 64) != 0;
        if (!(n && e)) mask &= ~2;
        if (!(s && e)) mask &= ~8;
        if (!(s && w)) mask &= ~32;
        if (!(n && w)) mask &= ~128;
        return mask;
    }

    public static ushort Resolve(int mask, BiomeDef biome)
    {
        if (biome.WallSet is { } set && set.Tiles.TryGetValue(mask, out var authored)) return authored;
        return Fallback(mask, biome);
    }

    /// <summary>Legacy 4-piece mapping (bit-exact with the old ClassifyWall decision table).</summary>
    internal static ushort Fallback(int mask, BiomeDef biome)
    {
        var n = (mask & 1) != 0; var e = (mask & 4) != 0; var s = (mask & 16) != 0; var w = (mask & 64) != 0;
        var vertAxis = n || s;
        var horizAxis = e || w;
        if (vertAxis && horizAxis)
            return (n && s) || (e && w) ? biome.WallPole : biome.WallCorner;
        if (vertAxis) return biome.WallH;
        if (horizAxis) return biome.WallV;
        return biome.WallCorner;
    }
}
```

Em `DungeonGenerator.PaintTiles` (linha ~717), trocar `floor.Wall[i] = ClassifyWall(floor, x, y, biome);` por `floor.Wall[i] = WallAutotile.Resolve(WallAutotile.Mask(floor, x, y), biome);` e **remover o método `ClassifyWall` inteiro** (linhas ~781-807).

- [x] **Step 4: Rodar testes + confirmar golden intocado por esta task**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests`
Expected: PASS. ✅ 42/42 (38 anteriores + 4 novos WallAutotileTests).

Confirmação extra (o diff do golden desta task deve ser vazio): rode `dotnet run --project tools/BalanceSim -- --golden --golden-out C:\Users\toudi\AppData\Local\Temp\golden_task4.txt` antes e depois do Step 3 e compare com `git diff --no-index` (ou `fc`) — os dois arquivos devem ser idênticos.
✅ Verificado: gerei o golden com a mudança aplicada, dei `git stash` pra voltar ao fim da Task 3,
gerei de novo, e `diff` deu VAZIO (70 floors, 7 seeds × 5 tiers × 2 floors) — a task não muda hash.

- [x] **Step 5: Build + commit**

```bash
dotnet build backend/src/KaezanArenaFable.Api
git add -A backend/src/KaezanArenaFable.Api backend/tests
git commit -m "refactor(mapgen): blob autotile infrastructure with behavior-preserving fallback"
```
✅ `dotnet build` limpo (0 warnings, 0 errors).

---

### Task 5: Profundidade visual barata (client-side) + clusters de decor maiores

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium

**Files:**
- Create: `frontend/src/app/core/tile-shade.ts`
- Create: `frontend/src/app/core/tile-shade.spec.ts`
- Modify: `frontend/src/app/core/renderer.ts` (`setMap` ~linha 230; pass de desenho após ground+decor ~linha 748)
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (`DecorClusterRadius` 1→2, `DecorDensityScale` 0.5→0.7)

**Interfaces:**
- Consumes: `MapDto` do cliente (`map.w`, `map.h`, `map.blocked`, `map.ground`, `map.decor` — ver `types.ts`).
- Produces: `computeTileShade(w: number, h: number, blocked: ArrayLike<boolean>): TileShade` com `edges: Uint8Array` (bits 1=N,2=E,4=S,8=W — lados que encostam em parede) e `variation: Uint8Array` (bucket 0..3 por célula, hash estável de x,y). Renderer ganha campo `private shade: TileShade | null` e método `private drawTileShade(...)`.

- [ ] **Step 1: Teste Vitest que falha**

Criar `frontend/src/app/core/tile-shade.spec.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { computeTileShade } from './tile-shade';

describe('computeTileShade', () => {
  // 3x3: rock ring around one open centre
  const blocked = [
    true, true, true,
    true, false, true,
    true, true, true,
  ];

  it('marks every wall-facing side of an enclosed cell', () => {
    const s = computeTileShade(3, 3, blocked);
    expect(s.edges[4]).toBe(1 | 2 | 4 | 8);
  });

  it('leaves blocked cells unmarked and treats out-of-bounds as wall', () => {
    const open = new Array(4).fill(false); // 2x2 all open
    const s = computeTileShade(2, 2, open);
    expect(s.edges[0]).toBe(1 | 8); // NW cell: map edge above and left
    expect(computeTileShade(3, 3, blocked).edges[0]).toBe(0); // blocked cell: no shading
  });

  it('variation is a stable pure function of coordinates', () => {
    const a = computeTileShade(8, 8, new Array(64).fill(false));
    const b = computeTileShade(8, 8, new Array(64).fill(false));
    expect(a.variation).toEqual(b.variation);
    expect(Array.from(a.variation).some((v) => v !== a.variation[0])).toBe(true);
  });
});
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd frontend && npm test -- --run tile-shade`
Expected: FAIL (módulo não existe).

- [ ] **Step 3: Implementar `tile-shade.ts`**

```typescript
/**
 * Precomputed cosmetic shading masks for the current floor. A pure function of the map grid —
 * no simulation input — so it can never affect determinism or the replay hash.
 */
export interface TileShade {
  /** Per-cell bitmask: 1=N, 2=E, 4=S, 8=W — which sides border a blocked cell (0 on blocked cells). */
  edges: Uint8Array;
  /** Per-cell brightness bucket 0..3 from a stable integer hash of (x,y): same map, same pattern. */
  variation: Uint8Array;
}

export function computeTileShade(w: number, h: number, blocked: ArrayLike<boolean>): TileShade {
  const edges = new Uint8Array(w * h);
  const variation = new Uint8Array(w * h);
  const isWall = (x: number, y: number) => x < 0 || x >= w || y < 0 || y >= h || !!blocked[y * w + x];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      let hsh = (x * 374761393 + y * 668265263) | 0;
      hsh = Math.imul(hsh ^ (hsh >>> 13), 1274126177);
      variation[i] = ((hsh ^ (hsh >>> 16)) >>> 0) & 3;
      if (blocked[i]) continue;
      let m = 0;
      if (isWall(x, y - 1)) m |= 1;
      if (isWall(x + 1, y)) m |= 2;
      if (isWall(x, y + 1)) m |= 4;
      if (isWall(x - 1, y)) m |= 8;
      edges[i] = m;
    }
  }
  return { edges, variation };
}
```

- [ ] **Step 4: Rodar teste e ver passar**

Run: `cd frontend && npm test -- --run tile-shade`
Expected: PASS (3 testes).

- [ ] **Step 5: Ligar no renderer**

Em `renderer.ts`:
1. Import: `import { computeTileShade, type TileShade } from './tile-shade';`
2. Campo: `private shade: TileShade | null = null;`
3. Em `setMap(...)` (~linha 230), após guardar o mapa: `this.shade = computeTileShade(map.w, map.h, map.blocked);`
4. No draw, logo DEPOIS do loop "1. ground + decor" (~linha 748) e ANTES do pass de corpses, chamar `this.drawTileShade(ctx, map, sx, sy);` e adicionar o método (usar EXATAMENTE os mesmos limites de loop/culling do pass de ground — copie os bounds do loop existente):

```typescript
  /** Cosmetic depth: darken floor cells hugging a wall (inner shadow) + subtle per-cell variation. */
  private drawTileShade(
    ctx: CanvasRenderingContext2D, map: MapView,
    sx: (x: number) => number, sy: (y: number) => number,
  ): void {
    const shade = this.shade;
    if (!shade) return;
    const step = sx(1) - sx(0); // on-screen tile size, whatever SCALE is in effect
    const t = Math.round(step * 0.22);
    ctx.save();
    // use the same visible-range bounds as the ground pass above
    for (let y = 0; y < map.h; y++) {
      for (let x = 0; x < map.w; x++) {
        const i = y * map.w + x;
        if (map.blocked[i]) continue;
        const v = shade.variation[i];
        if (v) {
          ctx.fillStyle = `rgba(0,0,0,${(v * 0.025).toFixed(3)})`;
          ctx.fillRect(sx(x), sy(y), step, step);
        }
        const m = shade.edges[i];
        if (!m) continue;
        ctx.fillStyle = 'rgba(0,0,0,0.18)';
        if (m & 1) ctx.fillRect(sx(x), sy(y), step, t);
        if (m & 2) ctx.fillRect(sx(x) + step - t, sy(y), t, step);
        if (m & 4) ctx.fillRect(sx(x), sy(y) + step - t, step, t);
        if (m & 8) ctx.fillRect(sx(x), sy(y), t, step);
      }
    }
    ctx.restore();
  }
```

(`MapView` = o tipo que `setMap` já recebe no renderer; use o nome real do tipo local. Se o pass de ground cula por viewport, replique os mesmos `x0/x1/y0/y1`.)

5. Backend — em `GameConfig.cs`: `DecorClusterRadius` de `1` para `2` e `DecorDensityScale` de `0.5` para `0.7` (clusters de decor maiores, spec item 4). **Isso altera hashes de `Decor` → golden segue quebrado até a Task 6, como planejado.**

- [ ] **Step 6: Verificar no preview + medir custo**

Com backend rodando (`tools/run-backend.ps1`) e `npm start`: entrar numa run T1, abrir o overlay F3 (Onda 1) e comparar `draw p95` com o baseline da Onda 1 — o pass de shading deve custar < 1 ms no p95. Screenshot de antes/depois para o gate da onda.

- [ ] **Step 7: Builds + commit**

```bash
cd frontend && npx ng build && cd ..
dotnet build backend/src/KaezanArenaFable.Api
git add frontend/src/app/core backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs
git commit -m "feat(render): wall-adjacent inner shadow, tile variation, larger decor clusters"
```

---

### Task 6: Validador ruidoso + rebaseline explícito (golden, replays, sweep, screenshots)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high — o passo perigoso do golden/replays; checklist explícito, mas exige disciplina de gate

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Engine/DungeonValidator.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (chamar o validador no fim de `Generate`)
- Modify: `docs/balance/golden_dungeon.txt` (rebaseline via `--golden`)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonValidatorTests.cs` (criar)

**Interfaces:**
- Consumes: `DungeonFloor` completo (Entry, Chests, Sanctuaries, LadderDown, Blocked).
- Produces: `DungeonValidator.Validate(DungeonFloor floor)` — lança `InvalidOperationException` com contexto se o floor for injogável. Chamado como última linha de `DungeonGenerator.Generate` (antes do `return floor;`, depois de `PaintTiles`).

- [ ] **Step 1: Testes que falham**

Criar `backend/tests/KaezanArenaFable.Api.Tests/DungeonValidatorTests.cs`:

```csharp
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

public class DungeonValidatorTests
{
    [Fact]
    public void valid_floor_passes()
    {
        var rng = new Rng(42UL);
        var floor = DungeonGenerator.Generate(rng, 0, isBossFloor: false, Biomes.ForTier(1));
        DungeonValidator.Validate(floor); // must not throw
    }

    [Fact]
    public void unreachable_chest_fails_loudly()
    {
        var rng = new Rng(42UL);
        var floor = DungeonGenerator.Generate(rng, 0, isBossFloor: false, Biomes.ForTier(1));
        floor.Chests.Add((0, 0)); // corner is always rock (margin band)
        var ex = Assert.Throws<InvalidOperationException>(() => DungeonValidator.Validate(floor));
        Assert.Contains("chest", ex.Message);
    }

    [Fact]
    public void every_seed_and_tier_generates_valid_floors()
    {
        // the in-repo sweep: 200 seeds x 5 tiers x 2 floors, all must validate
        for (var tier = 1; tier <= 5; tier++)
        {
            var biome = Biomes.ForTier(tier);
            for (long seed = 1; seed <= 200; seed++)
            {
                var rng = new Rng((ulong)seed);
                DungeonValidator.Validate(DungeonGenerator.Generate(rng, 0, false, biome));
                DungeonValidator.Validate(DungeonGenerator.Generate(rng, 1, true, biome));
            }
        }
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter DungeonValidatorTests`
Expected: FAIL — `DungeonValidator` não existe.

- [ ] **Step 3: Implementar**

Criar `backend/src/KaezanArenaFable.Api/Engine/DungeonValidator.cs`:

```csharp
namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Fails LOUDLY when a generated floor is unplayable. Runs as the last step of
/// <see cref="DungeonGenerator.Generate"/>: an invalid layout must abort run creation with a clear
/// message instead of shipping a soft-locked run. Pure BFS over the final Blocked grid — consumes
/// no rng, so it can never perturb determinism.
/// </summary>
public static class DungeonValidator
{
    public static void Validate(DungeonFloor floor)
    {
        var size = floor.W;
        if (floor.IsBlocked(floor.Entry.X, floor.Entry.Y))
            throw Fail(floor, $"entry ({floor.Entry.X},{floor.Entry.Y}) is blocked");

        var live = Flood(floor);
        int open = 0, reachable = 0;
        for (var i = 0; i < floor.Blocked.Length; i++)
        {
            if (floor.Blocked[i]) continue;
            open++;
            if (live[i]) reachable++;
        }
        if (open == 0) throw Fail(floor, "no open cells");
        if (reachable != open)
            throw Fail(floor, $"disconnected: {open - reachable} open cell(s) unreachable from entry");

        foreach (var (x, y) in floor.Chests)
            if (floor.IsBlocked(x, y) || !live[y * size + x])
                throw Fail(floor, $"chest at ({x},{y}) unreachable");
        foreach (var (x, y) in floor.Sanctuaries)
            if (floor.IsBlocked(x, y) || !live[y * size + x])
                throw Fail(floor, $"sanctuary at ({x},{y}) unreachable");
        if (floor.LadderDown is { } ladder && (floor.IsBlocked(ladder.X, ladder.Y) || !live[ladder.Y * size + ladder.X]))
            throw Fail(floor, $"ladder at ({ladder.X},{ladder.Y}) unreachable");
    }

    private static InvalidOperationException Fail(DungeonFloor f, string reason) =>
        new($"generated floor {f.Index} invalid: {reason} (W={f.W} H={f.H} rooms={f.Rooms.Count})");

    private static bool[] Flood(DungeonFloor floor)
    {
        var size = floor.W;
        var live = new bool[size * floor.H];
        var (ex, ey) = floor.Entry;
        var stack = new Stack<int>();
        live[ey * size + ex] = true;
        stack.Push(ey * size + ex);
        Span<(int dx, int dy)> steps = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            int x = idx % size, y = idx / size;
            foreach (var (dx, dy) in steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= size || ny < 0 || ny >= floor.H) continue;
                var ni = ny * size + nx;
                if (live[ni] || floor.Blocked[ni]) continue;
                live[ni] = true;
                stack.Push(ni);
            }
        }
        return live;
    }
}
```

Em `DungeonGenerator.Generate`, antes do `return floor;` final: `DungeonValidator.Validate(floor);` (também em `GenerateTrainingRoom`, antes do seu `return floor;`).

- [ ] **Step 4: Rodar TODOS os testes**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests`
Expected: PASS (o sweep de 2.000 floors incluído).

- [ ] **Step 5: Rebaseline do golden (explícito, consciente)**

```bash
dotnet run --project tools/BalanceSim -- --golden
git diff docs/balance/golden_dungeon.txt   # revisar: TODOS os 70 hashes devem ter mudado (gerador novo)
dotnet run --project tools/BalanceSim -- --golden-check
```
Expected no check: `golden mode: GREEN`.

- [ ] **Step 6: Replays FF-01 — limpar e regravar**

Replays salvos foram gravados com o gerador antigo: re-simulá-los com o gerador novo diverge por construção. Limpar e regravar:

```powershell
Remove-Item backend/src/KaezanArenaFable.Api/.data/replays/* -Confirm:$false
```

Depois, com o backend rodando (`tools/run-backend.ps1`), jogar 2-3 runs até o fim (T1 e T3 — vitória ou derrota, tanto faz) para o `ReplayStore` congelar replays novos, e então:

```bash
dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays
```
Expected: verde (todas as runs re-simulam bit-perfect).

- [ ] **Step 7: Sweep do BalanceSim + screenshots por bioma**

```bash
dotnet run --project tools/BalanceSim
```
Expected: sweep completa sem run inacabável/timeout.

Screenshots lado-a-lado por bioma (T1–T5): uma run de cada tier via preview, capturar com `preview_screenshot`, salvar em `docs/balance/mapgen_v2_screenshots/` — o material de revisão do gate.

- [ ] **Step 8: README + commit final**

Atualizar `README.md` (seção de geração de mapas, se existir; senão 2 linhas na seção do engine) descrevendo: macro-forma por lóbulos, pilares, pockets, anfiteatro, validador.

```bash
dotnet build backend/src/KaezanArenaFable.Api
git add -A
git commit -m "feat(mapgen): loud floor validator; rebaseline golden and replays for mapgen v2"
```

---

## Gate de saída da Onda 2

- [ ] `dotnet build` + `dotnet test` (backend) e `npx ng build` + `npm test` (frontend) verdes.
- [ ] `--golden-check` GREEN com o baseline novo commitado conscientemente.
- [ ] `--replay-check` GREEN com replays regravados.
- [ ] Sweep do BalanceSim sem run inacabável.
- [ ] Screenshots lado-a-lado por bioma em `docs/balance/mapgen_v2_screenshots/` revisados pelo usuário.
- [ ] Overlay F3: `draw p95` sem regressão > 1 ms vs. baseline da Onda 1.
