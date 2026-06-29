using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Meta;

public sealed record OfflineProgressionReward(
    long ElapsedMinutes,
    long CreditedMinutes,
    long Gold,
    long AccountXp,
    int Tier,
    bool Capped);

/// <summary>Applies a finished run to the account: currencies, xp, bestiary, inventory, dailies.</summary>
public sealed class RewardService(AccountStore store, DailyService dailies)
{
    public OfflineProgressionReward? ClaimOfflineProgression() =>
        store.Mutate(state => ClaimOfflineProgression(state, DateTimeOffset.UtcNow));

    public static OfflineProgressionReward? ClaimOfflineProgression(AccountState state, DateTimeOffset now)
    {
        if (!DateTimeOffset.TryParse(state.LastSeenUtc, out var lastSeen) || lastSeen > now)
        {
            state.LastSeenUtc = now.ToString("O");
            return null;
        }

        var elapsedMinutes = (long)Math.Floor((now - lastSeen).TotalMinutes);
        var creditedMinutes = Math.Min(elapsedMinutes, GameConfig.OfflineProgressCapMinutes);
        state.LastSeenUtc = now.ToString("O");
        if (creditedMinutes < GameConfig.OfflineProgressMinMinutes) return null;

        var tier = Math.Clamp(
            state.TierClears.Where(entry => entry.Value > 0)
                .Select(entry => entry.Key)
                .DefaultIfEmpty(1)
                .Max(),
            1, 5);
        var tierMult = GameConfig.OfflineProgressTierMultiplier(tier);
        var gold = (long)Math.Floor(GameConfig.OfflineProgressGoldPerHour * tierMult * creditedMinutes / 60.0);
        var accountXp = (long)Math.Floor(GameConfig.OfflineProgressAccountXpPerHour * tierMult * creditedMinutes / 60.0);

        if (gold <= 0 && accountXp <= 0) return null;

        state.Gold += gold;
        GrantAccountXp(state, accountXp);

        return new OfflineProgressionReward(
            elapsedMinutes, creditedMinutes, gold, accountXp, tier,
            elapsedMinutes > GameConfig.OfflineProgressCapMinutes);
    }

    public RunEndDto Apply(GameWorld world, RunEndDto end)
    {
        dailies.GetToday(); // roll the day if needed before progressing contracts
        var notes = new List<string>();

        store.Mutate(state =>
        {
            state.RunsPlayed++;
            if (end.Victory)
            {
                state.RunsWon++;
                state.TierClears[world.Tier.Tier] = state.TierClears.GetValueOrDefault(world.Tier.Tier) + 1;
            }

            state.Gold += end.GoldEarned;
            state.Kaeros += end.KaerosEarned;
            GrantAccountXp(state, end.AccountXpEarned);

            // kaeli depth: affinity per use + mastery points for the run's Kaeli
            var affinityXp = (end.Victory ? GameConfig.AffinityXpVictory : GameConfig.AffinityXpDefeat)
                             + GameConfig.AffinityXpPerRunLevel * end.RunLevel;
            KaeliService.GrantAffinityXp(state, world.Waifu, affinityXp, notes);

            var masteryPoints = end.Victory ? GameConfig.MasteryPointsPerVictory : GameConfig.MasteryPointsPerDefeat;
            if (!state.Mastery.TryGetValue(world.Waifu.Id, out var mastery))
                state.Mastery[world.Waifu.Id] = mastery = new MasteryState();
            mastery.Points += masteryPoints;
            notes.Add($"{world.Waifu.Name}: +{affinityXp} affinity · +{masteryPoints} mastery point(s)");

            foreach (var (species, kills) in world.KillsBySpecies)
                state.BestiaryKills[species] = state.BestiaryKills.GetValueOrDefault(species) + kills;

            foreach (var item in end.Items)
            {
                if (state.Inventory.TryGetValue(item.ItemId, out var stack)) stack.Count += item.Count;
                else state.Inventory[item.ItemId] = new InventoryStack { ItemId = item.ItemId, Name = item.Name, Count = item.Count };
            }

            // daily contract progress
            foreach (var contract in state.Dailies)
            {
                if (contract.Claimed || contract.Progress >= contract.Target) continue;
                var before = contract.Progress;
                contract.Progress = contract.Kind switch
                {
                    "kill_species" => contract.Progress + world.KillsBySpecies.GetValueOrDefault(contract.Param),
                    "clear_tier" when end.Victory && contract.Param == world.Tier.Tier.ToString() => contract.Progress + 1,
                    "open_chests" => contract.Progress + world.ChestsOpened,
                    "collect_gold" => contract.Progress + end.GoldEarned,
                    _ => contract.Progress
                };
                contract.Progress = Math.Min(contract.Progress, contract.Target);
                if (contract.Progress > before)
                    notes.Add(contract.Progress >= contract.Target
                        ? $"Contract complete: {contract.Description}"
                        : $"Contract {contract.Progress}/{contract.Target}: {contract.Description}");
            }
        });

        return end with { DailyProgressNotes = notes };
    }

    public static void GrantAccountXp(AccountState state, long xp)
    {
        state.AccountXp += xp;
        while (state.AccountLevel < GameConfig.MaxAccountLevel
               && state.AccountXp >= GameConfig.XpForAccountLevel(state.AccountLevel))
        {
            state.AccountXp -= GameConfig.XpForAccountLevel(state.AccountLevel);
            state.AccountLevel++;
        }
    }
}
