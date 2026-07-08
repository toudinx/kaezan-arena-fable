using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

[Collection("Tileset registry")]
public class DungeonGeneratorTests
{
    public DungeonGeneratorTests()
    {
        // Default biomes carry GroundFamilies, so generation reads the real tileset registry;
        // reload it per test because other classes in this collection load fakes.
        TilesetRegistry.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json"));
    }

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

    [Fact]
    public void PackedStacksMatchLegacyLayers()
    {
        DungeonFloor floor = Generate(42L);
        MapDto dto = MapDto.FromFloor(floor, Biomes.ForTier(1).Atmosphere, floor.Index, []);

        for (int i = 0; i < floor.W * floor.H; i++)
        {
            ushort[] borders = floor.BorderStack.Length == floor.W * floor.H && floor.BorderStack[i].Length > 0
                ? floor.BorderStack[i]
                : [.. new[] { floor.BorderA[i], floor.BorderB[i] }.Where(id => id != 0)];
            ushort[] expectedFlat = [.. new[] { floor.Ground[i] }
                .Concat(borders)
                .Concat([floor.Decor[i]])
                .Where(id => id != 0)];
            ushort[] expectedTall = floor.Wall[i] == 0 ? [] : [floor.Wall[i]];

            Assert.Equal(expectedFlat, floor.Flat[i]);
            Assert.Equal(expectedTall, floor.Tall[i]);
            Assert.Equal(expectedFlat, dto.Flat[i]);
            Assert.Equal(expectedTall, dto.Tall[i]);
        }
    }

    /// <summary>Generate with the WallSet resolved from the registry (production path), so the
    /// mountain brush can read the authored 47-slot family — the test <see cref="Generate(long,bool)"/>
    /// helper uses unresolved biomes (WallSet null) and only exercises the 4-piece fallback.</summary>
    private static DungeonFloor GenerateResolved(long seed, int tier)
    {
        Rng rng = new Rng((ulong)seed);
        return DungeonGenerator.Generate(rng, 0, isBossFloor: false, Biomes.Resolve(Biomes.ForTier(tier)));
    }

    [Theory]
    [InlineData(101L)]
    [InlineData(202L)]
    [InlineData(303L)]
    public void NoBlockedCellLacksOpaqueBacking(long seed)
    {
        // T3 mountain brush: every blocked cell carries an opaque bedrock backing under its wall
        // sprite, so no alpha wall piece ever reveals a black void beneath it.
        for (int tier = 1; tier <= 5; tier++)
        {
            BiomeDef biome = Biomes.Resolve(Biomes.ForTier(tier));
            DungeonFloor floor = GenerateResolved(seed, tier);
            for (int i = 0; i < floor.Blocked.Length; i++)
            {
                if (!floor.Blocked[i]) continue;
                Assert.True(floor.Flat[i].Length > 0,
                    $"tier {tier} blocked cell {i % floor.W},{i / floor.W} has no opaque backing");
                Assert.Equal(biome.Bedrock, floor.Flat[i][0]);
            }
        }
    }

