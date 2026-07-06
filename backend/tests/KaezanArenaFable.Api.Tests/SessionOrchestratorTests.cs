using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Tests;

public class SessionOrchestratorTests
{
    private static readonly SessionPlan Plan =
        new(1, ["waifu:eloa", "waifu:seren"], MaxRuns: 0, StopAfterConsecutiveLosses: 3,
            TierUpWins: 2, MaxTier: 3, StopWhenOutOfEnergy: false);

    [Fact]
    public void victory_resets_loss_streak_and_advances_rotation()
    {
        SessionStats stats = new SessionStats(RunsCompleted: 4, Wins: 2, ConsecutiveLosses: 2,
            WinsAtCurrentTier: 1, CurrentTier: 1, RotationIndex: 4);
        (SessionStats next, SessionDecision decision) = SessionOrchestrator.Advance(stats, Plan, victory: true, energy: 300, energyPerRun: 60);
        Assert.Equal(5, next.RunsCompleted);
        Assert.Equal(0, next.ConsecutiveLosses);
        Assert.Equal(0, next.WinsAtCurrentTier);  // 2nd win triggers the tier-up, which resets the counter
        StartNextRun start = Assert.IsType<StartNextRun>(decision);
        Assert.Equal(2, start.Tier);              // TierUpWins=2 reached
        Assert.Equal("waifu:seren", start.WaifuId); // rotation index 5 -> second Kaeli
    }

    [Fact]
    public void tier_up_resets_wins_at_tier_counter()
    {
        SessionStats stats = new SessionStats(4, 2, 0, 1, CurrentTier: 1, RotationIndex: 0);
        (SessionStats next, SessionDecision decision) = SessionOrchestrator.Advance(stats, Plan, victory: true, 300, 60);
        Assert.Equal(2, Assert.IsType<StartNextRun>(decision).Tier);
        Assert.Equal(2, next.CurrentTier);
        Assert.Equal(0, next.WinsAtCurrentTier);
    }

    [Fact]
    public void third_straight_loss_stops_the_session()
    {
        SessionStats stats = new SessionStats(9, 5, 2, 0, 1, 9);
        (SessionStats _, SessionDecision decision) = SessionOrchestrator.Advance(stats, Plan, victory: false, 300, 60);
        Assert.IsType<StopSession>(decision);
    }
}
