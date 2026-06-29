using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace BalanceSim;

/// <summary>
/// LM-01 — determinism safety net for the generator. For a FIXED list of seeds and tiers, it
/// regenerates floors via <see cref="DungeonGenerator.Generate"/> and computes a stable SHA-256 hash
/// per floor over everything that describes the layout: <c>Ground</c>, <c>Wall</c>, <c>Decor</c>,
/// <c>Blocked</c>, the <c>Room{X,Y,W,H,Role}</c> sequence, <c>Entry</c>, <c>LadderDown</c>,
/// <c>Chests</c>, and <c>Sanctuaries</c>. It mirrors exactly what <see cref="GameWorld"/> does at run
/// start (same seed -> <c>new Rng((ulong)seed)</c>, same biome, normal floor 0 + boss floor 1), so
/// any future generator/RNG change that alters the map is caught here.
///
///   dotnet run --project tools/BalanceSim -- --golden          # (re)escreve o baseline
///   dotnet run --project tools/BalanceSim -- --golden-check     # compara e falha se divergir
/// </summary>
internal static class Golden
{
    // FIXED list — never derive from clock/environment. Changing this list is a deliberate baseline change.
    private static readonly long[] Seeds = [1, 7, 42, 123, 4242, 99999, 2654435761];
    private static readonly int[] Tiers = [1, 2, 3, 4, 5];

    // Walk upward from bin until the repo root is found.
    private static readonly string DefaultBaselinePath =
        Path.Combine(RepoRoot(), "docs", "balance", "golden_dungeon.txt");

    /// <summary>
    /// Golden mode entrypoint. <paramref name="check"/>=false writes the baseline; true compares it.
    /// Returns the exit code consumed by <c>Main</c>.
    /// </summary>
    public static int Run(bool check, string? outPath)
    {
        var path = outPath is null ? DefaultBaselinePath : Path.GetFullPath(outPath);
        var current = Compute();

        if (!check)
        {
            Write(path, current);
            Console.WriteLine($"golden baseline written to {path}");
            Console.WriteLine($"  {current.Count} floors ({Seeds.Length} seeds x {Tiers.Length} tiers x 2 floors)");
            return 0;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"baseline not found at {path} — run without --golden-check first.");
            return 2;
        }

        var baseline = ParseBaseline(path);
        var diffs = new List<string>();
        foreach (var (key, hash) in current)
        {
            if (!baseline.TryGetValue(key, out var old))
                diffs.Add($"  + {key}: missing from baseline (generated={hash})");
            else if (!string.Equals(old, hash, StringComparison.OrdinalIgnoreCase))
                diffs.Add($"  ~ {key}: baseline={old} != generated={hash}");
        }
        foreach (var key in baseline.Keys)
            if (!current.ContainsKey(key))
                diffs.Add($"  - {key}: present in baseline but not generated");

        if (diffs.Count == 0)
        {
            Console.WriteLine($"golden mode: GREEN — {current.Count} floors identical to baseline ({path}).");
            return 0;
        }

