using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

public sealed class Room
{
    public int X, Y, W, H;
    // G-07 taxonomy: entry | mob (combate) | treasure | elite | hazard | miniboss | sanctuary | ladder | boss
    public string Role = "mob";
    public int CenterX => X + W / 2;
    public int CenterY => Y + H / 2;
    public bool Contains(int px, int py) => px >= X && px < X + W && py >= Y && py < Y + H;
}

public sealed class DungeonFloor
{
    public required int Index;
    public required int W;
    public required int H;
    public required ushort[] Ground;
    public required ushort[] Wall;     // 0 = none
    public required ushort[] Decor;    // 0 = none
    public required bool[] Blocked;
    public required List<Room> Rooms;
    public (int X, int Y) Entry;
    public (int X, int Y)? LadderDown;
    public List<(int X, int Y)> Chests = [];
    public List<(int X, int Y)> Sanctuaries = []; // G-06: altares de Eco (beat de escolha)

    public bool InBounds(int x, int y) => x >= 0 && x < W && y >= 0 && y < H;
    public bool IsBlocked(int x, int y) => !InBounds(x, y) || Blocked[y * W + x];
}

/// <summary>
/// Seeded rooms-and-corridors generator. Visual ids come from a <see cref="BiomeDef"/> (per tier)
/// so each dungeon themes its ground/walls/decor; see <see cref="Biomes"/>.
/// </summary>
public static class DungeonGenerator
{
    public const ushort ChestId = 2472;
    public const ushort SanctuaryId = 2478; // G-06: baú ornado de gemas = altar do Santuário de Eco
    public const ushort LadderDownId = 386;

    public static DungeonFloor Generate(Rng rng, int floorIndex, bool isBossFloor, BiomeDef biome)
    {
        var size = isBossFloor ? GameConfig.Floor2Size : GameConfig.Floor1Size;
        var roomCount = isBossFloor ? GameConfig.RoomsFloor2 : GameConfig.RoomsFloor1;

        var floor = new DungeonFloor
        {
            Index = floorIndex,
            W = size,
            H = size,
            Ground = new ushort[size * size],
            Wall = new ushort[size * size],
            Decor = new ushort[size * size],
            Blocked = new bool[size * size],
            Rooms = []
        };
        Array.Fill(floor.Blocked, true);

        // place non-overlapping rooms
        for (var attempt = 0; attempt < 200 && floor.Rooms.Count < roomCount; attempt++)
        {
            var w = rng.Range(GameConfig.RoomMin, GameConfig.RoomMax);
            var h = rng.Range(GameConfig.RoomMin, GameConfig.RoomMax);
            if (isBossFloor && floor.Rooms.Count == roomCount - 1) { w = 11; h = 9; } // boss hall
            var x = rng.Range(2, size - w - 2);
            var y = rng.Range(2, size - h - 2);
            var candidate = new Room { X = x, Y = y, W = w, H = h };
            var overlaps = floor.Rooms.Any(r =>
                x < r.X + r.W + 2 && x + w + 2 > r.X && y < r.Y + r.H + 2 && y + h + 2 > r.Y);
            if (!overlaps) floor.Rooms.Add(candidate);
        }

        // carve rooms
        foreach (var room in floor.Rooms)
            for (var yy = room.Y; yy < room.Y + room.H; yy++)
                for (var xx = room.X; xx < room.X + room.W; xx++)
                    floor.Blocked[yy * size + xx] = false;

        // G-07: connect rooms as a spatial spanning tree (nearest-neighbour from the entry) instead of
        // a spawn-order chain. A tree has real branches/dead-ends — the seam for risk/reward detours —
        // while still guaranteeing every room is reachable. One extra loop link keeps navigation open.
        var tree = ConnectRooms(floor, rng);
        AssignRoles(floor, tree, isBossFloor, rng);

        PaintTiles(floor, rng, biome);
        return floor;
    }

    private static int Manhattan(Room a, Room b) =>
        Math.Abs(a.CenterX - b.CenterX) + Math.Abs(a.CenterY - b.CenterY);