    [Theory]
    [InlineData(101L)]
    [InlineData(202L)]
    [InlineData(303L)]
    public void MassifInteriorUsesFamilyBody(long seed)
    {
        // A fully-enclosed blocked cell (blob mask 0) must draw the WallSet's mask-0 body piece —
        // the family massif — not the generic solid corner (1116/DirtCorner).
        for (int tier = 1; tier <= 5; tier++)
        {
            BiomeDef biome = Biomes.Resolve(Biomes.ForTier(tier));
            Assert.NotNull(biome.WallSet);
            ushort body = biome.WallSet!.Tiles[0];
            DungeonFloor floor = GenerateResolved(seed, tier);
            int enclosed = 0;
            for (int y = 0; y < floor.H; y++)
                for (int x = 0; x < floor.W; x++)
                {
                    int i = y * floor.W + x;
                    if (!floor.Blocked[i]) continue;
                    if (WallAutotile.Mask(floor, x, y) != 0) continue;
                    enclosed++;
                    Assert.Equal(new ushort[] { body }, floor.Tall[i]);
                }
            Assert.True(enclosed > 0, $"tier {tier} seed {seed} has no enclosed massif cell to check");
        }
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

    /// <summary>12x10 authored arena: full wall ring, open interior, one mouth on the west edge.</summary>
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
        // seed chosen to guarantee placement (PrefabRoomChance=0.6): iterate seeds until one lands
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

    [Fact]
    public void prefab_rect_clears_border_layers()
    {
        // Task 8 Step 3: authored crops carry their own borders as decor, so the painter's
        // border layer must be zeroed across the whole prefab rect (open AND blocked cells).
        for (long seed = 1; seed < 50; seed++)
        {
            Rng rng = new Rng((ulong)seed);
            DungeonFloor f = DungeonGenerator.Generate(rng, 0, false, Biomes.ForTier(1), [TestPrefab()]);
            Room? room = f.Rooms.FirstOrDefault(r => r.PrefabId != "");
            if (room is null) continue;
            for (int y = room.Y; y < room.Y + room.H; y++)
                for (int x = room.X; x < room.X + room.W; x++)
                {
                    Assert.Equal((ushort)0, f.BorderA[y * f.W + x]);
                    Assert.Equal((ushort)0, f.BorderB[y * f.W + x]);
                }
            return;
        }
        Assert.Fail("no seed in 1..49 placed a prefab — placement is broken");
    }

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
}

/// <summary>Map beauty Task 7: coherent ground patches (jittered-Voronoi family regions).</summary>
[Collection("Tileset registry")]
public class DungeonGroundPatchTests
{
    private const string FakeTilesets = """
    {
      "families": {
        "a": { "kind": "ground", "items": [1001, 1002], "zOrder": 100 },
        "b": { "kind": "ground", "items": [2001, 2002, 2003], "zOrder": 200 }
      }
    }
    """;

    private static void LoadFakeRegistry()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "tilesets.json");
        File.WriteAllText(path, FakeTilesets);
        TilesetRegistry.LoadFrom(path);
    }

    private static BiomeDef PatchBiome() =>
        Biomes.Cave with { WallSet = null, WallFamily = "", GroundFamilies = ["a", "b"] };

    private static BiomeDef LegacyBiome() =>
        Biomes.Cave with { WallSet = null, WallFamily = "", GroundFamilies = null };

    private static DungeonFloor Generate(long seed, BiomeDef biome)
    {
        Rng rng = new Rng((ulong)seed);
        return DungeonGenerator.Generate(rng, 0, isBossFloor: false, biome);
    }

    private static string FloorHash(DungeonFloor floor)
    {
        string payload = string.Join(",", floor.Ground)
            + "|" + string.Join(",", floor.Wall)
            + "|" + string.Join(",", floor.Decor)
            + "|" + string.Join(",", floor.Blocked.Select(b => b ? 1 : 0))
            + $"|{floor.Entry.X},{floor.Entry.Y}";
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(42L)]
    public void patch_ground_is_deterministic(long seed)
    {
        LoadFakeRegistry();
        DungeonFloor a = Generate(seed, PatchBiome());
        DungeonFloor b = Generate(seed, PatchBiome());
        Assert.Equal(a.Ground, b.Ground);
    }

    [Fact]
    public void floor_contains_ids_from_both_families()
    {
        LoadFakeRegistry();
        DungeonFloor floor = Generate(42L, PatchBiome());
        ushort[] familyA = [1001, 1002];
        ushort[] familyB = [2001, 2002, 2003];
        bool hasA = false, hasB = false;
        for (int i = 0; i < floor.Ground.Length; i++)
        {
            if (floor.Blocked[i]) continue;
            if (familyA.Contains(floor.Ground[i])) hasA = true;
            if (familyB.Contains(floor.Ground[i])) hasB = true;
        }
        Assert.True(hasA, "no open cell uses family 'a' ground ids");
        Assert.True(hasB, "no open cell uses family 'b' ground ids");
    }

    [Fact]
    public void every_open_cell_uses_a_family_id()
    {
        LoadFakeRegistry();
        DungeonFloor floor = Generate(7L, PatchBiome());
        HashSet<ushort> union = [1001, 1002, 2001, 2002, 2003];
        for (int i = 0; i < floor.Ground.Length; i++)
        {
            if (floor.Blocked[i]) continue;
            Assert.True(union.Contains(floor.Ground[i]),
                $"open cell {i % floor.W},{i / floor.W} ground {floor.Ground[i]} outside family union");
        }
    }

    [Fact]
    public void legacy_biome_output_is_byte_identical_to_pre_patch_generator()
    {
        // Regression pin for the legacy (no-family) generator path, seed 42 / tier-1 legacy biome.
        // Rebaselined 2026-07-07 by the T3 mountain brush: blocked cells now back onto opaque Bedrock
        // (was rng-picked ground on edge cells), so the wall pass is rng-free and both the ground
        // content and the downstream rng sequence changed deliberately. Off this new baseline the
        // hash must stay stable to catch accidental drift.
        LoadFakeRegistry();
        DungeonFloor floor = Generate(42L, LegacyBiome());
        Assert.Equal("BDB173594505EAA3CB325F4797AF0026F3DAF168D6AACFE235C929E479F20426", FloorHash(floor));
    }
}

