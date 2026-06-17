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
    public object JoinRun(int tier, string? waifuId = null, long? seed = null, bool resume = false)
    {
        var tierDef = content.Tier(tier)
                      ?? throw new HubException("tier desconhecido");

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

        // A Kaeli da run é explícita: o cliente manda quem entra (tela de pré-run). Sem waifuId
        // (compat), cai na fixada (ActiveWaifuId) e por fim na starter.
        var (accountLevel, runWaifuId, ascension, bestiary, equipment, affinityXp, masteryNodes, skinId) =
            store.Read(s =>
            {
                var target = waifuId is { Length: > 0 } && s.OwnedWaifus.Contains(waifuId)
                    ? waifuId
                    : s.OwnedWaifus.Contains(s.ActiveWaifuId) ? s.ActiveWaifuId : Waifus.StarterWaifuId;
                if (waifuId is { Length: > 0 } && !s.OwnedWaifus.Contains(waifuId))
                    throw new HubException("Kaeli não recrutada");
                s.Equipment.TryGetValue(AccountState.EquipKey(target, tierDef.Tier), out var loadout);
                return (
                    s.AccountLevel,
                    target,
                    s.Ascension.GetValueOrDefault(target),
                    new Dictionary<string, long>(s.BestiaryKills),
                    loadout?.ToDictionary() ?? [],
                    s.AffinityXp.GetValueOrDefault(target),
                    s.Mastery.TryGetValue(target, out var mastery) ? mastery.Nodes.ToList() : [],
                    s.SelectedSkins.GetValueOrDefault(target));
            });

        if (accountLevel < tierDef.RequiredAccountLevel)
            throw new HubException($"requer conta nível {tierDef.RequiredAccountLevel}");

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
        var world = new GameWorld(
            runSeed, tierDef, waifu, ascension, data, monsters, bestiary, equipmentStats, kaeliLoadout, items);
        runs.StartRun(Context.ConnectionId, world);
        return new { seed = runSeed, tier = tierDef.Tier, tierName = tierDef.Name, waifuId = waifu.Id, resumed = false };
    }

    public void Move(int dx, int dy) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetMoveDir, dx, dy, null));

    public void SetTarget(int actorId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetTarget, actorId, 0, null));

    public void CastSkill(int slot) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.CastSkill, slot, 0, null));

    public void UsePotion() =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.UsePotion, 0, 0, null));

    public void ToggleStance() =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ToggleStance, 0, 0, null));

    public void SetAutoHelper(bool targeting, bool skills, bool ultimate, string targetPreference, string movementMode)
    {
        var flags = (targeting ? 1 : 0) | (skills ? 2 : 0) | (ultimate ? 4 : 0);
        var movement = movementMode switch
        {
            GameConfig.AutoHelperMovementModeFollow => GameConfig.AutoHelperMovementModeFollowCode,
            GameConfig.AutoHelperMovementModeAvoid => GameConfig.AutoHelperMovementModeAvoidCode,
            _ => GameConfig.AutoHelperMovementModeNoneCode
        };
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ToggleAutoHelper, flags, movement, targetPreference));
    }

    public void Interact(int x, int y) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.Interact, x, y, null));

    public void ChooseCard(string cardId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ChooseCard, 0, 0, cardId));

    public void Abandon() => runs.AbandonRun(Context.ConnectionId);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        runs.DropRun(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