    /// <summary>
    /// Deterministic Prim spanning tree rooted at the entry (room 0): repeatedly carve the shortest
    /// edge from the connected set to an unconnected room (Manhattan between centres, ties broken by
    /// ascending index). Returns the adjacency used to route the entry→exit path. A single loop edge
    /// is carved for navigability but kept out of the adjacency so routing stays on the tree.
    /// </summary>
    private static List<int>[] ConnectRooms(DungeonFloor floor, Rng rng)
    {
        var n = floor.Rooms.Count;
        var adj = new List<int>[n];
        for (var i = 0; i < n; i++) adj[i] = [];
        if (n <= 1) return adj;

        var inTree = new bool[n];
        inTree[0] = true;
        for (var added = 1; added < n; added++)
        {
            int bestFrom = -1, bestTo = -1, bestDist = int.MaxValue;
            for (var i = 0; i < n; i++)
            {
                if (!inTree[i]) continue;
                for (var j = 0; j < n; j++)
                {
                    if (inTree[j]) continue;
                    var d = Manhattan(floor.Rooms[i], floor.Rooms[j]);
                    if (d < bestDist) { bestDist = d; bestFrom = i; bestTo = j; }
                }
            }
            inTree[bestTo] = true;
            adj[bestFrom].Add(bestTo);
            adj[bestTo].Add(bestFrom);
            CarveCorridor(floor, floor.Rooms[bestFrom], floor.Rooms[bestTo], rng);
        }

        if (n > 3)
            CarveCorridor(floor, floor.Rooms[0], floor.Rooms[rng.Range(2, n - 1)], rng);
        return adj;
    }

    /// <summary>
    /// G-07: assign room types using the graph. Entry = room 0; the farthest room is the exit
    /// (ladder, or boss on the boss floor). Rooms off the entry→exit tree-path are detours and get the
    /// reward/risk roles first (treasure/elite/eco/evento/miniboss), so a fork means "safe ahead vs.
    /// loot behind". Deterministic: stable candidate order + the run rng.
    /// </summary>
    private static void AssignRoles(DungeonFloor floor, List<int>[] tree, bool isBossFloor, Rng rng)
    {
        var rooms = floor.Rooms;
        var entry = rooms[0];
        entry.Role = "entry";
        floor.Entry = (entry.CenterX, entry.CenterY);
        if (rooms.Count == 1) return;

        var exitIdx = 1;
        var bestDist = -1;
        for (var i = 1; i < rooms.Count; i++)
        {
            var d = Manhattan(entry, rooms[i]);
            if (d > bestDist) { bestDist = d; exitIdx = i; }
        }
        var exit = rooms[exitIdx];
        exit.Role = isBossFloor ? "boss" : "ladder";
        if (!isBossFloor) floor.LadderDown = (exit.CenterX, exit.CenterY);

        var onPath = PathRooms(tree, 0, exitIdx);
        var detours = new List<Room>();
        var mainMid = new List<Room>();
        for (var i = 1; i < rooms.Count; i++)
        {
            if (i == exitIdx) continue;
            (onPath.Contains(i) ? mainMid : detours).Add(rooms[i]);
        }

        // detours first: rewards sit behind a fork off the critical path; main-path rooms fill in after.
        var candidates = new List<Room>(detours);
        candidates.AddRange(mainMid);
        var next = 0;
        Room? Take() => next < candidates.Count ? candidates[next++] : null;

        if (isBossFloor)
        {
            // pre-boss floor stays lean: a treasure cache and the Eco sanctuary beat, rest combat.
            if (Take() is { } t) t.Role = "treasure";
            for (var s = 0; s < GameConfig.SanctuariesPerFloor; s++)
                if (Take() is { } sr) sr.Role = "sanctuary";
        }
        else
        {
            if (Take() is { } t) t.Role = "treasure";
            if (Take() is { } e) e.Role = "elite";
            for (var s = 0; s < GameConfig.SanctuariesPerFloor; s++)
                if (Take() is { } sr) sr.Role = "sanctuary";
            if (Take() is { } h) h.Role = "hazard";
            if (rooms.Count >= GameConfig.MiniBossMinRooms && Take() is { } mb) mb.Role = "miniboss";
        }
        // anything left keeps the default "mob" (combat) role.

        // POIs from roles: chest in treasure + elite rooms (the detour loot), Eco altars in sanctuaries,
        // plus a couple of random extra caches in combat rooms.
        foreach (var room in rooms)
        {
            if (room.Role == "treasure") floor.Chests.Add((room.CenterX, room.CenterY));
            else if (room.Role == "elite") floor.Chests.Add((room.X + 1, room.Y + 1));
            else if (room.Role == "sanctuary") floor.Sanctuaries.Add((room.CenterX, room.CenterY));
        }
        var mobRooms = rooms.Where(r => r.Role == "mob").ToList();
        for (var i = 0; i < GameConfig.ChestsPerFloor - 1 && mobRooms.Count > 0; i++)
        {
            var room = rng.Pick(mobRooms);
            floor.Chests.Add((room.X + 1, room.Y + 1));
        }
    }

