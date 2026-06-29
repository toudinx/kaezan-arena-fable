using System.Security.Cryptography;
using System.Text;
using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// 3 deterministic contracts per UTC day (kaezan-arena Daily Contracts idea,
/// progression fed by run events in the style of the Kaezan Daily Hub).
/// </summary>
public sealed class DailyService(AccountStore store, MonsterRegistry monsters, ContentStore content)
{
    public List<DailyContract> GetToday()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return store.Mutate(state =>
        {
            if (state.DailyDate != today)
            {
                state.DailyDate = today;
                state.Dailies = Generate(state.Id, today);
            }
            else
            {
                foreach (var contract in state.Dailies)
                    contract.Description = DescriptionFor(contract);
            }
            return state.Dailies.Select(Clone).ToList();
        });
    }

    private static DailyContract Clone(DailyContract c) => new()
    {
        Id = c.Id, Kind = c.Kind, Param = c.Param, Description = c.Description,
        Target = c.Target, Progress = c.Progress, Claimed = c.Claimed
    };

    private List<DailyContract> Generate(string accountId, string date)
    {
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{accountId}:{date}"));
        var rng = new Rng(BitConverter.ToUInt64(seedBytes, 0));

        var contracts = new List<DailyContract>();

        // 1) kill contract: species from a random tier the player could plausibly reach
        var tiers = content.Tiers;
        var tier = tiers[rng.Next(Math.Min(3, tiers.Count))];
        var species = rng.Pick(tier.CommonMobs.Concat(tier.EliteMobs).ToList());
        var killTarget = rng.Range(15, 30);
        contracts.Add(new DailyContract
        {
            Id = $"daily:{date}:kill",
            Kind = "kill_species",
            Param = species,
            Target = killTarget,
            Description = KillDescription(species, killTarget)
        });

        // 2) clear contract
        var clearTier = tiers[rng.Next(Math.Min(3, tiers.Count))];
        contracts.Add(new DailyContract
        {
            Id = $"daily:{date}:clear",
            Kind = "clear_tier",
            Param = clearTier.Tier.ToString(),
            Target = 1,
            Description = ClearDescription(clearTier.Tier)
        });

        // 3) one of: chests or gold
        if (rng.Chance(0.5))
        {
            contracts.Add(new DailyContract
            {
                Id = $"daily:{date}:chests",
                Kind = "open_chests",
                Param = "",
                Target = 3,
                Description = "Open 3 chests in dungeons"
            });
        }
        else
        {
            var goldTarget = rng.Range(300, 600);
            contracts.Add(new DailyContract
            {
                Id = $"daily:{date}:gold",
                Kind = "collect_gold",
                Param = "",
                Target = goldTarget,
                Description = $"Collect {goldTarget} gold in runs"
            });
        }

        return contracts;
    }

    private string DescriptionFor(DailyContract contract) => contract.Kind switch
    {
        "kill_species" => KillDescription(contract.Param, contract.Target),
        "clear_tier" => int.TryParse(contract.Param, out var tier)
            ? ClearDescription(tier)
            : contract.Description,
        "open_chests" => "Open 3 chests in dungeons",
        "collect_gold" => $"Collect {contract.Target} gold in runs",
        _ => contract.Description
    };

    private string KillDescription(string species, long target)
    {
        var monster = monsters.Get(species);
        return $"Defeat {target}x {monster.Name} ({monster.BestiaryClass})";
    }

    private string ClearDescription(int tier)
    {
        var tierName = content.Tier(tier)?.Name ?? $"Tier {tier}";
        return $"Clear the dungeon \"{tierName}\" (defeat the boss)";
    }

    public object Claim(string contractId)
    {
        GetToday(); // ensure today's contracts exist
        return store.Mutate(state =>
        {
            var contract = state.Dailies.FirstOrDefault(c => c.Id == contractId)
                           ?? throw new ArgumentException("contract not found");
            if (contract.Claimed) throw new InvalidOperationException("already claimed");
            if (contract.Progress < contract.Target) throw new InvalidOperationException("incomplete");
            contract.Claimed = true;
            state.Kaeros += GameConfig.DailyKaerosReward;
            state.Gold += GameConfig.DailyGoldReward;
            RewardService.GrantAccountXp(state, GameConfig.DailyAccountXpReward);
            return new
            {
                contractId,
                kaeros = GameConfig.DailyKaerosReward,
                gold = GameConfig.DailyGoldReward,
                accountXp = GameConfig.DailyAccountXpReward,
                kaerosTotal = state.Kaeros
            };
        });
    }
}
