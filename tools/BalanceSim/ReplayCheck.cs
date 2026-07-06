using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace BalanceSim;

/// <summary>
/// FF-01: bit-perfect replay verification. Loads one replay (or a directory battery), rebuilds the
/// GameWorld from the frozen metadata, re-feeds the command log tick by tick, and compares the
/// canonical state hashes: every intermediate hash (bisection of the first divergent tick) plus the
/// final hash. No SignalR, no account state — pure engine.
///
///   dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays
///   dotnet run --project tools/BalanceSim -- --replay-check some-run.replay.json.gz
/// </summary>
internal static class ReplayCheck
{
    public static int Run(string target, string? contentRoot)
    {
        var root = SimHostEnvironment.ResolveContentRoot(contentRoot);
        var env = new SimHostEnvironment(root);
        var data = new GameData(env);
        var content = new ContentStore(env);
        var monsters = new MonsterRegistry(data, content);
        var items = new ItemRegistry(data, content);
        var kaelis = new KaeliRegistry(content);

        var files = Directory.Exists(target)
            ? Directory.GetFiles(target, "*" + ReplayIo.Extension).Order(StringComparer.Ordinal).ToArray()
            : [target];
        if (files.Length == 0)
        {
            Console.Error.WriteLine($"no {ReplayIo.Extension} files in {target}");
            return 1;
        }

        Console.WriteLine($"== replay-check: {files.Length} replay(s) ==");
        var failures = 0;
        foreach (var file in files)
        {
            var verdict = CheckOne(file, data, monsters, items, kaelis);
            if (!verdict) failures++;
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"REPLAY-CHECK GREEN — {files.Length}/{files.Length} bit-perfect"
            : $"REPLAY-CHECK FAIL — {failures}/{files.Length} diverged");
        return failures == 0 ? 0 : 4;
    }

    private static bool CheckOne(
        string file, GameData data, MonsterRegistry monsters, ItemRegistry items, KaeliRegistry kaelis)
    {
        var name = Path.GetFileName(file);
        ReplayFile replay;
        try
        {
            replay = ReplayIo.Load(file);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {name}: UNREADABLE ({ex.Message})");
            return false;
        }

        var waifu = kaelis.Find(replay.WaifuId);
        if (waifu is null)
        {
            Console.WriteLine($"  {name}: FAIL — unknown Kaeli '{replay.WaifuId}' (content changed?)");
            return false;
        }
        var skin = waifu.Skins.FirstOrDefault(s => s.Id == replay.SkinId) ?? waifu.DefaultSkin;
        var loadout = new KaeliLoadout(replay.AffinityLevel, replay.Mastery, skin);

        var world = new GameWorld(
            replay.Seed, replay.TierDef, waifu, replay.Ascension, data, monsters,
            replay.BestiaryKills, replay.EquipmentStats, loadout, items,
            replay.HelperProfile, replay.RoleTunings, replay.Mode, replay.Biome);

        // Re-feed the command log: everything recorded for tick N is enqueued right before ticking N,
        // preserving the original within-tick apply order (the log is in apply order).
        var next = 0;
        for (long tick = 1; tick <= replay.FinalTick; tick++)
        {
            while (next < replay.Commands.Count && replay.Commands[next].Tick == tick)
            {
                var c = replay.Commands[next++];
                world.Enqueue(new Command(c.Kind, c.A, c.B, c.S));
            }
            world.Tick();
        }

        // Intermediate hashes: first divergent tick wins the report (bisection anchor).
        var recorded = replay.TickHashes;
        var resimmed = world.TickHashes;
        for (var i = 0; i < Math.Min(recorded.Count, resimmed.Count); i++)
        {
            if (recorded[i].Hash == resimmed[i].Hash) continue;
            Console.WriteLine($"  {name}: FAIL — first divergence at tick {recorded[i].Tick} " +
                              $"(recorded {Short(recorded[i].Hash)} vs re-sim {Short(resimmed[i].Hash)})");
            return false;
        }
        if (recorded.Count != resimmed.Count)
        {
            Console.WriteLine($"  {name}: FAIL — hash timeline length {resimmed.Count} != recorded {recorded.Count} " +
                              "(run ended at a different tick)");
            return false;
        }

        var finalHash = world.ComputeStateHash();
        if (finalHash != replay.FinalHash)
        {
            Console.WriteLine($"  {name}: FAIL — final hash {Short(finalHash)} != recorded {Short(replay.FinalHash)} " +
                              $"(ended={world.Ended?.Reason ?? "not ended"})");
            return false;
        }

        Console.WriteLine($"  {name}: OK — {replay.FinalTick} ticks, {replay.Commands.Count} commands, " +
                          $"{recorded.Count} checkpoints, final {Short(finalHash)}");
        return true;
    }

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;
}
