using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Meta;

/// <summary>Seam the RunManager uses to chain session runs without a DI cycle: the RunManager only
/// knows this interface; endpoints/hub mediate everything else.</summary>
public interface ISessionCoordinator
{
    /// <summary>Called by the RunManager when a session run finished and its end-beat elapsed.
    /// Returns the next chained world, or null when the session stops/pauses.</summary>
    GameWorld? OnRunCompleted(GameWorld world, RunEndDto end);

    /// <summary>Manual player input on a watched session run pauses chaining until resumed.</summary>
    void OnManualInput();
}

public sealed record SessionStateDto(
    string Status, int RunNumber, int CurrentTier, string CurrentWaifuId, string? StopReason,
    SessionAggregates Last2h, List<RunJournalEntry> Journal);

/// <summary>
/// Owns the single idle session (single-account game): plan, stats, journal and status. Lives
/// entirely OUTSIDE the engine — GameWorld is never touched; chaining happens between runs.
/// Thread-safety: all mutations under one lock (called from the RunManager tick loop and from
/// API endpoints).
/// </summary>
public sealed class SessionOrchestrator(RunFactory factory, AccountStore store, ILogger<SessionOrchestrator> logger)
    : ISessionCoordinator
{
    private readonly object _lock = new();
    private SessionPlan? _plan;
    private SessionStats _stats = new(0, 0, 0, 0, 1, 0);
    private RunJournal _journal = new(GameConfig.SessionJournalCapacity);
    private string _status = "stopped"; // running | paused | stopped
    private string? _stopReason;
    private string _currentWaifuId = "";
    private bool _pendingChain; // a run ended while paused; Resume() must kick the next one

    public GameWorld StartSession(SessionPlan plan)
    {
        lock (_lock)
        {
            if (_status == "running") throw new InvalidOperationException("a session is already running");
            _plan = plan;
            _stats = new SessionStats(0, 0, 0, 0, plan.Tier, 0);
            _journal = new RunJournal(GameConfig.SessionJournalCapacity);
            _status = "running";
            _stopReason = null;
            _pendingChain = false;
            SessionDecision decision = SessionDecider.Decide(plan, _stats, EnergyAvailable(), GameConfig.DungeonEnergyPerRun);
            if (decision is not StartNextRun start)
                throw new InvalidOperationException(((StopSession)decision).Reason);
            return CreateWorld(start);
        }
    }

    public void Stop(string reason)
    {
        lock (_lock)
        {
            if (_status == "stopped") return;
            _status = "stopped";
            _stopReason = reason;
        }
    }

    public void OnManualInput()
    {
        lock (_lock)
        {
            if (_status == "running")
            {
                _status = "paused";
                logger.LogInformation("session paused by manual input");
            }
        }
    }

    /// <summary>Clears the pause; when a run already ended while paused, returns the next world
    /// to hand to the RunManager (mediated by the caller), else null (current run continues).</summary>
    public GameWorld? Resume()
    {
        lock (_lock)
        {
            if (_status != "paused") return null;
            _status = "running";
            if (!_pendingChain) return null;
            _pendingChain = false;
            SessionDecision decision = SessionDecider.Decide(_plan!, _stats, EnergyAvailable(), GameConfig.DungeonEnergyPerRun);
            if (decision is StartNextRun start) return CreateWorld(start);
            Stop(((StopSession)decision).Reason);
            return null;
        }
    }

    public GameWorld? OnRunCompleted(GameWorld world, RunEndDto end)
    {
        lock (_lock)
        {
            if (_plan is null || _status == "stopped") return null;

            SessionDecision decision;
            (_stats, decision) = Advance(_stats, _plan, end.Victory, EnergyAvailable(), GameConfig.DungeonEnergyPerRun);
            _journal.Add(new RunJournalEntry(
                _stats.RunsCompleted, world.Seed, world.Tier.Tier, world.Waifu.Id, end.Victory, end.Reason,
                end.DurationMs, end.GoldEarned, end.AccountXpEarned, end.Kills, DateTimeOffset.UtcNow));

            if (_status == "paused")
            {
                _pendingChain = true;
                return null;
            }
            if (decision is StopSession stop)
            {
                _status = "stopped";
                _stopReason = stop.Reason;
                return null;
            }
            return CreateWorld((StartNextRun)decision);
        }
    }

    /// <summary>Pure stats transition + next decision. Internal for unit tests.</summary>
    internal static (SessionStats Stats, SessionDecision Decision) Advance(
        SessionStats stats, SessionPlan plan, bool victory, int energy, int energyPerRun)
    {
        SessionStats next = stats with
        {
            RunsCompleted = stats.RunsCompleted + 1,
            Wins = stats.Wins + (victory ? 1 : 0),
            ConsecutiveLosses = victory ? 0 : stats.ConsecutiveLosses + 1,
            WinsAtCurrentTier = stats.WinsAtCurrentTier + (victory ? 1 : 0),
            RotationIndex = stats.RotationIndex + 1,
        };
        SessionDecision decision = SessionDecider.Decide(plan, next, energy, energyPerRun);
        if (decision is StartNextRun start && start.Tier > next.CurrentTier)
            next = next with { CurrentTier = start.Tier, WinsAtCurrentTier = 0 };
        return (next, decision);
    }

    public SessionStateDto? Snapshot()
    {
        lock (_lock)
        {
            if (_plan is null) return null;
            return new SessionStateDto(
                _status, _stats.RunsCompleted, _stats.CurrentTier, _currentWaifuId, _stopReason,
                _journal.Aggregate(TimeSpan.FromHours(2), DateTimeOffset.UtcNow),
                _journal.Entries.Reverse().Take(30).ToList());
        }
    }

    private GameWorld CreateWorld(StartNextRun start)
    {
        _currentWaifuId = start.WaifuId;
        store.Mutate(s => EnergyLedger.TrySpend(s, GameConfig.DungeonEnergyPerRun, DateTimeOffset.UtcNow));
        return factory.Create(start.Tier, start.WaifuId, seed: null, GameMode.Dungeon);
    }

    private int EnergyAvailable() => store.Read(s => EnergyLedger.Current(s, DateTimeOffset.UtcNow));
}