    /// <summary>BFS on the (tree) adjacency: the set of room indices on the unique entry→exit path.</summary>
    private static HashSet<int> PathRooms(List<int>[] tree, int start, int goal)
    {
        var prev = new int[tree.Length];
        Array.Fill(prev, -1);
        var visited = new bool[tree.Length];
        var queue = new Queue<int>();
        queue.Enqueue(start);
        visited[start] = true;
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) break;
            foreach (var nb in tree[cur])
                if (!visited[nb]) { visited[nb] = true; prev[nb] = cur; queue.Enqueue(nb); }
        }
        var path = new HashSet<int>();
        for (var at = goal; at != -1; at = prev[at]) path.Add(at);
        return path;
    }

    private static void CarveCorridor(DungeonFloor floor, Room a, Room b, Rng rng)
    {
        int x = a.CenterX, y = a.CenterY;
        var horizontalFirst = rng.Chance(0.5);
        while (x != b.CenterX || y != b.CenterY)
        {
            if (horizontalFirst && x != b.CenterX) x += Math.Sign(b.CenterX - x);
            else if (y != b.CenterY) y += Math.Sign(b.CenterY - y);
            else if (x != b.CenterX) x += Math.Sign(b.CenterX - x);
            floor.Blocked[y * floor.W + x] = false;
            // widen corridors to 2 tiles for smoother movement
            if (floor.InBounds(x + 1, y)) floor.Blocked[y * floor.W + x + 1] = false;
        }
    }

    private static void PaintTiles(DungeonFloor floor, Rng rng, BiomeDef biome)
    {
        var size = floor.W;
        var reserved = ReservedCells(floor);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                if (!floor.Blocked[i])
                {
                    var inRoom = floor.Rooms.FirstOrDefault(r => r.Contains(x, y));
                    var bossRoom = inRoom is { Role: "boss" };
                    floor.Ground[i] = bossRoom ? rng.Pick(biome.BossGround) : rng.Pick(biome.Ground);

                    // accent (e.g. lava) then ambient decor — both purely cosmetic (Decor layer
                    // never blocks), and skipped on entry/ladder/chest tiles so POIs stay readable.
                    var accentRoll = rng.Chance(biome.AccentChance);
                    var decorRoll = rng.Chance(biome.DecorChance);
                    if (inRoom is not null && !reserved.Contains((x, y)))
                    {
                        if (biome.Accent.Length > 0 && accentRoll) floor.Decor[i] = rng.Pick(biome.Accent);
                        else if (biome.Decor.Length > 0 && decorRoll) floor.Decor[i] = rng.Pick(biome.Decor);
                    }
                    continue;
                }

                // blocked cell: draw a wall sprite only when bordering walkable area
                var touchesFloor = false;
                for (var dy = -1; dy <= 1 && !touchesFloor; dy++)
                    for (var dx = -1; dx <= 1 && !touchesFloor; dx++)
                        if (floor.InBounds(x + dx, y + dy) && !floor.Blocked[(y + dy) * size + x + dx])
                            touchesFloor = true;
                if (!touchesFloor) continue;

                floor.Ground[i] = rng.Pick(biome.Ground); // shows through alpha (stone) walls
                floor.Wall[i] = ClassifyWall(floor, x, y, biome);
            }
        }
    }

    /// <summary>
    /// Picks the wall piece from the 4-neighbourhood of open floor: floor on the N/S axis means an
    /// E–W wall (WallH), floor on the E/W axis means an N–S wall (WallV), floor on both axes is a
    /// junction (WallPole), and a cell with floor only on a diagonal is an outer corner (WallCorner).
    /// The corner case is what removes the "teeth" at room quoins that a flat H/V choice leaves.
    /// </summary>
    private static ushort ClassifyWall(DungeonFloor floor, int x, int y, BiomeDef biome)
    {
        var size = floor.W;
        var openN = floor.InBounds(x, y - 1) && !floor.Blocked[(y - 1) * size + x];
        var openS = floor.InBounds(x, y + 1) && !floor.Blocked[(y + 1) * size + x];
        var openE = floor.InBounds(x + 1, y) && !floor.Blocked[y * size + x + 1];
        var openW = floor.InBounds(x - 1, y) && !floor.Blocked[y * size + x - 1];
        var vertAxis = openN || openS;   // floor above/below → wall runs horizontally
        var horizAxis = openE || openW;  // floor left/right  → wall runs vertically

        if (vertAxis && horizAxis) return biome.WallPole;
        if (vertAxis) return biome.WallH;
        if (horizAxis) return biome.WallV;
        return biome.WallCorner; // only a diagonal neighbour is open → outer corner
    }

    /// <summary>Cells that should never receive decor/accent so their POI sprite stays clear.</summary>
    private static HashSet<(int X, int Y)> ReservedCells(DungeonFloor floor)
    {
        var set = new HashSet<(int, int)> { floor.Entry };
        if (floor.LadderDown is { } ladder) set.Add(ladder);
        foreach (var chest in floor.Chests) set.Add(chest);
        foreach (var sanctuary in floor.Sanctuaries) set.Add(sanctuary);
        return set;
    }
}
