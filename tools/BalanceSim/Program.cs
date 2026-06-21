using System.Globalization;
using System.Text;
using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace BalanceSim;

/// <summary>
/// Simulador headless de balanceamento (MG-01). Constrói o engine determinístico (GameWorld) com o
/// piloto-automático ligado e tica até a run acabar, varrendo {7 Kaelis} × {5 tiers} × {N seeds} e
/// medindo TTK, tempo de hunt, dano e one-shots. Sem hub, sem frontend, sem DB.
///
///   dotnet run --project tools/BalanceSim -- --out docs/balance/baseline.csv
///
/// Flags: --out &lt;csv&gt;  --seeds &lt;N=50&gt;  --content-root &lt;dir&gt;  --cards full|none
///        --max-ticks &lt;N=12000&gt;  --kaeli &lt;id&gt;  --tier &lt;n&gt;  --seed-start &lt;n=1&gt;
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string? outPath = null, contentRoot = null, kaeliFilter = null;
        var seeds = 50;
        var seedStart = 1L;
        var cards = true;
        var maxTicks = 12000;
        int? tierFilter = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out": outPath = args[++i]; break;
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seed-start": seedStart = long.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--content-root": contentRoot = args[++i]; break;
                case "--cards": cards = !args[++i].Equals("none", StringComparison.OrdinalIgnoreCase); break;
                case "--max-ticks": maxTicks = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--kaeli": kaeliFilter = args[++i]; break;
                case "--tier": tierFilter = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default:
                    Console.Error.WriteLine($"flag desconhecida: {args[i]}");
                    return 1;
            }
        }

        var root = SimHostEnvironment.ResolveContentRoot(contentRoot);
        Console.WriteLine($"content-root: {root}");
        var env = new SimHostEnvironment(root);
        var data = new GameData(env);
        var content = new ContentStore(env);
        var monsters = new MonsterRegistry(data, content);
        var items = new ItemRegistry(data, content);
        var kaelis = new KaeliRegistry(content);

        var tiers = content.Tiers.OrderBy(t => t.Tier).ToList();
        if (tierFilter is { } tf) tiers = tiers.Where(t => t.Tier == tf).ToList();
        var roster = kaelis.All
            .Where(w => kaeliFilter is null || w.Id.Equals(kaeliFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (roster.Count == 0) { Console.Error.WriteLine("nenhuma Kaeli selecionada"); return 1; }

        // gear recomendado por tier (caro de montar; cacheia uma vez por tier).
        var gearByTier = tiers.ToDictionary(t => t.Tier, t => RecommendedGear.ForTier(t.Tier));

        GameWorld Build(WaifuDef waifu, DungeonTier tier, long seed) => new(
            seed, tier, waifu, ascension: 0, data, monsters,
            bestiaryKills: new Dictionary<string, long>(),
            equipmentStats: gearByTier[tier.Tier],
            loadout: null, items: items, helperProfile: null);

        // ---- determinism canary: mesma seed 2× → ticks de kill idênticos ----
        Console.WriteLine();
        Console.WriteLine($"== canário de determinismo ({roster[0].Name} · T{tiers[0].Tier} · seed 424242) ==");
        var canaryOk = DeterminismCanary(() => Build(roster[0], tiers[0], 424242), roster[0].Name, tiers[0].Tier, cards, maxTicks);
        Console.WriteLine(canaryOk ? "  PASS — runs idênticas" : "  FAIL — divergência entre runs!");

        // ---- sweep ----
        var allKills = new List<KillRow>();
        var allRuns = new List<RunRow>();
        var total = roster.Count * tiers.Count * seeds;
        var done = 0;
        Console.WriteLine();
        Console.WriteLine($"== sweep: {roster.Count} Kaelis × {tiers.Count} tiers × {seeds} seeds = {total} runs (cards={(cards ? "full" : "none")}) ==");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var tier in tiers)
        foreach (var waifu in roster)
        for (var s = 0; s < seeds; s++)
        {
            var seed = seedStart + s;
            var result = Simulator.Run(Build(waifu, tier, seed), waifu.Name, tier.Tier, seed, cards, maxTicks);
            allKills.AddRange(result.Kills);
            allRuns.Add(result.Summary);
            done++;
            if (done % 25 == 0 || done == total)
                Console.Write($"\r  {done}/{total} runs ({sw.Elapsed.TotalSeconds:F0}s)   ");
        }
        Console.WriteLine();

        // ---- pivô de TTK (linhas tier×rank, colunas Kaeli, célula = TTK mediano em ciclos) ----
        PrintTtkPivot(allKills, roster.Select(w => w.Name).ToList(), tiers.Select(t => t.Tier).ToList());
        PrintRunSummary(allRuns, roster.Select(w => w.Name).ToList(), tiers.Select(t => t.Tier).ToList());

        // ---- CSV ----
        if (outPath is not null)
        {
            WriteCsv(outPath, allKills, allRuns);
            Console.WriteLine($"\nCSV escrito em {Path.GetFullPath(outPath)} ({allKills.Count} kills + {allRuns.Count} runs)");
        }

        return canaryOk ? 0 : 2;
    }

    private static bool DeterminismCanary(Func<GameWorld> build, string kaeli, int tier, bool cards, int maxTicks)
    {
        static string Fingerprint(RunResult r) => string.Join(";",
            r.Kills.OrderBy(k => k.TtkTicks).ThenBy(k => k.Species, StringComparer.Ordinal)
                .Select(k => $"{k.Species}:{k.TtkTicks}:{k.MaxHp}"))
            + $"|dealt={r.Summary.DamageDealt};taken={r.Summary.DamageTaken};dur={r.Summary.DurationMs}";

        var a = Simulator.Run(build(), kaeli, tier, 424242, cards, maxTicks);
        var b = Simulator.Run(build(), kaeli, tier, 424242, cards, maxTicks);
        return Fingerprint(a) == Fingerprint(b);
    }

    private static readonly string[] Ranks = ["common", "elite", "boss"];

    private static void PrintTtkPivot(List<KillRow> kills, List<string> kaelis, List<int> tiers)
    {
        Console.WriteLine();
        Console.WriteLine("== TTK mediano em CICLOS de ação (gear × mob no mesmo tier) ==");
        var header = "tier×rank".PadRight(14) + string.Concat(kaelis.Select(k => Short(k).PadLeft(9)));
        Console.WriteLine(header);
        Console.WriteLine(new string('-', header.Length));

        foreach (var tier in tiers)
        foreach (var rank in Ranks)
        {
            var label = $"T{tier} {rank}".PadRight(14);
            var sb = new StringBuilder(label);
            foreach (var kaeli in kaelis)
            {
                var vals = kills
                    .Where(k => k.Tier == tier && k.Rank == rank && k.Kaeli == kaeli)
                    .Select(k => k.TtkCycles).ToList();
                sb.Append((vals.Count == 0 ? "-" : Median(vals).ToString("F1", CultureInfo.InvariantCulture)).PadLeft(9));
            }
            Console.WriteLine(sb);
        }
    }

    private static void PrintRunSummary(List<RunRow> runs, List<string> kaelis, List<int> tiers)
    {
        Console.WriteLine();
        Console.WriteLine("== runs por Kaeli×tier: vitórias / mortes / one-shots / runs sem fim ==");
        foreach (var tier in tiers)
        {
            Console.WriteLine($"  T{tier}:");
            foreach (var kaeli in kaelis)
            {
                var r = runs.Where(x => x.Tier == tier && x.Kaeli == kaeli).ToList();
                if (r.Count == 0) continue;
                var wins = r.Count(x => x.Victory);
                var deaths = r.Count(x => x.PlayerDied);
                var unfinished = r.Count(x => x.Unfinished);
                var oneShots = r.Sum(x => x.OneShotCount);
                var hunt = r.Where(x => x.Victory).Select(x => (double)x.DurationMs).DefaultIfEmpty(0).Average();
                Console.WriteLine(
                    $"    {Short(kaeli).PadRight(8)} win {wins,3}/{r.Count,-3}  death {deaths,3}  unfin {unfinished,3}  " +
                    $"1shot {oneShots,4}  huntMed {(hunt / 1000.0):F0}s");
            }
        }
    }

    private static void WriteCsv(string path, List<KillRow> kills, List<RunRow> runs)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("kind,kaeli,tier,seed,species,rank,maxHp,ttkTicks,ttkMs,ttkCycles,oneShot," +
                      "victory,playerDied,unfinished,durationMs,kills,dmgDealt,dmgTaken,oneShotCount");

        foreach (var k in kills)
            sb.Append("kill,").Append(Csv(k.Kaeli)).Append(',').Append(k.Tier).Append(',').Append(k.Seed).Append(',')
              .Append(Csv(k.Species)).Append(',').Append(k.Rank).Append(',').Append(k.MaxHp).Append(',')
              .Append(k.TtkTicks).Append(',').Append(k.TtkMs).Append(',')
              .Append(k.TtkCycles.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(k.OneShot ? 1 : 0).Append(",,,,,,,,\n");

        foreach (var r in runs)
            sb.Append("run,").Append(Csv(r.Kaeli)).Append(',').Append(r.Tier).Append(',').Append(r.Seed)
              .Append(",,,,,,,,") // species..oneShot vazios
              .Append(r.Victory ? 1 : 0).Append(',').Append(r.PlayerDied ? 1 : 0).Append(',')
              .Append(r.Unfinished ? 1 : 0).Append(',').Append(r.DurationMs).Append(',').Append(r.Kills).Append(',')
              .Append(r.DamageDealt).Append(',').Append(r.DamageTaken).Append(',').Append(r.OneShotCount).Append('\n');

        File.WriteAllText(path, sb.ToString());
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\""
        : s;

    private static string Short(string name) => name.Length <= 8 ? name : name[..8];

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }
}
