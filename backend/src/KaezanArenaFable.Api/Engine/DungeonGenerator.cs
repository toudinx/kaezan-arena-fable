using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

public sealed class Room
{
    public int X, Y, W, H;
    public string Role = "mob"; // entry | mob | treasure | ladder | boss
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

        // connect rooms in a chain (plus one extra loop link) with L corridors
        for (var i = 1; i < floor.Rooms.Count; i++)
            CarveCorridor(floor, floor.Rooms[i - 1], floor.Rooms[i], rng);
        if (floor.Rooms.Count > 3)
            CarveCorridor(floor, floor.Rooms[0], floor.Rooms[rng.Range(2, floor.Rooms.Count - 1)], rng);

        // assign roles: entry = first, farthest = ladder/boss, one treasure room
        var entry = floor.Rooms[0];
        entry.Role = "entry";
        floor.Entry = (entry.CenterX, entry.CenterY);

        var farthest = floor.Rooms.Skip(1)
            .OrderByDescending(r => Math.Abs(r.CenterX - entry.CenterX) + Math.Abs(r.CenterY - entry.CenterY))
            .First();
        farthest.Role = isBossFloor ? "boss" : "ladder";
        if (!isBossFloor) floor.LadderDown = (farthest.CenterX, farthest.CenterY);

        var treasureCandidates = floor.Rooms.Where(r => r.Role == "mob").ToList();
        if (treasureCandidates.Count > 0)
            rng.Pick(treasureCandidates).Role = "treasure";

        // chests: in treasure room + random mob room corners
        foreach (var room in floor.Rooms.Where(r => r.Role == "treasure"))
            floor.Chests.Add((room.CenterX, room.CenterY));
        var mobRooms = floor.Rooms.Where(r => r.Role == "mob").ToList();
        for (var i = 0; i < GameConfig.ChestsPerFloor - 1 && mobRooms.Count > 0; i++)
        {
            var room = rng.Pick(mobRooms);
            floor.Chests.Add((room.X + 1, room.Y + 1));
        }

        PaintTiles(floor, rng, biome);
        return floor;
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
        return set;
    }
}