/// <summary>Map composition T4: accent terrain is painted as a ground family, not decor.</summary>
[Collection("Tileset registry")]
public class DungeonAccentFamilyTests
{
    private const string FakeTilesets = """
    {
      "families": {
        "low": { "kind": "ground", "items": [1001, 1002], "zOrder": 100 },
        "lava": { "kind": "ground", "items": [3001, 3002], "zOrder": 7700 }
      },
      "borderSets": {
        "lava->low": { "n": 51, "e": 52, "s": 53, "w": 54, "cnw": 55, "cne": 56, "cse": 57, "csw": 58, "dnw": 59, "dne": 60, "dse": 61, "dsw": 62 }
      }
    }
    """;

    private static void LoadFakeRegistry()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "tilesets.json");
        File.WriteAllText(path, FakeTilesets);
        TilesetRegistry.LoadFrom(path);
    }

    private static BiomeDef AccentBiome() =>
        Biomes.Cave with
        {
            WallSet = null,
            WallFamily = "",
            GroundFamilies = ["low"],
            Accent = [],
            AccentChance = 0.12,
            AccentFamily = "lava"
        };

    [Fact]
    public void DefaultLairAndAbyssUseLavaAccentFamily()
    {
        TilesetRegistry.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json"));

        Assert.Equal("lava", Biomes.ForTier(4).AccentFamily);
        Assert.Equal("lava", Biomes.ForTier(5).AccentFamily);
        Assert.Equal("lava", Biomes.Resolve(Biomes.ForTier(4)).AccentFamily);
    }

    [Fact]
    public void AccentPatchesAreBordered()
    {
        LoadFakeRegistry();
        HashSet<ushort> lava = TilesetRegistry.Family("lava").Items.ToHashSet();
        HashSet<ushort> lavaBorders = new HashSet<ushort>(Enumerable.Range(51, 12).Select(id => (ushort)id));

        for (long seed = 1; seed <= 50; seed++)
        {
            Rng rng = new Rng((ulong)seed);
            DungeonFloor floor = DungeonGenerator.Generate(rng, 0, isBossFloor: false, AccentBiome());
            int seamCells = 0;
            int nakedSeams = 0;

            for (int y = 1; y < floor.H - 1; y++)
            {
                for (int x = 1; x < floor.W - 1; x++)
                {
                    int i = y * floor.W + x;
                    if (floor.Blocked[i] || lava.Contains(floor.Ground[i]))
                    {
                        continue;
                    }

                    bool touchesLava = false;
                    for (int dy = -1; dy <= 1 && !touchesLava; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !touchesLava; dx++)
                        {
                            if (dx == 0 && dy == 0)
                            {
                                continue;
                            }

                            touchesLava = lava.Contains(floor.Ground[(y + dy) * floor.W + x + dx]);
                        }
                    }

                    if (!touchesLava)
                    {
                        continue;
                    }

                    seamCells++;
                    if (!floor.Flat[i].Any(lavaBorders.Contains))
                    {
                        nakedSeams++;
                    }
                }
            }

            if (seamCells == 0)
            {
                continue;
            }

            Assert.Equal(0, nakedSeams);
            return;
        }

        Assert.Fail("no generated accent seam found in seeds 1..50");
    }
}

