using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;
using Microsoft.AspNetCore.SignalR;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Builds a ready-to-tick <see cref="GameWorld"/> from account state + content. Extracted verbatim
/// from GameHub.JoinRun (Wave 3) so the idle-session orchestrator can chain runs through the exact
/// same path a manual join uses — loadout, ascension, biome and validation stay in one place.
/// </summary>
public sealed class RunFactory(
    AccountStore store,
    GameData data,
    MonsterRegistry monsters,
    KaeliRegistry kaelis,
    ItemRegistry items,
    ContentStore content,
    ILogger<RunFactory> logger)
{
    public GameWorld Create(int tier, string? waifuId, long? seed, GameMode mode)
    {
        var tierDef = content.Tier(tier)
                      ?? throw new HubException("unknown tier");

        // The run's Kaeli is explicit: the client sends who enters (pre-run screen). Without waifuId
        // (compat), falls back to the active one (ActiveWaifuId) and finally to the starter.
        var (accountLevel, runWaifuId, ascension, bestiary, equipment, affinityXp, masteryNodes, skinId, helperProfile) =
            store.Read(s =>
            {
                var target = waifuId is { Length: > 0 } && s.OwnedWaifus.Contains(waifuId)
                    ? waifuId
                    : s.OwnedWaifus.Contains(s.ActiveWaifuId) ? s.ActiveWaifuId : Waifus.StarterWaifuId;
                if (waifuId is { Length: > 0 } && !s.OwnedWaifus.Contains(waifuId))
                    throw new HubException("Kaeli not recruited");
                s.Equipment.TryGetValue(AccountState.EquipKey(target, tierDef.Tier), out var loadout);
                return (
                    s.AccountLevel,
                    target,
                    s.Ascension.GetValueOrDefault(target),
                    new Dictionary<string, long>(s.BestiaryKills),
                    loadout?.ToDictionary() ?? [],
                    s.AffinityXp.GetValueOrDefault(target),
                    s.Mastery.TryGetValue(target, out var mastery) ? mastery.Nodes.ToList() : [],
                    s.SelectedSkins.GetValueOrDefault(target),
                    s.HelperProfiles.GetValueOrDefault(target, ""));
            });

        if (accountLevel < tierDef.RequiredAccountLevel)
            throw new HubException($"requires account level {tierDef.RequiredAccountLevel}");

        var waifu = kaelis.Find(runWaifuId) ?? kaelis.ById[Waifus.StarterWaifuId];
        var runSeed = seed ?? Random.Shared.NextInt64(1, long.MaxValue);

        var skin = skinId is not null
            ? waifu.Skins.FirstOrDefault(s => s.Id == skinId) ?? waifu.DefaultSkin
            : waifu.DefaultSkin;
        var kaeliLoadout = new KaeliLoadout(
            KaeliService.AffinityLevelFor(affinityXp),
            Mastery.Aggregate(waifu.Id, masteryNodes),
            skin);

        var equipmentStats = EquipmentStatAggregator.Aggregate(equipment, items.All);
        // LM-08: biome resolved from ContentStore (editable in admin); falls back to canonical defaults.
        var biome = content.Biome(tierDef.Tier) ?? Biomes.ForTier(tierDef.Tier);
        var creationStart = System.Diagnostics.Stopwatch.GetTimestamp();
        var world = new GameWorld(
            runSeed, tierDef, waifu, ascension, data, monsters, bestiary, equipmentStats, kaeliLoadout, items,
            helperProfile, content.RoleTunings, mode, biome);
        logger.LogInformation("run created in {Ms:F0}ms (tier {Tier}, seed {Seed})",
            System.Diagnostics.Stopwatch.GetElapsedTime(creationStart).TotalMilliseconds, tierDef.Tier, runSeed);
        return world;
    }
}
