using System.Text.Json;

namespace KaezanArenaFable.Api.Content;

public sealed record PrefabPoi(int X, int Y);

public sealed record PrefabDef(
    string Id, string Role, int Tier, string Theme, int W, int H,
    ushort[] Ground, ushort[] Wall, ushort[] Decor, bool[] Blocked,
    PrefabPoi[] Mouths, PrefabPoi[] Chests, string[] SpawnTheme);

public static class PrefabRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ValidRoles = new HashSet<string>(StringComparer.Ordinal)
    {
        "mob",
        "treasure",
        "boss"
    };

    private static IReadOnlyList<PrefabDef> all = Array.Empty<PrefabDef>();
    private static IReadOnlyDictionary<int, IReadOnlyList<PrefabDef>> byTier =
        new Dictionary<int, IReadOnlyList<PrefabDef>>();

    public static IReadOnlyList<PrefabDef> All => all;

    public static void LoadFrom(string dir, Func<string, bool> speciesExists)
    {
        if (!Directory.Exists(dir))
        {
            all = Array.Empty<PrefabDef>();
            byTier = new Dictionary<int, IReadOnlyList<PrefabDef>>();
            return;
        }

        string[] files = Directory.GetFiles(dir, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        List<PrefabDef> loaded = new List<PrefabDef>();
        foreach (string file in files)
        {
            loaded.Add(LoadOne(file, speciesExists));
        }

        all = loaded
            .OrderBy(prefab => prefab.Id, StringComparer.Ordinal)
            .ToArray();
        byTier = all
            .GroupBy(prefab => prefab.Tier)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PrefabDef>)group
                    .OrderBy(prefab => prefab.Id, StringComparer.Ordinal)
                    .ToArray());
    }

    public static IReadOnlyList<PrefabDef> ForTier(int tier) =>
        byTier.TryGetValue(tier, out IReadOnlyList<PrefabDef>? prefabs)
            ? prefabs
            : Array.Empty<PrefabDef>();

    private static PrefabDef LoadOne(string path, Func<string, bool> speciesExists)
    {
        string json = File.ReadAllText(path);
        PrefabDto? dto = JsonSerializer.Deserialize<PrefabDto>(json, JsonOptions);
        if (dto is null)
        {
            throw Invalid(path, "JSON root is empty");
        }

        ValidateHeader(path, dto);
        bool[] blocked = ConvertBlocked(path, dto.Blocked, dto.W * dto.H);
        ValidateGrids(path, dto, blocked.Length);
        PrefabPoi[] mouths = dto.Mouths ?? Array.Empty<PrefabPoi>();
        PrefabPoi[] chests = dto.Chests ?? Array.Empty<PrefabPoi>();
        string[] spawnTheme = dto.SpawnTheme ?? Array.Empty<string>();
        ValidateMouths(path, dto.W, dto.H, blocked, mouths);
        ValidateConnected(path, dto.W, dto.H, blocked);
        ValidateSpawnTheme(path, dto.Role, spawnTheme, speciesExists);

        return new PrefabDef(
            dto.Id!,
            dto.Role!,
            dto.Tier,
            dto.Theme ?? "",
            dto.W,
            dto.H,
            dto.Ground!,
            dto.Wall!,
            dto.Decor!,
            blocked,
            mouths,
            chests,
            spawnTheme);
    }

    private static void ValidateHeader(string path, PrefabDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id) || !dto.Id.StartsWith("prefab:", StringComparison.Ordinal))
        {
            throw Invalid(path, "id must start with 'prefab:'");
        }

        if (string.IsNullOrWhiteSpace(dto.Role) || !ValidRoles.Contains(dto.Role))
        {
            throw Invalid(path, "role must be one of mob, treasure, boss");
        }

        if (dto.Tier < 1 || dto.Tier > 5)
        {
            throw Invalid(path, "tier must be in 1..5");
        }

        if (dto.W < 4 || dto.H < 4)
        {
            throw Invalid(path, "w and h must be at least 4");
        }
    }

    private static void ValidateGrids(string path, PrefabDto dto, int expected)
    {
        ValidateGrid(path, "ground", dto.Ground, expected);
        ValidateGrid(path, "wall", dto.Wall, expected);
        ValidateGrid(path, "decor", dto.Decor, expected);
    }

    private static void ValidateGrid(string path, string name, ushort[]? grid, int expected)
    {
        if (grid is null || grid.Length != expected)
        {
            throw Invalid(path, $"{name} length must be {expected}");
        }
    }

    private static bool[] ConvertBlocked(string path, int[]? blocked, int expected)
    {
        if (blocked is null || blocked.Length != expected)
        {
            throw Invalid(path, $"blocked length must be {expected}");
        }

        bool[] converted = new bool[blocked.Length];
        for (int i = 0; i < blocked.Length; i++)
        {
            if (blocked[i] != 0 && blocked[i] != 1)
            {
                throw Invalid(path, "blocked values must be 0 or 1");
            }

            converted[i] = blocked[i] == 1;
        }

        return converted;
    }

    private static void ValidateMouths(string path, int w, int h, bool[] blocked, PrefabPoi[] mouths)
    {
        if (mouths.Length == 0)
        {
            throw Invalid(path, "at least one mouth is required");
        }

        foreach (PrefabPoi mouth in mouths)
        {
            if (mouth.X < 0 || mouth.X >= w || mouth.Y < 0 || mouth.Y >= h)
            {
                throw Invalid(path, $"mouth {mouth.X},{mouth.Y} is outside the prefab");
            }

            bool edge = mouth.X == 0 || mouth.Y == 0 || mouth.X == w - 1 || mouth.Y == h - 1;
            if (!edge)
            {
                throw Invalid(path, $"mouth {mouth.X},{mouth.Y} must be on the edge");
            }

            if (blocked[mouth.Y * w + mouth.X])
            {
                throw Invalid(path, $"mouth {mouth.X},{mouth.Y} must be open");
            }
        }
    }

    private static void ValidateConnected(string path, int w, int h, bool[] blocked)
    {
        int start = Array.FindIndex(blocked, cell => !cell);
        if (start < 0)
        {
            throw Invalid(path, "at least one open cell is required");
        }

        bool[] seen = new bool[blocked.Length];
        Stack<int> stack = new Stack<int>();
        seen[start] = true;
        stack.Push(start);
        while (stack.Count > 0)
        {
            int index = stack.Pop();
            int x = index % w;
            int y = index / w;
            TryVisit(w, h, blocked, seen, stack, x - 1, y);
            TryVisit(w, h, blocked, seen, stack, x + 1, y);
            TryVisit(w, h, blocked, seen, stack, x, y - 1);
            TryVisit(w, h, blocked, seen, stack, x, y + 1);
        }

        for (int i = 0; i < blocked.Length; i++)
        {
            if (!blocked[i] && !seen[i])
            {
                throw Invalid(path, "open cells must be 4-connected");
            }
        }
    }

    private static void TryVisit(int w, int h, bool[] blocked, bool[] seen, Stack<int> stack, int x, int y)
    {
        if (x < 0 || x >= w || y < 0 || y >= h)
        {
            return;
        }

        int index = y * w + x;
        if (blocked[index] || seen[index])
        {
            return;
        }

        seen[index] = true;
        stack.Push(index);
    }

    private static void ValidateSpawnTheme(
        string path,
        string? role,
        string[] spawnTheme,
        Func<string, bool> speciesExists)
    {
        if (role == "mob" && spawnTheme.Length == 0)
        {
            throw Invalid(path, "mob prefabs require a non-empty spawnTheme");
        }

        foreach (string species in spawnTheme)
        {
            if (!speciesExists(species))
            {
                throw Invalid(path, $"unknown species in spawnTheme: {species}");
            }
        }
    }

    private static InvalidDataException Invalid(string path, string reason) =>
        new InvalidDataException($"{path}: {reason}");

    private sealed class PrefabDto
    {
        public string? Id { get; set; }
        public string? Role { get; set; }
        public int Tier { get; set; }
        public string? Theme { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public ushort[]? Ground { get; set; }
        public ushort[]? Wall { get; set; }
        public ushort[]? Decor { get; set; }
        public int[]? Blocked { get; set; }
        public PrefabPoi[]? Mouths { get; set; }
        public PrefabPoi[]? Chests { get; set; }
        public string[]? SpawnTheme { get; set; }
    }
}