/// <summary>Map beauty Task 8: 2-slot ground border layer resolved from RME border sets.</summary>
[Collection("Tileset registry")]
public class DungeonBorderTests
{
    // "high" (z-order 200) borders over "low" (100); "rock" is the blocked wall family whose
    // "->OPEN" set is the rock foot drawn on adjacent open ground. Edge items are distinct so
    // each assert pins the exact edge piece that was resolved.
    private const string FakeTilesets = """
    {
      "families": {
        "low":  { "kind": "ground", "items": [1001], "zOrder": 100 },
        "mid":  { "kind": "ground", "items": [1501], "zOrder": 150 },
        "high": { "kind": "ground", "items": [2001], "zOrder": 200 },
        "rock": { "kind": "mountain", "items": [3001], "zOrder": 9000 }
      },
      "borderSets": {
        "mid->low": { "n": 51, "e": 52, "s": 53, "w": 54, "cnw": 55, "cne": 56, "cse": 57, "csw": 58, "dnw": 59, "dne": 60, "dse": 61, "dsw": 62 },
        "high->low": { "n": 11, "e": 12, "s": 13, "w": 14, "cnw": 15, "cne": 16, "cse": 17, "csw": 18, "dnw": 19, "dne": 20, "dse": 21, "dsw": 22 },
        "rock->OPEN": { "n": 31, "e": 32, "s": 33, "w": 34, "cnw": 35, "cne": 36, "cse": 37, "csw": 38, "dnw": 39, "dne": 40, "dse": 41, "dsw": 42 }
      }
    }
    """;

