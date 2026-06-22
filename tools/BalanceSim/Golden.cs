using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace BalanceSim;

/// <summary>
/// LM-01 — rede de segurança de determinismo do gerador. Para uma lista FIXA de seeds e tiers,
/// re-gera os andares via <see cref="DungeonGenerator.Generate"/> e computa um hash estável (SHA-256)
/// por andar de tudo que descreve o layout: <c>Ground</c>, <c>Wall</c>, <c>Decor</c>, <c>Blocked</c>,
/// a sequência de <c>Room{X,Y,W,H,Role}</c>, <c>Entry</c>, <c>LadderDown</c>, <c>Chests</c> e
/// <c>Sanctuaries</c>. Espelha exatamente o que o <see cref="GameWorld"/> faz no início da run
/// (mesmo seed → <c>new Rng((ulong)seed)</c>, mesmo bioma, andar 0 normal + andar 1 boss), então
/// qualquer mexida futura no gerador/RNG que altere o mapa é capturada aqui.
///
///   dotnet run --project tools/BalanceSim -- --golden          # (re)escreve o baseline
///   dotnet run --project tools/BalanceSim -- --golden-check     # compara e falha se divergir
/// </summary>
internal static class Golden
{
    // Lista FIXA — não derivar de relógio/ambiente. Mudar esta lista é uma mudança consciente de baseline.
    private static readonly long[] Seeds = [1, 7, 42, 123, 4242, 99999, 2654435761];
    private static readonly int[] Tiers = [1, 2, 3, 4, 5];

    // Sobe na árvore (ou sai do bin) até achar a raiz do repo (onde existe docs/ e backend/).
    private static readonly string DefaultBaselinePath =
        Path.Combine(RepoRoot(), "docs", "balance", "golden_dungeon.txt");

    /// <summary>
    /// Entrada do modo-ouro. <paramref name="check"/>=false grava o baseline; true compara contra ele.
    /// Retorna o exit code (0 = ok; ≠0 = divergência/erro), consumido pelo <c>Main</c>.
    /// </summary>
    public static int Run(bool check, string? outPath)
    {
        var path = outPath is null ? DefaultBaselinePath : Path.GetFullPath(outPath);
        var current = Compute();

        if (!check)
        {
            Write(path, current);
            Console.WriteLine($"baseline-ouro escrito em {path}");
            Console.WriteLine($"  {current.Count} andares ({Seeds.Length} seeds × {Tiers.Length} tiers × 2 andares)");
            return 0;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"baseline não encontrado em {path} — rode sem --golden-check primeiro.");
            return 2;
        }

        var baseline = ParseBaseline(path);
        var diffs = new List<string>();
        foreach (var (key, hash) in current)
        {
            if (!baseline.TryGetValue(key, out var old))
                diffs.Add($"  + {key}: ausente no baseline (gerado={hash})");
            else if (!string.Equals(old, hash, StringComparison.OrdinalIgnoreCase))
                diffs.Add($"  ~ {key}: baseline={old} != gerado={hash}");
        }
        foreach (var key in baseline.Keys)
            if (!current.ContainsKey(key))
                diffs.Add($"  - {key}: presente no baseline mas não gerado");

        if (diffs.Count == 0)
        {
            Console.WriteLine($"modo-ouro: VERDE — {current.Count} andares idênticos ao baseline ({path}).");
            return 0;
        }

        Console.Error.WriteLine($"modo-ouro: FALHA — {diffs.Count} andar(es) divergem do baseline:");
        foreach (var d in diffs) Console.Error.WriteLine(d);
        Console.Error.WriteLine();
        Console.Error.WriteLine("Se a mudança de geração é INTENCIONAL, rebaseline com:  " +
                                "dotnet run --project tools/BalanceSim -- --golden");
        return 3;
    }

    /// <summary>Hashes ordenados por (tier, seed, andar) — chave estável "T{tier} seed{seed} f{floor}".</summary>
    private static SortedDictionary<string, string> Compute()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var tier in Tiers)
        {
            var biome = Biomes.ForTier(tier);
            foreach (var seed in Seeds)
            {
                // Espelha GameWorld: um único Rng((ulong)seed) gera os dois andares em sequência.
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
    /// SHA-256 sobre uma serialização determinística do andar. Tudo que entra é little-endian via
    /// <see cref="BinaryWriter"/>, com contagens prefixadas, para que a fronteira entre campos não seja
    /// ambígua e a ordem das salas/POIs (já determinística no gerador) seja preservada na íntegra.
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
        sb.AppendLine("# golden_dungeon.txt — baseline de determinismo do gerador (LM-01).");
        sb.AppendLine("#");
        sb.AppendLine("# Cada linha: tier<TAB>seed<TAB>floor<TAB>sha256. O hash cobre Ground/Wall/Decor/Blocked,");
        sb.AppendLine("# a sequência de Room{X,Y,W,H,Role}, Entry, LadderDown, Chests e Sanctuaries de cada andar");
        sb.AppendLine("# (andar 0 = normal, andar 1 = boss), gerados como o GameWorld faz no início da run.");
        sb.AppendLine("#");
        sb.AppendLine("# Gerar/regenerar:  dotnet run --project tools/BalanceSim -- --golden");
        sb.AppendLine("# Conferir (CI):    dotnet run --project tools/BalanceSim -- --golden-check");
        sb.AppendLine("#");
        sb.AppendLine("# Se o comparador falhar e a mudança de geração for INTENCIONAL, rebaseline com --golden");
        sb.AppendLine("# e commite o diff conscientemente. Falha inesperada = regressão de determinismo.");
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
        // Fallback: diretório atual (o usuário pode passar --golden-out explícito).
        return Directory.GetCurrentDirectory();
    }
}
