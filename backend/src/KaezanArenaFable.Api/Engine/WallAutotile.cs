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
