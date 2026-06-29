using System.Globalization;
using System.Text;
using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace BalanceSim;

/// <summary>
/// Headless balance simulator (MG-01). Builds the deterministic engine (GameWorld) with autopilot
/// enabled and ticks until the run ends, sweeping {7 Kaelis} x {5 tiers} x {N seeds} and measuring
/// TTK, hunt time, damage, and one-shots. No hub, no frontend, no DB.
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
        bool golden = false, goldenCheck = false;
        string? goldenOut = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--golden": golden = true; break;
                case "--golden-check": golden = true; goldenCheck = true; break;
                case "--golden-out": goldenOut = args[++i]; break;
                case "--out": outPath = args[++i]; break;
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seed-start": seedStart = long.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--content-root": contentRoot = args[++i]; break;
                case "--cards": cards = !args[++i].Equals("none", StringComparison.OrdinalIgnoreCase); break;
                case "--max-ticks": maxTicks = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--kaeli": kaeliFilter = args[++i]; break;
                case "--tier": tierFilter = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default:
                    Console.Error.WriteLine($"unknown flag: {args[i]}");
                    return 1;
            }
        }

        // LM-01: generator determinism golden mode. It is independent from content/DB — only Rng
        // + biome — so it runs before loading the environment and exits.
        if (golden)
            return Golden.Run(goldenCheck, goldenOut);

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
        if (roster.Count == 0) { Console.Error.WriteLine("no Kaeli selected"); return 1; }

        // Recommended gear by tier; expensive to assemble, cached once per tier.
        var gearByTier = tiers.ToDictionary(t => t.Tier, t => RecommendedGear.ForTier(t.Tier));

        GameWorld Build(WaifuDef waifu, DungeonTier tier, long seed) => new(
            seed, tier, waifu, ascension: 0, data, monsters,
            bestiaryKills: new Dictionary<string, long>(),
            equipmentStats: gearByTier[tier.Tier],
            loadout: null, items: items, helperProfile: null);

        // ---- determinism canary: same seed 2x -> identical kill ticks ----
        Console.WriteLine();
        Console.WriteLine($"== determinism canary ({roster[0].Name} · T{tiers[0].Tier} · seed 424242) ==");
        var canaryOk = DeterminismCanary(() => Build(roster[0], tiers[0], 424242), roster[0].Name, tiers[0].Tier, cards, maxTicks);
        Console.WriteLine(canaryOk ? "  PASS — identical runs" : "  FAIL — runs diverged!");

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

        // ---- TTK pivot (rows tier x rank, columns Kaeli, cell = median TTK in cycles) ----
        PrintTtkPivot(allKills, roster.Select(w => w.Name).ToList(), tiers.Select(t => t.Tier).ToList());
        PrintTtkTargetPivot(allKills, roster.Select(w => w.Name).ToList(), tiers.Select(t => t.Tier).ToList());
        PrintPostureBreakPivot(allRuns, tiers.Select(t => t.Tier).ToList());
        PrintRunSummary(allRuns, roster.Select(w => w.Name).ToList(), tiers.Select(t => t.Tier).ToList());
        PrintParityPivot(allRuns, roster);
        PrintRolePivot(allRuns, roster);

        // ---- CSV ----
        if (outPath is not null)
        {
            WriteCsv(outPath, allKills, allRuns);
            Console.WriteLine($"\nCSV written to {Path.GetFullPath(outPath)} ({allKills.Count} kills + {allRuns.Count} runs)");
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
        Console.WriteLine("== median TTK in ACTION CYCLES (gear x mob on the same tier) ==");
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

    // MG-08: TTK targets in CYCLES by rank (common ~3, elite ~6, boss ~12). "No hit-kill": boss < 8 is a hard fail.
    private static readonly IReadOnlyDictionary<string, double> TtkTargets =
        new Dictionary<string, double> { ["common"] = 3, ["elite"] = 6, ["boss"] = 12 };

    // MG-08: master pivot for HP/anti-one-shot solving. Per cell (tier x rank): CROSS-Kaeli median
    // TTK (HP is shared, so calibrate against the roster median while the +/-1 acceptance band absorbs
    // role variance), target, cycle delta, suggested HP factor (target/observed), one-shots, and
    // observed median maxHp. Pure reporting.
    private static void PrintTtkTargetPivot(List<KillRow> kills, List<string> kaelis, List<int> tiers)
    {
        Console.WriteLine();
        Console.WriteLine("== TTK vs. TARGET (MG-08): cross-Kaeli median per cell · +/-1 cycle band ==");
        Console.WriteLine("   (xHP = suggested factor for next iteration = target/observed; 1shot = single hits >= HP)");
        Console.WriteLine($"   {"cell",-12}{"obsTTK",8}{"target",6}{"delta",8}{"xHP",7}{"1shot",7}{"maxHpMed",10}  status");
        Console.WriteLine("   " + new string('-', 70));

        foreach (var tier in tiers)
        foreach (var rank in Ranks)
        {
            var target = TtkTargets[rank];
            // Cross median: median per Kaeli, then median of those medians (each Kaeli weighs equally).
            var perKaeli = new List<double>();
            foreach (var k in kaelis)
            {
                var v = kills.Where(x => x.Tier == tier && x.Rank == rank && x.Kaeli == k)
                    .Select(x => x.TtkCycles).ToList();
                if (v.Count > 0) perKaeli.Add(Median(v));
            }
            var cellKills = kills.Where(x => x.Tier == tier && x.Rank == rank).ToList();
            var oneShots = cellKills.Count(x => x.OneShot);
            var maxHpMed = cellKills.Count > 0 ? Median(cellKills.Select(x => (double)x.MaxHp).ToList()) : 0;

            var label = $"T{tier} {rank}";
            if (perKaeli.Count == 0)
            {
                Console.WriteLine($"   {label,-12}{"-",8}{target,6:F0}{"-",8}{"-",7}{oneShots,7}{maxHpMed,10:F0}  no data");
                continue;
            }
            var obs = Median(perKaeli);
            var dev = obs - target;
            var scale = obs > 0 ? target / obs : 0;
            // Inside the +/-1 cycle band AND no one-shot = OK; one-shot = hard hit-kill fail.
            var inBand = Math.Abs(dev) <= 1.0;
            var status = oneShots > 0 ? "ONE-SHOT" : inBand ? "OK" : (dev < 0 ? "fast" : "slow");
            // boss < 8 cycles is the hard roadmap constraint.
            if (rank == "boss" && obs < 8) status += " ⛔<8";
            Console.WriteLine($"   {label,-12}{obs,8:F1}{target,6:F0}{dev,+8:F1}{scale,7:F2}{oneShots,7}{maxHpMed,10:F0}  {status}");
        }
    }

    // F-E posture: how much boss death comes from Echo Break windows. Per tier (only runs that
    // actually fought the boss = BossTotalDamage > 0): median break count, median % of boss HP
    // removed IN breaks, % of total boss damage from breaks, and peak window (%maxHp).
    // Target: break contributes <= ~40% of the boss bar; above that Echo Break becomes "delete".
    private static void PrintPostureBreakPivot(List<RunRow> runs, List<int> tiers)
    {
        Console.WriteLine();
        Console.WriteLine("== ECHO BREAK (F-E): break contribution to boss death (only runs that fought the boss) ==");
        Console.WriteLine("   (target: %HP/breaks <= ~40%; peak = largest effective damage in one window, in %maxHp)");
        Console.WriteLine($"   {"tier",-6}{"runs",6}{"breaks",9}{"%hp(br)",11}{"%dmg(br)",11}{"peakWindow",12}  status");
        Console.WriteLine("   " + new string('-', 64));

        const double breakShareTarget = 0.40;
        foreach (var tier in tiers)
        {
            var bossRuns = runs.Where(r => r.Tier == tier && r.BossTotalDamage > 0 && r.BossMaxHp > 0).ToList();
            if (bossRuns.Count == 0)
            {
                Console.WriteLine($"   {("T" + tier),-6}{"-",6}{"-",9}{"-",11}{"-",11}{"-",12}  no data");
                continue;
            }
            var breaksMed = Median(bossRuns.Select(r => (double)r.BreakCount).ToList());
            var hpShareMed = Median(bossRuns.Select(r => (double)r.BossBreakDamage / r.BossMaxHp).ToList());
            var dmgShareMed = Median(bossRuns.Select(r => (double)r.BossBreakDamage / r.BossTotalDamage).ToList());
            var peakMed = Median(bossRuns.Select(r => (double)r.PeakWindowDamage / r.BossMaxHp).ToList());
            var status = hpShareMed <= breakShareTarget ? "OK" : "HIGH";
            Console.WriteLine(
                $"   {("T" + tier),-6}{bossRuns.Count,6}{breaksMed,9:F1}" +
                $"{hpShareMed * 100,10:F0}%{dmgShareMed * 100,10:F0}%{peakMed * 100,11:F0}%  {status}");
        }
    }

    private static void PrintRunSummary(List<RunRow> runs, List<string> kaelis, List<int> tiers)
    {
        Console.WriteLine();
        Console.WriteLine("== runs by Kaeli x tier: victories / deaths / one-shots / unfinished runs ==");
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

    // MG-06: INTRA-ROLE parity pivot. For each role (Mage/Archer/Knight) and tier, list each Kaeli's
    // median hunt time (victories only), median dmgDealt (victories), deaths, and % delta vs. group
    // median. spread% = (max-min)/group median; target <= +/-10% in BOTH hunt and damage. Pure reporting.
    private static void PrintParityPivot(List<RunRow> runs, List<WaifuDef> roster)
    {
        Console.WriteLine();
        Console.WriteLine("== INTRA-ROLE PARITY (MG-06): hunt (victories) + dmgDealt by Kaeli, delta vs. group median ==");
        Console.WriteLine("   (target: each Kaeli within +/-10% of group median, and group spread% <= +/-10%, in BOTH hunt and dmg)");

        var tiers = runs.Select(r => r.Tier).Distinct().OrderBy(t => t).ToList();
        var byName = roster.ToDictionary(w => w.Name);

        foreach (var role in new[] { KaeliRole.Mage, KaeliRole.Archer, KaeliRole.Knight })
        {
            var group = roster.Where(w => w.Role == role).Select(w => w.Name).ToList();
            if (group.Count == 0) continue;
            Console.WriteLine();
            Console.WriteLine($"  ── {role} ── ({string.Join(", ", group.Select(Short))})");

            foreach (var tier in tiers)
            {
                // Median per Kaeli (victories only) for hunt(s) and dmg; deaths = total tier deaths.
                var huntMed = new Dictionary<string, double?>();
                var dmgMed = new Dictionary<string, double?>();
                var deaths = new Dictionary<string, int>();
                foreach (var name in group)
                {
                    var rs = runs.Where(r => r.Tier == tier && r.Kaeli == name).ToList();
                    var wins = rs.Where(r => r.Victory).ToList();
                    huntMed[name] = wins.Count > 0 ? Median(wins.Select(r => (double)r.DurationMs).ToList()) : null;
                    dmgMed[name] = wins.Count > 0 ? Median(wins.Select(r => (double)r.DamageDealt).ToList()) : null;
                    deaths[name] = rs.Count(r => r.PlayerDied);
                }

                var huntVals = group.Select(n => huntMed[n]).Where(v => v is not null).Select(v => v!.Value).ToList();
                var dmgVals = group.Select(n => dmgMed[n]).Where(v => v is not null).Select(v => v!.Value).ToList();
                var huntGroupMed = huntVals.Count > 0 ? Median(huntVals) : 0;
                var dmgGroupMed = dmgVals.Count > 0 ? Median(dmgVals) : 0;

                Console.WriteLine($"    T{tier}:");
                foreach (var name in group)
                {
                    var h = huntMed[name];
                    var d = dmgMed[name];
                    var hStr = h is null ? "-" : $"{h.Value / 1000.0,5:F0}s";
                    var hDev = h is null || huntGroupMed == 0 ? "" : $"{(h.Value - huntGroupMed) / huntGroupMed * 100,+5:F0}%";
                    var dStr = d is null ? "-" : $"{d.Value,8:F0}";
                    var dDev = d is null || dmgGroupMed == 0 ? "" : $"{(d.Value - dmgGroupMed) / dmgGroupMed * 100,+5:F0}%";
                    Console.WriteLine($"      {Short(name).PadRight(8)} hunt {hStr,6} {hDev,6}   dmg {dStr,8} {dDev,6}   death {deaths[name],2}");
                }

                if (huntVals.Count >= 2 && huntGroupMed > 0)
                {
                    var hSpread = (huntVals.Max() - huntVals.Min()) / huntGroupMed * 100;
                    var dSpread = dmgVals.Count >= 2 && dmgGroupMed > 0 ? (dmgVals.Max() - dmgVals.Min()) / dmgGroupMed * 100 : 0;
                    var ok = hSpread <= 20 && dSpread <= 20; // spread (max-min) <= 20% means +/-10% around median
                    Console.WriteLine($"      spread%  hunt {hSpread,5:F0}%   dmg {dSpread,5:F0}%   {(ok ? "OK" : "OUT")}");
                }
            }
        }
    }

    // MG-07: BETWEEN-ROLE normalization pivot. Aggregates by role (all Kaelis in the role x seeds)
    // median hunt time (victories only), effective dmgDealt/dmgTaken, and deaths by tier. spread%
    // between the 3 roles = (max-min)/median; target <= +/-15% in hunt while preserving MG-02 target order.
    private static void PrintRolePivot(List<RunRow> runs, List<WaifuDef> roster)
    {
        Console.WriteLine();
        Console.WriteLine("== BETWEEN-ROLE NORMALIZATION (MG-07): hunt (victories) + damage dealt/taken + deaths by role ==");
        Console.WriteLine("   (target: hunt spread% between the 3 roles <= +/-15% => <=30%; MG-02 target order preserved)");

        var roles = new[] { KaeliRole.Mage, KaeliRole.Archer, KaeliRole.Knight };
        var nameRole = roster.ToDictionary(w => w.Name, w => w.Role);
        var tiers = runs.Select(r => r.Tier).Distinct().OrderBy(t => t).ToList();

        // spread% is only honest between roles that actually clear the tier; median hunt for a role
        // that wins 6/60 is survivorship bias. Count only roles above this win-rate floor; below it,
        // viability is a survival problem (deaths) = MG-08.
        const double viableWinRate = 0.40;

        foreach (var tier in tiers)
        {
            var huntViable = new Dictionary<KaeliRole, double?>();
            Console.WriteLine($"  T{tier}:");
            foreach (var role in roles)
            {
                var rs = runs.Where(r => r.Tier == tier && nameRole.GetValueOrDefault(r.Kaeli) == role).ToList();
                if (rs.Count == 0) continue;
                var wins = rs.Where(r => r.Victory).ToList();
                var winRate = (double)wins.Count / rs.Count;
                double? hunt = wins.Count > 0 ? Median(wins.Select(r => (double)r.DurationMs).ToList()) : null;
                double? dealt = wins.Count > 0 ? Median(wins.Select(r => (double)r.DamageDealt).ToList()) : null;
                double? taken = wins.Count > 0 ? Median(wins.Select(r => (double)r.DamageTaken).ToList()) : null;
                // MG-08: end HP (post-boss) and minimum HP in victories — survival without death noise.
                double? endHp = wins.Count > 0 ? Median(wins.Select(r => r.EndHpFraction).ToList()) : null;
                double? minHp = wins.Count > 0 ? Median(wins.Select(r => r.MinHpFraction).ToList()) : null;
                var deaths = rs.Count(r => r.PlayerDied);
                if (winRate >= viableWinRate) huntViable[role] = hunt;
                var hStr = hunt is null ? "    -" : $"{hunt.Value / 1000.0,5:F0}s";
                var dStr = dealt is null ? "-" : $"{dealt.Value,8:F0}";
                var tStr = taken is null ? "-" : $"{taken.Value,7:F0}";
                var endStr = endHp is null ? "-" : $"{endHp.Value * 100,3:F0}%";
                var minStr = minHp is null ? "-" : $"{minHp.Value * 100,3:F0}%";
                var viableMark = winRate >= viableWinRate ? "  " : " !"; // ! = not viable, outside spread
                Console.WriteLine($"    {role,-7} hunt {hStr}   dmgDealt {dStr}   dmgTaken {tStr}   endHp {endStr,4} minHp {minStr,4}  death {deaths,3}  (wins {wins.Count}/{rs.Count}{viableMark})");
            }

            var hv = roles.Select(r => huntViable.GetValueOrDefault(r)).Where(v => v is not null).Select(v => v!.Value).ToList();
            if (hv.Count >= 2)
            {
                var med = Median(hv);
                var spread = med > 0 ? (hv.Max() - hv.Min()) / med * 100 : 0;
                Console.WriteLine($"    spread% hunt (viable roles, win>={viableWinRate:P0}) {spread,5:F0}%   {(spread <= 30 ? "OK" : "OUT")}");
            }
            else
            {
                Console.WriteLine($"    spread% hunt — <2 viable roles in this tier (survival = MG-08)");
            }
        }
    }

    private static void WriteCsv(string path, List<KillRow> kills, List<RunRow> runs)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("kind,kaeli,tier,seed,species,rank,maxHp,ttkTicks,ttkMs,ttkCycles,oneShot," +
                      "victory,playerDied,unfinished,durationMs,kills,dmgDealt,dmgTaken,oneShotCount," +
                      "endHpFrac,minHpFrac,bossMaxHp,bossTotalDamage,bossBreakDamage,breakCount,peakWindowDamage");

        foreach (var k in kills)
            sb.Append("kill,").Append(Csv(k.Kaeli)).Append(',').Append(k.Tier).Append(',').Append(k.Seed).Append(',')
              .Append(Csv(k.Species)).Append(',').Append(k.Rank).Append(',').Append(k.MaxHp).Append(',')
              .Append(k.TtkTicks).Append(',').Append(k.TtkMs).Append(',')
              .Append(k.TtkCycles.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(k.OneShot ? 1 : 0).Append(",,,,,,,,,,,,,,,\n");

        foreach (var r in runs)
            sb.Append("run,").Append(Csv(r.Kaeli)).Append(',').Append(r.Tier).Append(',').Append(r.Seed)
              .Append(",,,,,,,,") // empty species..oneShot fields
              .Append(r.Victory ? 1 : 0).Append(',').Append(r.PlayerDied ? 1 : 0).Append(',')
              .Append(r.Unfinished ? 1 : 0).Append(',').Append(r.DurationMs).Append(',').Append(r.Kills).Append(',')
              .Append(r.DamageDealt).Append(',').Append(r.DamageTaken).Append(',').Append(r.OneShotCount).Append(',')
              .Append(r.EndHpFraction.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.MinHpFraction.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.BossMaxHp).Append(',').Append(r.BossTotalDamage).Append(',').Append(r.BossBreakDamage)
              .Append(',').Append(r.BreakCount).Append(',').Append(r.PeakWindowDamage).Append('\n');

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
