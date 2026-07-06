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
}
