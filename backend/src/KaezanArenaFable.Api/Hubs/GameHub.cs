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
    AccountStore store,
    ContentStore content) : Hub
{
    public object JoinRun(int tier, long? seed = null, bool resume = false)
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

        var (accountLevel, waifuId, ascension, bestiary, equipment, affinityXp, masteryNodes, skinId) =
            store.Read(s =>
            {
                s.Equipment.TryGetValue(s.ActiveWaifuId, out var loadout);
                return (
                    s.AccountLevel,
                    s.ActiveWaifuId,
                    s.Ascension.GetValueOrDefault(s.ActiveWaifuId),
                    new Dictionary<string, long>(s.BestiaryKills),
                    loadout?.ToDictionary() ?? [],
                    s.AffinityXp.GetValueOrDefault(s.ActiveWaifuId),
                    s.Mastery.TryGetValue(s.ActiveWaifuId, out var mastery) ? mastery.Nodes.ToList() : [],
                    s.SelectedSkins.GetValueOrDefault(s.ActiveWaifuId));
            });

        if (accountLevel < tierDef.RequiredAccountLevel)
            throw new HubException($"requer conta nível {tierDef.RequiredAccountLevel}");

        var waifu = kaelis.Find(waifuId) ?? kaelis.ById[Waifus.StarterWaifuId];
        var runSeed = seed ?? Random.Shared.NextInt64(1, long.MaxValue);

        var skin = skinId is not null
            ? waifu.Skins.FirstOrDefault(s => s.Id == skinId) ?? waifu.DefaultSkin
            : waifu.DefaultSkin;
        var kaeliLoadout = new KaeliLoadout(
            KaeliService.AffinityLevelFor(affinityXp),
            Mastery.Aggregate(waifu.Id, masteryNodes),
            skin);

        var equipmentStats = EquipmentStatAggregator.Aggregate(equipment, data.Items);
        var world = new GameWorld(
            runSeed, tierDef, waifu, ascension, data, monsters, bestiary, equipmentStats, kaeliLoadout);
        runs.StartRun(Context.ConnectionId, world);
        return new { seed = runSeed, tier = tierDef.Tier, tierName = tierDef.Name, waifuId = waifu.Id, resumed = false };
    }

    public void Move(int dx, int dy) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetMoveDir, dx, dy, null));

    public void SetTarget(int actorId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetTarget, actorId, 0, null));

    public void CastSkill(int slot) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.CastSkill, slot, 0, null));

    public void ToggleStance() =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ToggleStance, 0, 0, null));

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
