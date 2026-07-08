using KaezanArenaFable.Api.Content;

namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Ground border pass (map beauty Task 8/T4): fills the border stack of every open cell from
/// the RME border sets in <see cref="TilesetRegistry"/>. RME rule: the neighbouring family with
/// the HIGHER z-order draws ITS outer border over the lower tile, so a cell of family A receives
/// pieces from each higher family B present in its 8-neighbourhood, edges named after where B
/// sits. Blocked neighbours count as the biome's wall family through its "-&gt;OPEN" set (the rock
/// foot at the base of mountain walls). Pure resolution: no rng draw, fixed y,x scan order,
/// neighbour families visited in z-order desc then ordinal name, every resolved piece is preserved.
/// Bit layout matches <see cref="WallAutotile"/>: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW.
/// </summary>
public static class BorderAutotile
{
    // neighbour offsets in canonical bit order 0..7 (N, NE, E, SE, S, SW, W, NW)
    private static readonly (int Dx, int Dy)[] Offsets =
    [
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)
    ];

    /// <summary>
    /// Paints border draw stacks for every open
    /// cell with a ground family (<paramref name="familyOf"/> index &gt;= 0; -1 = blocked or boss
    /// hall, which never receives borders). <paramref name="wallFamily"/> may be empty or unknown
    /// to the registry — strict validation lives in Biomes.Resolve; here it just paints nothing.
    /// </summary>
    public static void Paint(DungeonFloor floor, int[] familyOf, string[] groundFamilies, string wallFamily)
    {
        int size = floor.W;
        int familyCount = groundFamilies.Length;
        bool hasWall = wallFamily.Length > 0 && TilesetRegistry.HasFamily(wallFamily);
        int wallIndex = familyCount; // extra slot for the wall family in the per-cell mask table

        int[] zOrders = new int[familyCount + 1];
        for (int f = 0; f < familyCount; f++)
        {
            zOrders[f] = TilesetRegistry.Family(groundFamilies[f]).ZOrder;
        }
        zOrders[wallIndex] = hasWall ? TilesetRegistry.Family(wallFamily).ZOrder : int.MinValue;

        // neighbour families sorted once: z-order desc, then ordinal name (determinism)
        string NameOf(int f) => f == wallIndex ? wallFamily : groundFamilies[f];
        int[] order = new int[familyCount + (hasWall ? 1 : 0)];
        for (int f = 0; f < order.Length; f++)
        {
            order[f] = f;
        }
        Array.Sort(order, (a, b) =>
        {
            int byZ = zOrders[b].CompareTo(zOrders[a]);
            return byZ != 0 ? byZ : string.CompareOrdinal(NameOf(a), NameOf(b));
        });

        // border sets cached per (neighbour family, cell family) pair. Ground neighbours resolve
        // "B->A" (registry falls back to "B->none"); the wall family targets "OPEN" (its rock foot).
        BorderSet?[,] sets = new BorderSet?[familyCount + 1, familyCount];
        for (int a = 0; a < familyCount; a++)
        {
            for (int b = 0; b < familyCount; b++)
            {
                sets[b, a] = TilesetRegistry.Borders(groundFamilies[b], groundFamilies[a]);
            }
            if (hasWall)
            {
                sets[wallIndex, a] = TilesetRegistry.Borders(wallFamily, "OPEN");
            }
        }

        int[] masks = new int[familyCount + 1];
        List<ushort> pieces = new List<ushort>();
        for (int y = 0; y < floor.H; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (floor.Blocked[i] || familyOf[i] < 0) continue; // borders live on family ground only

                Array.Clear(masks);
                for (int bit = 0; bit < 8; bit++)
                {
                    (int dx, int dy) = Offsets[bit];
                    int nx = x + dx, ny = y + dy;
                    if (!floor.InBounds(nx, ny) || floor.Blocked[ny * size + nx])
                    {
                        if (hasWall) masks[wallIndex] |= 1 << bit;
                        continue;
                    }
                    int neighbourFamily = familyOf[ny * size + nx];
                    // open cells without a family (boss hall) contribute no border
                    if (neighbourFamily >= 0) masks[neighbourFamily] |= 1 << bit;
                }

                int cellFamily = familyOf[i];
                pieces.Clear();
                foreach (int b in order)
                {
                    if (b == cellFamily || masks[b] == 0 || zOrders[b] <= zOrders[cellFamily]) continue;
                    BorderSet? set = sets[b, cellFamily];
                    if (set is null) continue;
                    ResolvePieces(masks[b], set, pieces);
                }

                if (pieces.Count > 0) floor.SetBorderPieces(i, pieces);
            }
        }
    }

    /// <summary>Pieces resolved for ONE neighbouring family B around an open cell (bit set = B at
    /// that neighbour): concave corners swallow their two edges, remaining edges emit, then lone
    /// diagonals. Edges absent from the set are skipped.</summary>
    private static void ResolvePieces(int maskOfB, BorderSet set, List<ushort> pieces)
    {
        bool n = (maskOfB & 1) != 0, e = (maskOfB & 4) != 0, s = (maskOfB & 16) != 0, w = (maskOfB & 64) != 0;
        if (n && w) Add(set, "cnw", pieces);
        if (n && e) Add(set, "cne", pieces);
        if (s && w) Add(set, "csw", pieces);
        if (s && e) Add(set, "cse", pieces);
        if (n && !w && !e) Add(set, "n", pieces);
        if (s && !w && !e) Add(set, "s", pieces);
        if (w && !n && !s) Add(set, "w", pieces);
        if (e && !n && !s) Add(set, "e", pieces);
        if (!n && !w && (maskOfB & 128) != 0) Add(set, "dnw", pieces);
        if (!n && !e && (maskOfB & 2) != 0) Add(set, "dne", pieces);
        if (!s && !e && (maskOfB & 8) != 0) Add(set, "dse", pieces);
        if (!s && !w && (maskOfB & 32) != 0) Add(set, "dsw", pieces);
    }

    private static void Add(BorderSet set, string edge, List<ushort> pieces)
    {
        if (set.Edges.TryGetValue(edge, out ushort item)) pieces.Add(item);
    }
}
