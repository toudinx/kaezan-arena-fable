using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Meta;

/// <summary>Applies a finished run to the account: currencies, xp, bestiary, inventory, dailies.</summary>
public sealed class RewardService(AccountStore store, DailyService dailies)
{
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
                        ? $"Contrato completo: {contract.Description}"
                        : $"Contrato {contract.Progress}/{contract.Target}: {contract.Description}");
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
