namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Player-authored strategy for an idle session: which tier to farm, which Kaelis rotate run over
/// run, and when to stop. Zero-valued stop rules mean "disabled" (an infinite session stops only
/// by explicit player action). Pure data — the decision logic lives in <see cref="SessionDecider"/>.
/// </summary>
public sealed record SessionPlan(
    int Tier,
    IReadOnlyList<string> WaifuRotation,
    int MaxRuns,
    int StopAfterConsecutiveLosses,
    int TierUpWins,
    int MaxTier,
    bool StopWhenOutOfEnergy);

/// <summary>Running counters the decider reads. CurrentTier can exceed Plan.Tier via TierUpWins.</summary>
public sealed record SessionStats(
    int RunsCompleted, int Wins, int ConsecutiveLosses, int WinsAtCurrentTier, int CurrentTier, int RotationIndex);

public abstract record SessionDecision;
public sealed record StartNextRun(int Tier, string WaifuId) : SessionDecision;
public sealed record StopSession(string Reason) : SessionDecision;

/// <summary>
/// Pure decision function for run chaining. Stop rules are checked first (budget, loss streak,
/// energy), then tier progression, then Kaeli rotation. No I/O, no clock, no rng — fully unit-tested.
/// </summary>
public static class SessionDecider
{
    public static SessionDecision Decide(SessionPlan plan, SessionStats stats, int energyAvailable, int energyPerRun)
    {
        if (plan.MaxRuns > 0 && stats.RunsCompleted >= plan.MaxRuns)
            return new StopSession($"run budget reached ({plan.MaxRuns})");
        if (plan.StopAfterConsecutiveLosses > 0 && stats.ConsecutiveLosses >= plan.StopAfterConsecutiveLosses)
            return new StopSession($"{stats.ConsecutiveLosses} losses in a row");
        if (plan.StopWhenOutOfEnergy && energyAvailable < energyPerRun)
            return new StopSession("out of energy");

        var tier = stats.CurrentTier;
        if (plan.TierUpWins > 0 && stats.WinsAtCurrentTier >= plan.TierUpWins && tier < plan.MaxTier)
            tier++;

        var waifu = plan.WaifuRotation[stats.RotationIndex % plan.WaifuRotation.Count];
        return new StartNextRun(tier, waifu);
    }
}
