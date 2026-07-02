using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;
using KaezanArenaFable.Api.Meta;
using Microsoft.AspNetCore.SignalR;

namespace KaezanArenaFable.Api.Hubs;

/// <summary>Realtime game channel: one active run per connection.</summary>
public sealed class GameHub(
    RunManager runs,
    GameData data,
    MonsterRegistry monsters,
    KaeliRegistry kaelis,
    ItemRegistry items,
    AccountStore store,
    ContentStore content) : Hub
{
    // LM-03: `mode` is optional, defaulting to Dungeon — the current client (sending 4 args) enters
    // legacy mode unchanged. Arena (LM-04) will pass GameMode.Arena here.
    public object JoinRun(int tier, string? waifuId = null, long? seed = null, bool resume = false,
        GameMode mode = GameMode.Dungeon)
    {
        var tierDef = content.Tier(tier)
                      ?? throw new HubException("unknown tier");

        if (resume && runs.TryResumeRun(Context.ConnectionId, out var resumedWorld) && resumedWorld is not null)
        {
            return new
            {
                seed = resumedWorld.Seed,
                tier = resumedWorld.Tier.Tier,
                tierName = resumedWorld.Tier.Name,
                waifuId = resumedWorld.Waifu.Id,
                resumed = true
            };
        }

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
        var world = new GameWorld(
            runSeed, tierDef, waifu, ascension, data, monsters, bestiary, equipmentStats, kaeliLoadout, items,
            helperProfile, content.RoleTunings, mode, biome);
        runs.StartRun(Context.ConnectionId, world);
        return new { seed = runSeed, tier = tierDef.Tier, tierName = tierDef.Name, waifuId = waifu.Id, mode, resumed = false };
    }

    public void Move(int dx, int dy) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetMoveDir, dx, dy, null));

    public void SetTarget(int actorId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetTarget, actorId, 0, null));

    public void CastSkill(int slot) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.CastSkill, slot, 0, null));

    public void UsePotion() =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.UsePotion, 0, 0, null));

    // Dash/Dodge (Shift): dx/dy = desired direction (0,0 = uses movement/facing direction in the engine).
    public void Dash(int dx, int dy) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.Dash, dx, dy, null));

    public void ToggleStance() =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ToggleStance, 0, 0, null));

    public void SetAutoHelper(
        bool targeting, bool skills, bool ultimate, string targetPreference, string movementMode,
        bool autoHeal, int autoHealPct, string navMode, bool autoCards)
    {
        var flags = (targeting ? 1 : 0) | (skills ? 2 : 0) | (ultimate ? 4 : 0)
                    | (autoHeal ? GameConfig.AutoHelperAutoHealFlag : 0)
                    | (autoCards ? GameConfig.AutoHelperAutoCardsFlag : 0);
        var movement = movementMode switch
        {
            GameConfig.AutoHelperMovementModeFollow => GameConfig.AutoHelperMovementModeFollowCode,
            GameConfig.AutoHelperMovementModeAvoid => GameConfig.AutoHelperMovementModeAvoidCode,
            _ => GameConfig.AutoHelperMovementModeNoneCode
        };
        // S = "targetPreference|navMode|healPct" (a single string field in the command).
        var payload = $"{targetPreference}|{GameConfig.NormalizeAutoHelperNav(navMode)}|{GameConfig.ClampHealPct(autoHealPct)}";
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ToggleAutoHelper, flags, movement, payload));
    }

    // Training Room sandbox toggle: when on, skills and the ultimate ignore cooldown/gauge.
    // The engine ignores this outside Training mode, so it is always safe to send.
    public void SetTrainingFreeCast(bool enabled) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetTrainingFreeCast, enabled ? 1 : 0, 0, null));

    // G-10: persists the helper config as the default for the run's Kaeli (loaded on the next JoinRun).
    public void SaveHelperProfile()
    {
        var run = runs.GetRun(Context.ConnectionId);
        if (run is null) return;
        var encoded = run.EncodeHelperProfile();
        var waifuId = run.Waifu.Id;
        store.Mutate(s => s.HelperProfiles[waifuId] = encoded);
    }

    public void Interact(int x, int y) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.Interact, x, y, null));

    public void ChooseCard(string cardId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ChooseCard, 0, 0, cardId));

    public void RerollCards() =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.RerollCards, 0, 0, null));

    public void BanCard(string cardId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.BanCard, 0, 0, cardId));

    public void Abandon() => runs.AbandonRun(Context.ConnectionId);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        runs.DropRun(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
