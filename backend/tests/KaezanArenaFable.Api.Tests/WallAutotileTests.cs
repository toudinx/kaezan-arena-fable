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
