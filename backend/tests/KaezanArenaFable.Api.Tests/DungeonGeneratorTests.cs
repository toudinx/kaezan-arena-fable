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
        // Hash captured on 2026-07-07 BEFORE the voronoi patch pass existed (seed 42, tier-1 legacy
        // biome). The legacy path must not consume any new rng draw, so this must never change.
        LoadFakeRegistry();
        DungeonFloor floor = Generate(42L, LegacyBiome());
        Assert.Equal("F9B15983DB89884A4EB3D290B3468E40B082EEAB8C30D5B33BF8C7D5029536CA", FloorHash(floor));
    }
}
