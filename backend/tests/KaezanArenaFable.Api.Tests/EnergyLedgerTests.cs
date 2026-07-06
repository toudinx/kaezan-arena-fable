using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Tests;

public class EnergyLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void fresh_account_is_at_cap()
    {
        var s = new AccountState();
        Assert.Equal(GameConfig.DungeonEnergyCap, EnergyLedger.Current(s, T0));
    }

    [Fact]
    public void regenerates_over_elapsed_time_and_clamps_at_cap()
    {
        var s = new AccountState { Energy = 100, EnergyUpdatedUtc = T0.ToString("O") };
        var after10 = EnergyLedger.Current(s, T0.AddMinutes(10));
        Assert.Equal(100 + 10 * GameConfig.EnergyRegenPerMinute, after10);
        Assert.Equal(GameConfig.DungeonEnergyCap, EnergyLedger.Current(s, T0.AddHours(48)));
    }

    [Fact]
    public void try_spend_settles_regen_then_deducts()
    {
        var s = new AccountState { Energy = 50, EnergyUpdatedUtc = T0.ToString("O") };
        var now = T0.AddMinutes(10);
        Assert.True(EnergyLedger.TrySpend(s, 60, now));
        Assert.Equal(50 + 10 * GameConfig.EnergyRegenPerMinute - 60, s.Energy);
        Assert.Equal(now.ToString("O"), s.EnergyUpdatedUtc);
    }

    [Fact]
    public void try_spend_fails_without_enough_energy_and_mutates_nothing_but_settlement()
    {
        var s = new AccountState { Energy = 10, EnergyUpdatedUtc = T0.ToString("O") };
        Assert.False(EnergyLedger.TrySpend(s, 60, T0.AddMinutes(1)));
        Assert.Equal(10 + GameConfig.EnergyRegenPerMinute, s.Energy);
    }
}