        Console.Error.WriteLine($"golden mode: FAIL — {diffs.Count} floor(s) diverge from baseline:");
        foreach (var d in diffs) Console.Error.WriteLine(d);
        Console.Error.WriteLine();
        Console.Error.WriteLine("If the generation change is INTENTIONAL, rebaseline with:  " +
                                "dotnet run --project tools/BalanceSim -- --golden");
        return 3;
    }

    /// <summary>Hashes ordered by (tier, seed, floor) — stable key "T{tier} seed{seed} f{floor}".</summary>
    private static SortedDictionary<string, string> Compute()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var tier in Tiers)
        {
            var biome = Biomes.ForTier(tier);
            foreach (var seed in Seeds)
            {
                // Mirrors GameWorld: one Rng((ulong)seed) generates both floors in sequence.
                var rng = new Rng((ulong)seed);
                var f0 = DungeonGenerator.Generate(rng, 0, isBossFloor: false, biome);
                var f1 = DungeonGenerator.Generate(rng, 1, isBossFloor: true, biome);
                result[Key(tier, seed, 0)] = HashFloor(f0);
                result[Key(tier, seed, 1)] = HashFloor(f1);
            }
        }
        return result;
    }

    private static string Key(int tier, long seed, int floor) =>
        string.Create(CultureInfo.InvariantCulture, $"T{tier} seed{seed} f{floor}");

    /// <summary>
    /// SHA-256 over a deterministic floor serialization. Everything is little-endian via
    /// <see cref="BinaryWriter"/>, with prefixed counts so field boundaries are unambiguous and the
    /// already deterministic room/POI order is preserved exactly.
    /// </summary>
    private static string HashFloor(DungeonFloor f)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(f.Index);
            w.Write(f.W);
            w.Write(f.H);

            foreach (var v in f.Ground) w.Write(v);
            foreach (var v in f.Wall) w.Write(v);
            foreach (var v in f.Decor) w.Write(v);
            foreach (var b in f.Blocked) w.Write(b);

            w.Write(f.Rooms.Count);
            foreach (var r in f.Rooms)
            {
                w.Write(r.X); w.Write(r.Y); w.Write(r.W); w.Write(r.H);
                w.Write(r.Role);
            }

            w.Write(f.Entry.X); w.Write(f.Entry.Y);
            w.Write(f.LadderDown.HasValue);
            if (f.LadderDown is { } ld) { w.Write(ld.X); w.Write(ld.Y); }

            w.Write(f.Chests.Count);
            foreach (var (x, y) in f.Chests) { w.Write(x); w.Write(y); }

            w.Write(f.Sanctuaries.Count);
            foreach (var (x, y) in f.Sanctuaries) { w.Write(x); w.Write(y); }
        }
        return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }

    private static void Write(string path, SortedDictionary<string, string> hashes)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("# golden_dungeon.txt — generator determinism baseline (LM-01).");
        sb.AppendLine("#");
        sb.AppendLine("# Each line: tier<TAB>seed<TAB>floor<TAB>sha256. The hash covers Ground/Wall/Decor/Blocked,");
        sb.AppendLine("# the Room{X,Y,W,H,Role}, Entry, LadderDown, Chests, and Sanctuaries sequence for each floor");
        sb.AppendLine("# (floor 0 = normal, floor 1 = boss), generated as GameWorld does at run start.");
        sb.AppendLine("#");
        sb.AppendLine("# Generate/regenerate:  dotnet run --project tools/BalanceSim -- --golden");
        sb.AppendLine("# Check (CI):           dotnet run --project tools/BalanceSim -- --golden-check");
        sb.AppendLine("#");
        sb.AppendLine("# If the comparator fails and the generation change is INTENTIONAL, rebaseline with --golden");
        sb.AppendLine("# and commit the diff consciously. Unexpected failure = determinism regression.");
        sb.AppendLine("#");
        foreach (var (key, hash) in hashes)
        {
            // key = "T{tier} seed{seed} f{floor}" → tier, seed, floor
            var (tier, seed, floor) = ParseKey(key);
            sb.Append(tier).Append('\t').Append(seed).Append('\t').Append(floor).Append('\t').Append(hash).Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }

    private static Dictionary<string, string> ParseBaseline(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split('\t');
            if (parts.Length != 4) continue;
            var tier = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var seed = long.Parse(parts[1], CultureInfo.InvariantCulture);
            var floor = int.Parse(parts[2], CultureInfo.InvariantCulture);
            map[Key(tier, seed, floor)] = parts[3];
        }
        return map;
    }

    private static (int tier, long seed, int floor) ParseKey(string key)
    {
        // "T{tier} seed{seed} f{floor}"
        var sp = key.Split(' ');
        return (
            int.Parse(sp[0].AsSpan(1), CultureInfo.InvariantCulture),
            long.Parse(sp[1].AsSpan(4), CultureInfo.InvariantCulture),
            int.Parse(sp[2].AsSpan(1), CultureInfo.InvariantCulture));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "docs", "balance")) &&
                Directory.Exists(Path.Combine(dir.FullName, "backend")))
                return dir.FullName;
            dir = dir.Parent;
        }
        // Fallback: current directory (the user can pass explicit --golden-out).
        return Directory.GetCurrentDirectory();
    }
}
