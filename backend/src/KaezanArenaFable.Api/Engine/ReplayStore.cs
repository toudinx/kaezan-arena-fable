using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// FF-01: persists finished-run replays to <c>.data/replays/</c> (gzip JSON) purely for
/// debugging/verification — nothing here is exposed to the player. Retention is capped at
/// <see cref="GameConfig.ReplayKeepLast"/> files (oldest deleted). Training runs are skipped:
/// they are sandboxes that only end by Abandon and carry no balance signal.
/// </summary>
public sealed class ReplayStore(IHostEnvironment environment, ILogger<ReplayStore> logger)
{
    private readonly string _directory = Path.Combine(environment.ContentRootPath, ".data", "replays");

    public void SaveFinishedRun(GameWorld world)
    {
        if (world.Ended is null || world.Mode == GameMode.Training) return;
        try
        {
            Directory.CreateDirectory(_directory);
            var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}_{world.Waifu.Id.Replace(':', '-')}" +
                       $"_t{world.Tier.Tier}_seed{world.Seed}{ReplayIo.Extension}";
            ReplayIo.Save(world.BuildReplay(), Path.Combine(_directory, name));
            Prune();
        }
        catch (Exception ex)
        {
            // Replay persistence must never take down the tick loop — log and move on.
            logger.LogWarning(ex, "failed to persist replay for seed {Seed}", world.Seed);
        }
    }

    private void Prune()
    {
        var stale = Directory.GetFiles(_directory, "*" + ReplayIo.Extension)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Skip(GameConfig.ReplayKeepLast)
            .ToList();
        foreach (var path in stale) File.Delete(path);
    }
}
