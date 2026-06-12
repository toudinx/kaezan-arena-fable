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

/// <summary>
/// Owns all active runs (one per SignalR connection) and ticks them at GameConfig.TickMs,
/// pushing snapshots to the owning client.
/// </summary>
public sealed class RunManager(IHubContext<GameHub> hub, RewardService rewards, ILogger<RunManager> logger)
    : BackgroundService
{
    private readonly ConcurrentDictionary<string, ActiveRun> _runs = new();

    public void StartRun(string connectionId, GameWorld world) =>
        _runs[connectionId] = new ActiveRun { World = world, ConnectionId = connectionId };

    public GameWorld? GetRun(string connectionId) =>
        _runs.TryGetValue(connectionId, out var run) ? run.World : null;

    public void DropRun(string connectionId)
    {
        if (_runs.TryRemove(connectionId, out var run) && run.World.Ended is null && !run.RewardsApplied)
        {
            // disconnected mid-run: treat as abandono (half gold kept)
            run.World.Enqueue(new Command(CommandKind.Abandon, 0, 0, null));
            run.World.Tick();
            if (run.World.Ended is not null)
                rewards.Apply(run.World, run.World.Ended);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Domain.GameConfig.TickMs));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var (connectionId, run) in _runs)
            {
                try
                {
                    var (snapshot, map) = run.World.Tick();

                    if (map is not null)
                        await hub.Clients.Client(connectionId).SendAsync("map", map, stoppingToken);

                    if (snapshot.Run.Ended is not null && !run.RewardsApplied)
                    {
                        run.RewardsApplied = true;
                        var enriched = rewards.Apply(run.World, snapshot.Run.Ended);
                        snapshot = snapshot with { Run = snapshot.Run with { Ended = enriched } };
                    }

                    await hub.Clients.Client(connectionId).SendAsync("snapshot", snapshot, stoppingToken);

                    if (run.RewardsApplied && ++run.TicksAfterEnd > 50)
                        _runs.TryRemove(connectionId, out _);
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
