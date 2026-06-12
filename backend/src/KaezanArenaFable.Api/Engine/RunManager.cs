using System.Collections.Concurrent;
using KaezanArenaFable.Api.Hubs;
using KaezanArenaFable.Api.Meta;
using Microsoft.AspNetCore.SignalR;

namespace KaezanArenaFable.Api.Engine;

public sealed class ActiveRun
{
    public required GameWorld World;
    public required string ConnectionId;
    public bool RewardsApplied;
    public int TicksAfterEnd;
}

public sealed record OrphanedRun(ActiveRun Run, DateTimeOffset DisconnectedAt);

/// <summary>
/// Owns all active runs (one per SignalR connection) and ticks them at GameConfig.TickMs,
/// pushing snapshots to the owning client.
/// </summary>
public sealed class RunManager(IHubContext<GameHub> hub, RewardService rewards, ILogger<RunManager> logger)
    : BackgroundService
{
    private readonly ConcurrentDictionary<string, ActiveRun> _runs = new();
    private readonly Dictionary<string, OrphanedRun> _orphans = [];
    private readonly object _orphanLock = new();

    public void StartRun(string connectionId, GameWorld world)
    {
        if (_runs.TryRemove(connectionId, out var previous))
            FinalizeAbandon(previous);
        _runs[connectionId] = new ActiveRun { World = world, ConnectionId = connectionId };
    }

    public GameWorld? GetRun(string connectionId) =>
        _runs.TryGetValue(connectionId, out var run) ? run.World : null;

    public bool TryResumeRun(string connectionId, out GameWorld? world)
    {
        ActiveRun? resumed = null;
        List<ActiveRun> expired = [];
        var now = DateTimeOffset.UtcNow;

        lock (_orphanLock)
        {
            foreach (var (orphanId, orphan) in _orphans.ToList())
            {
                if ((now - orphan.DisconnectedAt).TotalMilliseconds < Domain.GameConfig.RunReconnectGraceMs)
                    continue;
                _orphans.Remove(orphanId);
                expired.Add(orphan.Run);
            }

            var candidate = _orphans
                .OrderByDescending(pair => pair.Value.DisconnectedAt)
                .FirstOrDefault();
            if (candidate.Value is not null)
            {
                _orphans.Remove(candidate.Key);
                resumed = candidate.Value.Run;
            }
        }

        foreach (var run in expired)
            FinalizeAbandon(run);

        if (resumed is null)
        {
            world = null;
            return false;
        }

        lock (resumed)
        {
            resumed.ConnectionId = connectionId;
            resumed.World.RequestMapRefresh();
        }
        _runs[connectionId] = resumed;
        world = resumed.World;
        return true;
    }

    public void DropRun(string connectionId)
    {
        if (!_runs.TryRemove(connectionId, out var run)) return;

        lock (run)
        {
            if (run.World.Ended is null && !run.RewardsApplied)
            {
                lock (_orphanLock)
                    _orphans[connectionId] = new OrphanedRun(run, DateTimeOffset.UtcNow);
                return;
            }
        }

        FinalizeAbandon(run);
    }

    public void AbandonRun(string connectionId)
    {
        if (_runs.TryRemove(connectionId, out var run))
            FinalizeAbandon(run);
    }

    private void FinalizeAbandon(ActiveRun run)
    {
        lock (run)
        {
            if (run.World.Ended is null)
            {
                run.World.Enqueue(new Command(CommandKind.Abandon, 0, 0, null));
                run.World.Tick();
            }

            if (run.World.Ended is not null && !run.RewardsApplied)
            {
                run.RewardsApplied = true;
                rewards.Apply(run.World, run.World.Ended);
            }
        }
    }

    private void ExpireOrphans()
    {
        List<ActiveRun> expired = [];
        var cutoff = DateTimeOffset.UtcNow.AddMilliseconds(-Domain.GameConfig.RunReconnectGraceMs);
        lock (_orphanLock)
        {
            foreach (var (orphanId, orphan) in _orphans.ToList())
            {
                if (orphan.DisconnectedAt > cutoff) continue;
                _orphans.Remove(orphanId);
                expired.Add(orphan.Run);
            }
        }

        foreach (var run in expired)
            FinalizeAbandon(run);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Domain.GameConfig.TickMs));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            ExpireOrphans();
            foreach (var (connectionId, run) in _runs)
            {
                try
                {
                    SnapshotDto snapshot;
                    MapDto? map;
                    lock (run)
                    {
                        if (!_runs.TryGetValue(connectionId, out var current) || !ReferenceEquals(current, run))
                            continue;

                        (snapshot, map) = run.World.Tick();

                        if (snapshot.Run.Ended is not null && !run.RewardsApplied)
                        {
                            run.RewardsApplied = true;
                            var enriched = rewards.Apply(run.World, snapshot.Run.Ended);
                            snapshot = snapshot with { Run = snapshot.Run with { Ended = enriched } };
                        }

                        if (run.RewardsApplied && ++run.TicksAfterEnd > 50)
                            _runs.TryRemove(connectionId, out _);
                    }

                    if (map is not null)
                        await hub.Clients.Client(connectionId).SendAsync("map", map, stoppingToken);

                    await hub.Clients.Client(connectionId).SendAsync("snapshot", snapshot, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "run tick failed; dropping run {ConnectionId}", connectionId);
                    _runs.TryRemove(connectionId, out _);
                }
            }
        }
    }
}
