using System.Collections.Concurrent;
using KaezanArenaFable.Api.Hubs;
using KaezanArenaFable.Api.Meta;
using Microsoft.AspNetCore.SignalR;

namespace KaezanArenaFable.Api.Engine;

public sealed class ActiveRun
{
    public required GameWorld World;
    public required string ConnectionId;   // session runs: the fixed key "session:local"
    public bool RewardsApplied;
    public int TicksAfterEnd;
    public bool IsSession;
    public string? WatcherConnectionId;    // SignalR client currently spectating (nullable)
    public RunEndDto? EnrichedEnd;         // reward-enriched end DTO captured for the session journal
}

public sealed record OrphanedRun(ActiveRun Run, DateTimeOffset DisconnectedAt);

/// <summary>
/// Owns all active runs (one per SignalR connection) and ticks them at GameConfig.TickMs,
/// pushing snapshots to the owning client.
/// </summary>
public sealed class RunManager(
    IHubContext<GameHub> hub, RewardService rewards, ReplayStore replays,
    ISessionCoordinator sessions, ILogger<RunManager> logger)
    : BackgroundService
{
    private const string SessionKey = "session:local";

    private readonly ConcurrentDictionary<string, ActiveRun> _runs = new();
    private readonly Dictionary<string, OrphanedRun> _orphans = [];
    private readonly object _orphanLock = new();
    private readonly PerfStats _tickPerf = new();
    private long _perfLogCounter;

    public void StartRun(string connectionId, GameWorld world)
    {
        if (_runs.TryRemove(connectionId, out var previous))
            FinalizeAbandon(previous);
        _runs[connectionId] = new ActiveRun { World = world, ConnectionId = connectionId };
    }

    /// <summary>Starts (or replaces) the single idle-session run. Session runs tick with no owning
    /// connection; a watcher may attach for snapshots.</summary>
    public void StartSessionRun(GameWorld world)
    {
        string? watcher = null;
        if (_runs.TryRemove(SessionKey, out ActiveRun? previous))
        {
            watcher = previous.WatcherConnectionId;
            FinalizeAbandon(previous);
        }
        _runs[SessionKey] = new ActiveRun
        {
            World = world, ConnectionId = SessionKey, IsSession = true, WatcherConnectionId = watcher
        };
        if (watcher is not null) world.RequestMapRefresh();
    }

    /// <summary>Attaches a spectator to the session run (returns its world, or null when no session).</summary>
    public GameWorld? AttachWatcher(string connectionId)
    {
        if (!_runs.TryGetValue(SessionKey, out ActiveRun? run)) return null;
        lock (run)
        {
            run.WatcherConnectionId = connectionId;
            run.World.RequestMapRefresh();
            return run.World;
        }
    }

    public void DetachWatcher(string connectionId)
    {
        if (_runs.TryGetValue(SessionKey, out ActiveRun? run))
            lock (run)
                if (run.WatcherConnectionId == connectionId) run.WatcherConnectionId = null;
    }

    public void StopSessionRun()
    {
        if (_runs.TryRemove(SessionKey, out ActiveRun? run)) FinalizeAbandon(run);
    }

    public GameWorld? GetRun(string connectionId)
    {
        if (_runs.TryGetValue(connectionId, out ActiveRun? run)) return run.World;
        if (_runs.TryGetValue(SessionKey, out ActiveRun? session) && session.WatcherConnectionId == connectionId)
        {
            sessions.OnManualInput(); // manual interference pauses chaining until resumed
            return session.World;
        }
        return null;
    }

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
        DetachWatcher(connectionId); // watcher leaves; the session run keeps ticking
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
                replays.SaveFinishedRun(run.World); // FF-01: freeze the replay at the ending tick
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
            if (++_perfLogCounter % 300 == 0 && _tickPerf.Count > 0)
                logger.LogInformation(
                    "tick perf: p50={P50:F2}ms p95={P95:F2}ms max={Max:F2}ms ({Count} samples)",
                    _tickPerf.Percentile(50), _tickPerf.Percentile(95), _tickPerf.Max(), _tickPerf.Count);

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

                        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                        (snapshot, map) = run.World.Tick();
                        _tickPerf.Add(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

                        if (snapshot.Run.Ended is not null && !run.RewardsApplied)
                        {
                            run.RewardsApplied = true;
                            replays.SaveFinishedRun(run.World); // FF-01: freeze the replay at the ending tick
                            var enriched = rewards.Apply(run.World, snapshot.Run.Ended);
                            run.EnrichedEnd = enriched;
                            snapshot = snapshot with { Run = snapshot.Run with { Ended = enriched } };
                        }

                        if (run.RewardsApplied && ++run.TicksAfterEnd > Domain.GameConfig.SessionRunChainDelayTicks)
                        {
                            if (run.IsSession && snapshot.Run.Ended is { } ended)
                            {
                                GameWorld? next = sessions.OnRunCompleted(run.World, run.EnrichedEnd ?? ended);
                                if (next is not null)
                                {
                                    run.World = next;
                                    run.RewardsApplied = false;
                                    run.TicksAfterEnd = 0;
                                    run.EnrichedEnd = null;
                                    if (run.WatcherConnectionId is not null) next.RequestMapRefresh();
                                }
                                else _runs.TryRemove(connectionId, out _);
                            }
                            else _runs.TryRemove(connectionId, out _);
                        }
                    }

                    string? target = run.IsSession ? run.WatcherConnectionId : connectionId;
                    if (target is not null)
                    {
                        if (map is not null)
                            await hub.Clients.Client(target).SendAsync("map", map, stoppingToken);

                        await hub.Clients.Client(target).SendAsync("snapshot", snapshot, stoppingToken);
                    }
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
