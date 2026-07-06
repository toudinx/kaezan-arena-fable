using System.Globalization;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Lazy-settled energy bank used only by idle-session chaining. The engine never reads it, so run
/// determinism stays inside GameWorld.
/// </summary>
public static class EnergyLedger
{
    public static int Current(AccountState s, DateTimeOffset nowUtc)
    {
        if (s.EnergyUpdatedUtc.Length == 0) return GameConfig.DungeonEnergyCap;
        var last = DateTimeOffset.Parse(s.EnergyUpdatedUtc, CultureInfo.InvariantCulture);
        var minutes = Math.Max(0, (nowUtc - last).TotalMinutes);
        var regen = (long)(minutes * GameConfig.EnergyRegenPerMinute);
        return (int)Math.Min(GameConfig.DungeonEnergyCap, s.Energy + regen);
    }

    /// <summary>
    /// Settles regen into state, then deducts when affordable. Failed spends still advance the
    /// settlement timestamp so repeated attempts cannot double-count the same regeneration.
    /// </summary>
    public static bool TrySpend(AccountState s, int amount, DateTimeOffset nowUtc)
    {
        var current = Current(s, nowUtc);
        s.EnergyUpdatedUtc = nowUtc.ToString("O");
        if (current < amount)
        {
            s.Energy = current;
            return false;
        }

        s.Energy = current - amount;
        return true;
    }
}
