using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Tests;

public class SessionDeciderTests
{
    private static SessionPlan Plan(
        int tier = 1, string[]? rotation = null, int maxRuns = 0, int stopLosses = 0,
        int tierUpWins = 0, int maxTier = 5, bool stopNoEnergy = false) =>
        new(tier, rotation ?? ["waifu:eloa"], maxRuns, stopLosses, tierUpWins, maxTier, stopNoEnergy);

    private static SessionStats Stats(
        int runs = 0, int wins = 0, int losses = 0, int winsAtTier = 0, int tier = 1, int rot = 0) =>
        new(runs, wins, losses, winsAtTier, tier, rot);

    [Fact]
    public void starts_next_run_at_plan_tier_with_first_rotation_kaeli()
    {
        var d = SessionDecider.Decide(Plan(tier: 2, rotation: ["waifu:eloa", "waifu:seren"]), Stats(tier: 2), 300, 60);
        var start = Assert.IsType<StartNextRun>(d);
        Assert.Equal(2, start.Tier);
        Assert.Equal("waifu:eloa", start.WaifuId);
    }

    [Fact]
    public void rotation_cycles_by_rotation_index()
    {
        var plan = Plan(rotation: ["waifu:eloa", "waifu:seren"]);
        var d = SessionDecider.Decide(plan, Stats(runs: 3, rot: 3), 300, 60);
        Assert.Equal("waifu:seren", Assert.IsType<StartNextRun>(d).WaifuId);
    }

    [Fact]
    public void stops_when_run_budget_reached()
    {
        var d = SessionDecider.Decide(Plan(maxRuns: 10), Stats(runs: 10), 300, 60);
        Assert.Contains("budget", Assert.IsType<StopSession>(d).Reason);
    }

    [Fact]
    public void stops_after_consecutive_losses()
    {
        var d = SessionDecider.Decide(Plan(stopLosses: 3), Stats(runs: 5, losses: 3), 300, 60);
        Assert.Contains("losses", Assert.IsType<StopSession>(d).Reason);
    }

    [Fact]
    public void stops_when_out_of_energy_if_enabled()
    {
        var d = SessionDecider.Decide(Plan(stopNoEnergy: true), Stats(), 40, 60);
        Assert.Contains("energy", Assert.IsType<StopSession>(d).Reason);
        // disabled: keeps going
        Assert.IsType<StartNextRun>(SessionDecider.Decide(Plan(stopNoEnergy: false), Stats(), 40, 60));
    }

    [Fact]
    public void tiers_up_after_enough_wins_but_respects_ceiling()
    {
        var plan = Plan(tier: 2, tierUpWins: 3, maxTier: 3);
        var up = SessionDecider.Decide(plan, Stats(runs: 4, winsAtTier: 3, tier: 2), 300, 60);
        Assert.Equal(3, Assert.IsType<StartNextRun>(up).Tier);
        var capped = SessionDecider.Decide(plan, Stats(runs: 9, winsAtTier: 5, tier: 3), 300, 60);
        Assert.Equal(3, Assert.IsType<StartNextRun>(capped).Tier);
    }

    [Fact]
    public void zero_valued_rules_mean_disabled()
    {
        var d = SessionDecider.Decide(Plan(maxRuns: 0, stopLosses: 0), Stats(runs: 500, losses: 50), 0, 60);
        Assert.IsType<StartNextRun>(d);
    }
}
