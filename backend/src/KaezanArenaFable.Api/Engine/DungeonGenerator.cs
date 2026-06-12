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
/// Seeded rooms-and-corridors generator. Visual ids reference the Tibia object
/// sprites exported by tools/AssetExtractor (cave biome).
/// </summary>
public static class DungeonGenerator
{
    // tibia object ids (see tools/AssetExtractor/content-config.json semantic groups)
    private static readonly ushort[] CaveGround = [351, 352, 353, 354, 355];
    private static readonly ushort[] StoneGround = [416, 418];
    private const ushort WallPole = 356;       // dirt wall variants; orientation refined client-side
    private const ushort WallHorizontal = 357;
    private const ushort WallVertical = 358;
    private static readonly ushort[] CaveDecor = [1047, 1048, 1772, 1773, 1774, 1775];
    public const ushort ChestId = 2472;
    public const ushort LadderDownId = 386;

    public static DungeonFloor Generate(Rng rng, int floorIndex, bool isBossFloor)
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

        PaintTiles(floor, rng, isBossFloor);
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

    private static void PaintTiles(DungeonFloor floor, Rng rng, bool isBossFloor)
    {
        var size = floor.W;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                if (!floor.Blocked[i])
                {
                    var bossRoom = floor.Rooms.FirstOrDefault(r => r.Role == "boss" && r.Contains(x, y));
                    floor.Ground[i] = bossRoom is not null ? rng.Pick(StoneGround) : rng.Pick(CaveGround);
                    if (rng.Chance(0.025) && floor.Rooms.Any(r => r.Contains(x, y)))
                        floor.Decor[i] = rng.Pick(CaveDecor);
                    continue;
                }

                // blocked cell: draw a wall sprite only when bordering walkable area
                var touchesFloor = false;
                for (var dy = -1; dy <= 1 && !touchesFloor; dy++)
                    for (var dx = -1; dx <= 1 && !touchesFloor; dx++)
                        if (floor.InBounds(x + dx, y + dy) && !floor.Blocked[(y + dy) * size + x + dx])
                            touchesFloor = true;
                if (!touchesFloor) continue;

                floor.Ground[i] = rng.Pick(CaveGround);
                var openSouth = floor.InBounds(x, y + 1) && !floor.Blocked[(y + 1) * size + x];
                var openNorth = floor.InBounds(x, y - 1) && !floor.Blocked[(y - 1) * size + x];
                var openEast = floor.InBounds(x + 1, y) && !floor.Blocked[y * size + x + 1];
                var openWest = floor.InBounds(x - 1, y) && !floor.Blocked[y * size + x - 1];
                floor.Wall[i] = (openNorth || openSouth) switch
                {
                    true when openEast || openWest => WallPole,
                    true => WallHorizontal,
                    _ => WallVertical
                };
            }
        }
    }
}
