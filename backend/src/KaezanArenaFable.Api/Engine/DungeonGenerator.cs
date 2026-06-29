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
    /// <summary>Chests that can NEVER be mimics (they always give a benefit; they may be normal or cursed).
    /// Strategic arena chests go here: the player is rewarded for detouring to claim them.</summary>
    public HashSet<(int X, int Y)> BenefitChests = [];
    public List<(int X, int Y)> Sanctuaries = []; // G-06: Echo altars (choice beat)

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
    public const ushort SanctuaryId = 2478; // G-06: gemmed ornate chest = Echo Sanctuary altar
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

        if (roomCount <= 1)
        {
            // SINGLE ARENA: one open room fills the floor (3-tile margin for the biome wall).
            const int margin = 3;
            floor.Rooms.Add(new Room { X = margin, Y = margin, W = size - 2 * margin, H = size - 2 * margin });
        }
        else
        {
            // place non-overlapping rooms
            for (var attempt = 0; attempt < GameConfig.RoomPlacementAttempts && floor.Rooms.Count < roomCount; attempt++)
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
        }

        // carve rooms, then erode each with a cellular-automata pass (H-02 B1) so the outline reads as an
        // organic blob instead of a literal rectangle. Erosion runs before ConnectRooms; corridors carve
        // centre-to-centre afterwards and punch through the eroded edge, so reachability is never lost.
        var singleArena = floor.Rooms.Count == 1;
        foreach (var room in floor.Rooms)
        {
            for (var yy = room.Y; yy < room.Y + room.H; yy++)
                for (var xx = room.X; xx < room.X + room.W; xx++)
                    floor.Blocked[yy * size + xx] = false;
            // single arena: cave erosion across the whole interior so the room reads irregular, not square.
            if (singleArena) ErodeArena(floor, room, rng);
            else ErodeRoom(floor, room, rng);
        }

        // G-07: connect rooms as a spatial spanning tree (nearest-neighbour from the entry) instead of
        // a spawn-order chain. A tree has real branches/dead-ends: the seam for risk/reward detours,
        // while still guaranteeing every room is reachable. One extra loop link keeps navigation open.
        var tree = ConnectRooms(floor, rng);
        AssignRoles(floor, tree, isBossFloor, rng);

        // H-03 (G3): carve a 1-tile-mouth "box" alcove into each combat room (corridors stay wide; the
        // only 1-tile choke is the alcove mouth). Runs after roles/POIs so it can dodge chests/altars.
        // Disabled by GameConfig.EnableBoxNiches for the open-map direction (the choke stalls mobbing).
        if (GameConfig.EnableBoxNiches) CarveBoxNiches(floor, rng);

        PaintTiles(floor, rng, biome);
        return floor;
    }

    private static int Manhattan(Room a, Room b) =>
        Math.Abs(a.CenterX - b.CenterX) + Math.Abs(a.CenterY - b.CenterY);

    /// <summary>
    /// H-02 (B1): erodes a freshly-carved rectangular room into an organic blob with a deterministic
    /// cellular-automata pass. Only a border band is seeded as rock (interior stays open); the classic
    /// 4-5 smoothing rule rounds the outline (cells outside the rect count as rock, so corners erode
    /// inward). A flood-fill from the centre then keeps just the connected component and re-opens the
    /// centre; corridors join centre-to-centre, so the room must stay a single reachable blob. Uses only
    /// the run rng in a fixed scan order; the CA is double-buffered so the result is order-independent.
    /// </summary>
    private static void ErodeRoom(DungeonFloor floor, Room room, Rng rng)
    {
        int w = room.W, h = room.H;
        // small rooms stay rectangular; erosion would pinch them shut and there is no box to give back.
        if (Math.Min(w, h) < GameConfig.OrganicRoomMinSize) return;

        // local rock grid (true = rock). Seed only the border band; the interior is left open so the
        // centre and a generous core never start blocked.
        var rock = new bool[w * h];
        for (var ly = 0; ly < h; ly++)
            for (var lx = 0; lx < w; lx++)
            {
                var edge = Math.Min(Math.Min(lx, w - 1 - lx), Math.Min(ly, h - 1 - ly));
                if (edge < GameConfig.OrganicSeedBand)
                    rock[ly * w + lx] = rng.Chance(GameConfig.OrganicFillProb);
            }

        // CA smoothing (4-5 rule). Out-of-rect neighbours count as rock so the blob pulls away from the
        // corners. Double-buffered -> independent of scan order (no rng here, fully deterministic).
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

        // connectivity: flood-fill the open cells reachable from the (forced-open) centre using 4-way
        // steps (matching nav); anything unreached becomes rock so the room is one blob around its centre.
        int cx = w / 2, cy = h / 2;
        rock[cy * w + cx] = false;
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

        // write back: reached cells open, everything else blocked (organic rock the painter turns to wall).
        var size = floor.W;
        for (var ly = 0; ly < h; ly++)
            for (var lx = 0; lx < w; lx++)
                floor.Blocked[(room.Y + ly) * size + (room.X + lx)] = !reached[ly * w + lx];
    }

    /// <summary>
    /// Single organic arena (feedback 2026-06-29): instead of eroding only the edge (which leaves the
    /// interior square), seed the WHOLE RECTANGLE with noise and smooth it with the same 4-5 CA rule.
    /// The result is an irregular cave with rock masses/pillars. A central core is forced open
    /// (guaranteed battle stage), and flood-fill from the center keeps only the connected component.
    /// Deterministic (run Rng, double-buffered CA).
    /// </summary>
    private static void ErodeArena(DungeonFloor floor, Room room, Rng rng)
    {
        int w = room.W, h = room.H;
        var rock = new bool[w * h];
        for (var ly = 0; ly < h; ly++)
            for (var lx = 0; lx < w; lx++)
                rock[ly * w + lx] = rng.Chance(GameConfig.ArenaFillProb);

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

    /// <summary>Nearest walkable cell to (x,y) within a room (spiral by Chebyshev ring). POIs anchored at
    /// a corner can land on rock after H-02 erosion; this snaps them onto open ground. Centre is the
    /// guaranteed fallback (always open).</summary>
    private static (int X, int Y) OpenCellInRoom(DungeonFloor floor, Room room, int x, int y)
    {
        var size = floor.W;
        var maxR = Math.Max(room.W, room.H);
        for (var r = 0; r <= maxR; r++)
            for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int nx = x + dx, ny = y + dy;
                    if (room.Contains(nx, ny) && !floor.Blocked[ny * size + nx]) return (nx, ny);
                }
        return (room.CenterX, room.CenterY);
    }

    /// <summary>
    /// Deterministic Prim spanning tree rooted at the entry (room 0): repeatedly carve the shortest
    /// edge from the connected set to an unconnected room (Manhattan between centres, ties broken by
    /// ascending index). Returns the adjacency used to route the entry-to-exit path. A single loop edge
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
    /// (ladder, or boss on the boss floor). Rooms off the entry-to-exit tree-path are detours and get the
    /// reward/risk roles first (treasure/elite/eco/evento/miniboss), so a fork means "safe ahead vs.
    /// loot behind". Deterministic: stable candidate order + the run rng.
    /// </summary>
    private static void AssignRoles(DungeonFloor floor, List<int>[] tree, bool isBossFloor, Rng rng)
    {
        var rooms = floor.Rooms;
        if (rooms.Count == 1)
        {
            AssignSingleArena(floor, rooms[0], isBossFloor, rng);
            return;
        }

        var entry = rooms[0];
        entry.Role = "entry";
        floor.Entry = (entry.CenterX, entry.CenterY);

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
            else if (room.Role == "elite") floor.Chests.Add(OpenCellInRoom(floor, room, room.X + 1, room.Y + 1));
            else if (room.Role == "sanctuary") floor.Sanctuaries.Add((room.CenterX, room.CenterY));
        }
        var mobRooms = rooms.Where(r => r.Role == "mob").ToList();
        for (var i = 0; i < GameConfig.ChestsPerFloor - 1 && mobRooms.Count > 0; i++)
        {
            var room = rng.Pick(mobRooms);
            floor.Chests.Add(OpenCellInRoom(floor, room, room.X + 1, room.Y + 1));
        }
    }

    /// <summary>
    /// Single-arena floor: the whole room is the stage. Entry sits on one side, exit on the opposite
    /// side (ladder, or the boss in the boss arena), and chests/altar are placed INSIDE the arena.
    /// The Kaeli mobs, clears, and claims everything there without navigating between rooms.
    /// The room is either "mob" (spawns the horde) or "boss".
    /// </summary>
    private static void AssignSingleArena(DungeonFloor floor, Room arena, bool isBossFloor, Rng rng)
    {
        arena.Role = isBossFloor ? "boss" : "mob";
        // Entry near the bottom edge; exit near the top edge (anchored to open ground after erosion).
        floor.Entry = OpenCellInRoom(floor, arena, arena.CenterX, arena.Y + arena.H - 3);

        // Boss arena: the exit is defeating the boss (no ladder, no chest, per 2026-06-29 feedback:
        // "a chest in that room does not make sense"). Only the chamber, boss, and escort.
        if (isBossFloor) return;

        // Horde floor: NO static chest and NO pre-placed ladder (2026-06-29 feedback, 8th pass).
        //  - the chest DROPS every N kills on the mob corpse (GameWorld.KillMonster), so the Kaeli claims it while luring;
        //  - the exit only appears as a TELEPORT on the last mob corpse when the room is cleared (GameWorld.KillMonster).
        // Only the Echo altar stays in the center here (the guaranteed choice beat).
        var midY = arena.Y + arena.H / 2;
        for (var s = 0; s < GameConfig.SanctuariesPerFloor; s++)
            floor.Sanctuaries.Add(OpenCellInRoom(floor, arena, arena.CenterX, midY)); // altar no centro
    }

    /// <summary>BFS on the (tree) adjacency: the set of room indices on the unique entry-to-exit path.</summary>
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
        // Corridor width is 2 or 3 tiles, never 1: a 1-sqm corridor pinches movement (no side-by-side
        // passing, reads as a crack). A square brush of `width` tiles per step guarantees that thickness
        // in every direction, including at the L-bend where a perpendicular-only widen would leave a
        // single-tile corner. Deterministic (run rng picks the width).
        var width = rng.Range(GameConfig.CorridorWidthMin, GameConfig.CorridorWidthMax);
        void Brush(int cx, int cy)
        {
            for (var dy = 0; dy < width; dy++)
                for (var dx = 0; dx < width; dx++)
                    if (floor.InBounds(cx + dx, cy + dy))
                        floor.Blocked[(cy + dy) * floor.W + cx + dx] = false;
        }
        Brush(x, y);
        while (x != b.CenterX || y != b.CenterY)
        {
            if (horizontalFirst && x != b.CenterX) x += Math.Sign(b.CenterX - x);
            else if (y != b.CenterY) y += Math.Sign(b.CenterY - y);
            else if (x != b.CenterX) x += Math.Sign(b.CenterX - x);
            Brush(x, y);
        }
    }

    /// <summary>
    /// H-03 (G3): carve a ~3x3 "box" alcove into each combat room with a single 1-tile mouth, so a player
    /// can over-lure a pile in the open room then retreat into the alcove and tank the mobs as they queue
    /// through the one-tile doorway. Corridors stay 2-3 wide (final decision): the only 1-tile choke is
    /// this mouth. The whole enclosure (back + sides + mouth wall) is owned inside the room rectangle, flush
    /// against one wall, so we never touch a corridor outside the rect. Each placement is BFS-validated and
    /// the first valid wall/slide commits. Deterministic: only the wall order draws from the run rng.
    /// </summary>
    private static void CarveBoxNiches(DungeonFloor floor, Rng rng)
    {
        var reserved = ReservedCells(floor);
        foreach (var room in floor.Rooms)
        {
            if (room.Role != "mob" && room.Role != "elite") continue;
            if (Math.Min(room.W, room.H) < GameConfig.BoxRoomMinSize) continue;
            // recomputed per room so it reflects walls committed by earlier rooms.
            var liveBefore = FloodFromEntry(floor);
            foreach (var (wall, fx, fy) in BoxCandidates(room, rng))
                if (TryPlaceBox(floor, room, wall, fx, fy, reserved, liveBefore)) break;
        }
    }

    /// <summary>Deterministic placement order for a room's box: walls shuffled by the run rng, each slid
    /// along its length centre-outward. Wall 0/1/2/3 = N/S/W/E flush; the footprint hugs that wall.</summary>
    private static IEnumerable<(int wall, int fx, int fy)> BoxCandidates(Room room, Rng rng)
    {
        var fs = GameConfig.BoxInteriorSize + 2;
        var walls = new List<int> { 0, 1, 2, 3 };
        rng.Shuffle(walls);
        foreach (var wall in walls)
        {
            var along = wall is 0 or 1 ? room.W : room.H;
            var maxOff = along - fs;
            if (maxOff < 0) continue;
            foreach (var off in CenterOut(maxOff))
            {
                (int fx, int fy) = wall switch
                {
                    0 => (room.X + off, room.Y),                   // N flush (mouth faces south)
                    1 => (room.X + off, room.Y + room.H - fs),     // S flush (mouth faces north)
                    2 => (room.X, room.Y + off),                   // W flush (mouth faces east)
                    _ => (room.X + room.W - fs, room.Y + off),     // E flush (mouth faces west)
                };
                yield return (wall, fx, fy);
            }
        }
    }

    /// <summary>0..maxOff yielded centre-first then alternating outward, so the alcove favours the middle of
    /// the wall (away from the corners where corridors usually punch in).</summary>
    private static IEnumerable<int> CenterOut(int maxOff)
    {
        var c = maxOff / 2;
        yield return c;
        for (var d = 1; c - d >= 0 || c + d <= maxOff; d++)
        {
            if (c - d >= 0) yield return c - d;
            if (c + d <= maxOff) yield return c + d;
        }
    }

    /// <summary>
    /// Tries one box placement flush against <paramref name="wall"/> at footprint origin (fx,fy). The
    /// footprint is <c>BoxInteriorSize+2</c> square: a full border ring of wall around a bxb open interior,
    /// with a centred <c>BoxMouthWidth</c>-tile gap on the interior-facing edge. Commits (returns true) only
    /// when the footprint sits inside the rect, covers no POI, the mouth opens onto live room floor, the
    /// interior ends up reachable, and no previously-reachable cell is orphaned by the new walls. Otherwise
    /// reverts every cell it touched and returns false.
    /// </summary>
    private static bool TryPlaceBox(
        DungeonFloor floor, Room room, int wall, int fx, int fy,
        HashSet<(int, int)> reserved, bool[] liveBefore)
    {
        var size = floor.W;
        var fs = GameConfig.BoxInteriorSize + 2;
        var mouthW = GameConfig.BoxMouthWidth;

        // footprint must lie fully inside the room rect: every wall we set is then ours to own (no corridor
        // outside the rect is ever touched).
        if (fx < room.X || fy < room.Y || fx + fs > room.X + room.W || fy + fs > room.Y + room.H)
            return false;

        var mouthStart = (fs - mouthW) / 2;
        (int dx, int dy) beyond = wall switch { 0 => (0, 1), 1 => (0, -1), 2 => (1, 0), _ => (-1, 0) };
        bool IsMouth(int lx, int ly) => wall switch
        {
            0 => ly == fs - 1 && lx >= mouthStart && lx < mouthStart + mouthW, // south edge
            1 => ly == 0 && lx >= mouthStart && lx < mouthStart + mouthW,       // north edge
            2 => lx == fs - 1 && ly >= mouthStart && ly < mouthStart + mouthW,  // east edge
            _ => lx == 0 && ly >= mouthStart && ly < mouthStart + mouthW,        // west edge
        };

        var ring = new List<int>();   // becomes wall
        var open = new List<int>();   // interior + mouth, becomes floor
        var mouth = new List<(int gx, int gy)>();
        for (var ly = 0; ly < fs; ly++)
            for (var lx = 0; lx < fs; lx++)
            {
                int gx = fx + lx, gy = fy + ly;
                if (reserved.Contains((gx, gy))) return false; // never bury a POI under the box
                var border = lx == 0 || lx == fs - 1 || ly == 0 || ly == fs - 1;
                var idx = gy * size + gx;
                if (border && IsMouth(lx, ly)) { mouth.Add((gx, gy)); open.Add(idx); }
                else if (border) ring.Add(idx);
                else open.Add(idx);
            }

        // the mouth has to open onto live room floor, else the alcove would be sealed off.
        var anyBeyondLive = false;
        foreach (var (gx, gy) in mouth)
        {
            int bx = gx + beyond.dx, by = gy + beyond.dy;
            if (floor.InBounds(bx, by) && liveBefore[by * size + bx]) { anyBeyondLive = true; break; }
        }
        if (!anyBeyondLive) return false;

        // apply tentatively (record originals so we can revert a rejected placement).
        var openWas = new bool[open.Count];
        for (var k = 0; k < open.Count; k++) { openWas[k] = floor.Blocked[open[k]]; floor.Blocked[open[k]] = false; }
        var ringWas = new bool[ring.Count];
        for (var k = 0; k < ring.Count; k++) { ringWas[k] = floor.Blocked[ring[k]]; floor.Blocked[ring[k]] = true; }

        // BFS sanity: the alcove interior must be reachable, and the new walls must not strand any cell that
        // was reachable before (e.g. a corridor running through this corner of the room).
        var liveAfter = FloodFromEntry(floor);
        var ringSet = new HashSet<int>(ring);
        var ok = true;
        foreach (var i in open)
            if (!liveAfter[i]) { ok = false; break; }
        if (ok)
            for (var i = 0; i < liveBefore.Length; i++)
                if (liveBefore[i] && !liveAfter[i] && !ringSet.Contains(i)) { ok = false; break; }

        if (!ok)
        {
            for (var k = 0; k < ring.Count; k++) floor.Blocked[ring[k]] = ringWas[k];
            for (var k = 0; k < open.Count; k++) floor.Blocked[open[k]] = openWas[k];
            return false;
        }
        return true;
    }

    /// <summary>4-way flood of walkable cells reachable from the floor entry: the connectivity oracle the
    /// box carving checks against.</summary>
    private static bool[] FloodFromEntry(DungeonFloor floor)
    {
        var size = floor.W;
        var live = new bool[size * size];
        var (ex, ey) = floor.Entry;
        if (floor.IsBlocked(ex, ey)) return live;
        var stack = new Stack<int>();
        var start = ey * size + ex;
        live[start] = true;
        stack.Push(start);
        Span<(int dx, int dy)> steps = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            int x = idx % size, y = idx / size;
            foreach (var (dx, dy) in steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                var ni = ny * size + nx;
                if (live[ni] || floor.Blocked[ni]) continue;
                live[ni] = true;
                stack.Push(ni);
            }
        }
        return live;
    }

    private static void PaintTiles(DungeonFloor floor, Rng rng, BiomeDef biome)
    {
        var size = floor.W;

        // Pass 1: ground + walls. A blocked cell that borders walkable area is an edge wall (oriented
        // sprite via ClassifyWall); a fully-enclosed blocked cell is bedrock: opaque rock + the solid
        // corner piece, so the map's negative reads as a massif instead of a hard-edged black void.
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                if (!floor.Blocked[i])
                {
                    var bossRoom = floor.Rooms.Any(r => r.Role == "boss" && r.Contains(x, y));
                    floor.Ground[i] = bossRoom ? rng.Pick(biome.BossGround) : rng.Pick(biome.Ground);
                    continue;
                }

                var touchesFloor = false;
                for (var dy = -1; dy <= 1 && !touchesFloor; dy++)
                    for (var dx = -1; dx <= 1 && !touchesFloor; dx++)
                        if (floor.InBounds(x + dx, y + dy) && !floor.Blocked[(y + dy) * size + x + dx])
                            touchesFloor = true;

                if (touchesFloor)
                {
                    floor.Ground[i] = rng.Pick(biome.Ground); // shows through alpha (stone) walls
                    floor.Wall[i] = ClassifyWall(floor, x, y, biome);
                }
                else
                {
                    // bedrock fill: no rng (a fixed massif tile keeps the rock reading uniform/solid).
                    floor.Ground[i] = biome.Bedrock;
                    floor.Wall[i] = biome.WallCorner;
                }
            }
        }

        // Pass 2: ambient decor/accent, clustered inside rooms only (corridors stay clean). Accent (e.g.
        // lava) pools first so it reads as terrain, then ambient props; both on the non-blocking Decor
        // layer, skipping POI tiles so chests/altars/ladder stay legible.
        var reserved = ReservedCells(floor);
        foreach (var room in floor.Rooms)
        {
            PaintClusters(floor, room, rng, biome.Accent, biome.AccentChance, GameConfig.AccentClusterRadius, reserved);
            PaintClusters(floor, room, rng, biome.Decor, biome.DecorChance, GameConfig.DecorClusterRadius, reserved);
        }
    }

    /// <summary>
    /// Scatters <paramref name="palette"/> tiles into a room as a few blobs instead of per-cell noise:
    /// the cluster count scales with room area x <paramref name="chance"/>, each blob stamps a radius
    /// with a chance falloff from its centre. Deterministic (run rng only). Skips blocked, reserved
    /// (POI) and already-decorated cells so props read as grouped ambience, never as obstacles.
    /// </summary>
    private static void PaintClusters(
        DungeonFloor floor, Room room, Rng rng, ushort[] palette, double chance, int radius,
        HashSet<(int X, int Y)> reserved)
    {
        if (palette.Length == 0 || chance <= 0) return;
        var size = floor.W;
        var clusters = (int)Math.Round(room.W * room.H * chance * GameConfig.DecorDensityScale);
        for (var c = 0; c < clusters; c++)
        {
            var cx = rng.Range(room.X, room.X + room.W - 1);
            var cy = rng.Range(room.Y, room.Y + room.H - 1);
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var x = cx + dx;
                    var y = cy + dy;
                    if (!room.Contains(x, y)) continue;
                    var i = y * size + x;
                    if (floor.Blocked[i] || floor.Decor[i] != 0 || reserved.Contains((x, y))) continue;
                    var ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (ring > 0 && !rng.Chance(1.0 - ring * GameConfig.ClusterFalloff)) continue;
                    floor.Decor[i] = rng.Pick(palette);
                }
            }
        }
    }

    /// <summary>
    /// Picks the wall piece from the 8-neighbourhood of open floor. Floor on the N/S axis only means an
    /// E-W wall (WallH); on the E/W axis only an N-S wall (WallV). Both axes open is either a straight
    /// pass-through / T- / cross-junction (WallPole) or, when it's a single perpendicular pair, a concave
    /// L-corner: there the solid corner piece fills the cell flush where a pole would leave a "tooth".
    /// A cell open only on a diagonal is an outer (convex) corner: the solid corner again. With bedrock
    /// fill the truly-enclosed cells never reach here, so every classified cell is a real edge wall.
    /// </summary>
    private static ushort ClassifyWall(DungeonFloor floor, int x, int y, BiomeDef biome)
    {
        var size = floor.W;
        bool Open(int dx, int dy)
        {
            var nx = x + dx;
            var ny = y + dy;
            return floor.InBounds(nx, ny) && !floor.Blocked[ny * size + nx];
        }
        var openN = Open(0, -1);
        var openS = Open(0, 1);
        var openE = Open(1, 0);
        var openW = Open(-1, 0);
        var vertAxis = openN || openS;   // floor above/below -> wall runs horizontally
        var horizAxis = openE || openW;  // floor left/right  -> wall runs vertically

        if (vertAxis && horizAxis)
        {
            // straight corridor through or a 3-/4-way junction -> pole; a lone perpendicular pair (an L)
            // is a concave corner -> solid corner so no nub/tooth protrudes into the opening.
            var straight = (openN && openS) || (openE && openW);
            return straight ? biome.WallPole : biome.WallCorner;
        }
        if (vertAxis) return biome.WallH;
        if (horizAxis) return biome.WallV;
        return biome.WallCorner; // only a diagonal neighbour is open -> outer corner
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
