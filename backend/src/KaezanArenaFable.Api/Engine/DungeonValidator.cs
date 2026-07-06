namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Fails LOUDLY when a generated floor is unplayable. Runs as the last step of
/// <see cref="DungeonGenerator.Generate"/>: an invalid layout must abort run creation with a clear
/// message instead of shipping a soft-locked run. Pure BFS over the final Blocked grid — consumes
/// no rng, so it can never perturb determinism.
/// </summary>
public static class DungeonValidator
{
    public static void Validate(DungeonFloor floor)
    {
        var size = floor.W;
        if (floor.IsBlocked(floor.Entry.X, floor.Entry.Y))
            throw Fail(floor, $"entry ({floor.Entry.X},{floor.Entry.Y}) is blocked");

        var live = Flood(floor);
        int open = 0, reachable = 0;
        for (var i = 0; i < floor.Blocked.Length; i++)
        {
            if (floor.Blocked[i]) continue;
            open++;
            if (live[i]) reachable++;
        }
        if (open == 0) throw Fail(floor, "no open cells");
        if (reachable != open)
            throw Fail(floor, $"disconnected: {open - reachable} open cell(s) unreachable from entry");

        foreach (var (x, y) in floor.Chests)
            if (floor.IsBlocked(x, y) || !live[y * size + x])
                throw Fail(floor, $"chest at ({x},{y}) unreachable");
        foreach (var (x, y) in floor.Sanctuaries)
            if (floor.IsBlocked(x, y) || !live[y * size + x])
                throw Fail(floor, $"sanctuary at ({x},{y}) unreachable");
        if (floor.LadderDown is { } ladder && (floor.IsBlocked(ladder.X, ladder.Y) || !live[ladder.Y * size + ladder.X]))
            throw Fail(floor, $"ladder at ({ladder.X},{ladder.Y}) unreachable");
    }

    private static InvalidOperationException Fail(DungeonFloor f, string reason) =>
        new($"generated floor {f.Index} invalid: {reason} (W={f.W} H={f.H} rooms={f.Rooms.Count})");

    private static bool[] Flood(DungeonFloor floor)
    {
        var size = floor.W;
        var live = new bool[size * floor.H];
        var (ex, ey) = floor.Entry;
        var stack = new Stack<int>();
        live[ey * size + ex] = true;
        stack.Push(ey * size + ex);
        Span<(int dx, int dy)> steps = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            int x = idx % size, y = idx / size;
            foreach (var (dx, dy) in steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= size || ny < 0 || ny >= floor.H) continue;
                var ni = ny * size + nx;
                if (live[ni] || floor.Blocked[ni]) continue;
                live[ni] = true;
                stack.Push(ni);
            }
        }
        return live;
    }
}