    private static void LoadFakeRegistry()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "tilesets.json");
        File.WriteAllText(path, FakeTilesets);
        TilesetRegistry.LoadFrom(path);
    }

    private const int Size = 5;
    private const int Center = 2 * Size + 2;

    /// <summary>5x5 fully-open floor with every cell defaulted to family 0 ("low").</summary>
    private static (DungeonFloor Floor, int[] FamilyOf) OpenFloor()
    {
        DungeonFloor floor = new DungeonFloor
        {
            Index = 0, W = Size, H = Size,
            Ground = new ushort[Size * Size], Wall = new ushort[Size * Size],
            Decor = new ushort[Size * Size],
            BorderA = new ushort[Size * Size], BorderB = new ushort[Size * Size],
            Blocked = new bool[Size * Size], Rooms = []
        };
        int[] familyOf = new int[Size * Size];
        return (floor, familyOf);
    }

    [Fact]
    public void single_higher_neighbour_emits_its_edge_piece()
    {
        LoadFakeRegistry();
        (DungeonFloor floor, int[] familyOf) = OpenFloor();
        familyOf[1 * Size + 2] = 1; // "high" north of the centre cell
        BorderAutotile.Paint(floor, familyOf, ["low", "high"], "");
        Assert.Equal((ushort)11, floor.BorderA[Center]); // edge "n"
        Assert.Equal((ushort)0, floor.BorderB[Center]);
    }

    [Fact]
    public void two_adjacent_edges_collapse_into_the_concave_corner()
    {
        LoadFakeRegistry();
        (DungeonFloor floor, int[] familyOf) = OpenFloor();
        familyOf[1 * Size + 2] = 1; // N
        familyOf[2 * Size + 1] = 1; // W
        BorderAutotile.Paint(floor, familyOf, ["low", "high"], "");
        Assert.Equal((ushort)15, floor.BorderA[Center]); // "cnw" swallows both edges
        Assert.Equal((ushort)0, floor.BorderB[Center]);
    }

    [Fact]
    public void edge_plus_lone_diagonal_fill_both_slots()
    {
        LoadFakeRegistry();
        (DungeonFloor floor, int[] familyOf) = OpenFloor();
        familyOf[1 * Size + 2] = 1; // N
        familyOf[3 * Size + 3] = 1; // SE diagonal
        BorderAutotile.Paint(floor, familyOf, ["low", "high"], "");
        Assert.Equal((ushort)11, floor.BorderA[Center]); // edge "n"
        Assert.Equal((ushort)21, floor.BorderB[Center]); // lone diagonal "dse"
    }

    [Fact]
    public void interior_cell_gets_no_border()
    {
        LoadFakeRegistry();
        (DungeonFloor floor, int[] familyOf) = OpenFloor();
        BorderAutotile.Paint(floor, familyOf, ["low", "high"], "");
        Assert.Equal((ushort)0, floor.BorderA[Center]);
        Assert.Equal((ushort)0, floor.BorderB[Center]);
    }

    [Fact]
    public void blocked_neighbour_counts_as_the_wall_family_open_set()
    {
        LoadFakeRegistry();
        (DungeonFloor floor, int[] familyOf) = OpenFloor();
        floor.Blocked[1 * Size + 2] = true; // rock north of the centre
        familyOf[1 * Size + 2] = -1;
        BorderAutotile.Paint(floor, familyOf, ["low", "high"], "rock");
        Assert.Equal((ushort)31, floor.BorderA[Center]); // "rock->OPEN" edge "n"
        Assert.Equal((ushort)0, floor.BorderB[Center]);
    }

    [Fact]
    public void families_stack_by_descending_z_order()
    {
        LoadFakeRegistry();
        (DungeonFloor floor, int[] familyOf) = OpenFloor();
        familyOf[1 * Size + 2] = 1;         // "high" to the N
        floor.Blocked[3 * Size + 2] = true; // "rock" to the S (z-order 9000 wins slot A)
        familyOf[3 * Size + 2] = -1;
        BorderAutotile.Paint(floor, familyOf, ["low", "high"], "rock");
        Assert.Equal((ushort)33, floor.BorderA[Center]); // rock "s" first
        Assert.Equal((ushort)11, floor.BorderB[Center]); // then high "n"
    }

    [Fact]
    public void border_stack_keeps_every_resolved_piece_in_draw_order()
    {
        LoadFakeRegistry();
        (DungeonFloor floor, int[] familyOf) = OpenFloor();
        floor.Ground[Center] = 1001;
        familyOf[1 * Size + 2] = 2;         // "high" to the N
        familyOf[2 * Size + 3] = 1;         // "mid" to the E
        floor.Blocked[3 * Size + 2] = true; // "rock" to the S
        familyOf[3 * Size + 2] = -1;

        BorderAutotile.Paint(floor, familyOf, ["low", "mid", "high"], "rock");
        floor.PackStacks();

        Assert.Equal(new ushort[] { 1001, 33, 11, 52 }, floor.Flat[Center]);
    }

    private static BiomeDef BorderBiome() =>
        Biomes.Cave with { WallSet = null, WallFamily = "rock", GroundFamilies = ["low", "high"] };

    private static DungeonFloor Generate(long seed, BiomeDef biome)
    {
        Rng rng = new Rng((ulong)seed);
        return DungeonGenerator.Generate(rng, 0, isBossFloor: false, biome);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(42L)]
    public void border_layers_are_deterministic(long seed)
    {
        LoadFakeRegistry();
        DungeonFloor a = Generate(seed, BorderBiome());
        DungeonFloor b = Generate(seed, BorderBiome());
        Assert.Equal(a.BorderA, b.BorderA);
        Assert.Equal(a.BorderB, b.BorderB);
        Assert.Contains(a.BorderA, id => id != 0); // seams and rock feet must actually paint
    }

    [Fact]
    public void legacy_biome_leaves_border_layers_empty()
    {
        LoadFakeRegistry();
        DungeonFloor floor = Generate(42L,
            Biomes.Cave with { WallSet = null, WallFamily = "", GroundFamilies = null });
        Assert.All(floor.BorderA, id => Assert.Equal((ushort)0, id));
        Assert.All(floor.BorderB, id => Assert.Equal((ushort)0, id));
    }
}
