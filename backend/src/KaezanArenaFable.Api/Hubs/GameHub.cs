using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;
using KaezanArenaFable.Api.Meta;
using Microsoft.AspNetCore.SignalR;

namespace KaezanArenaFable.Api.Hubs;

/// <summary>Realtime game channel: one active run per connection.</summary>
public sealed class GameHub(RunManager runs, GameData data, AccountStore store) : Hub
{
    public object JoinRun(int tier, long? seed)
    {
        var tierDef = GameConfig.Tiers.FirstOrDefault(t => t.Tier == tier)
                      ?? throw new HubException("tier desconhecido");

        var (accountLevel, waifuId, ascension, bestiary) = store.Read(s =>
            (s.AccountLevel, s.ActiveWaifuId, s.Ascension.GetValueOrDefault(s.ActiveWaifuId), new Dictionary<string, long>(s.BestiaryKills)));

        if (accountLevel < tierDef.RequiredAccountLevel)
            throw new HubException($"requer conta nível {tierDef.RequiredAccountLevel}");

        var waifu = Waifus.ById.GetValueOrDefault(waifuId) ?? Waifus.ById[Waifus.StarterWaifuId];
        var runSeed = seed ?? Random.Shared.NextInt64(1, long.MaxValue);

        var world = new GameWorld(runSeed, tierDef, waifu, ascension, data, bestiary);
        runs.StartRun(Context.ConnectionId, world);
        return new { seed = runSeed, tier = tierDef.Tier, tierName = tierDef.Name, waifuId = waifu.Id };
    }

    public void Move(int dx, int dy) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetMoveDir, dx, dy, null));

    public void SetTarget(int actorId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.SetTarget, actorId, 0, null));

    public void CastSkill(int slot) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.CastSkill, slot, 0, null));

    public void Interact(int x, int y) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.Interact, x, y, null));

    public void ChooseCard(string cardId) =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.ChooseCard, 0, 0, cardId));

    public void Abandon() =>
        runs.GetRun(Context.ConnectionId)?.Enqueue(new Command(CommandKind.Abandon, 0, 0, null));

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        runs.DropRun(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
