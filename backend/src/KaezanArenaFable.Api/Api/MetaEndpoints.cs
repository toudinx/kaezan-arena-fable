using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Api;

public static class MetaEndpoints
{
    public static void MapMetaEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // ---- catalog (static game data for the client) ----
        api.MapGet("/catalog", (GameData data) => Results.Ok(new
        {
            waifus = Waifus.All,
            classes = Classes.All,
            skills = Classes.Skills.Values,
            cards = Cards.All,
            tiers = GameConfig.Tiers,
            banners = GachaService.Banners,
            pullCost = GameConfig.PullCostKaeros,
            ascensionShardCost = GameConfig.AscensionShardCost,
            addonAscensions = new[] { GameConfig.AddonOneAscension, GameConfig.AddonTwoAscension },
            bestiaryRanks = GameConfig.BestiaryRankKills,
            monsters = data.Monsters.Values.Select(m => new
            {
                m.Name,
                m.Description,
                m.Health,
                m.Experience,
                m.IsBoss,
                m.BestiaryClass,
                m.Outfit,
                loot = m.Loot.Select(l => new { l.ItemId, l.Name, l.Chance })
            })
        }));

        // ---- account ----
        api.MapGet("/account", (AccountStore store, DailyService dailies) =>
        {
            var contracts = dailies.GetToday();
            return Results.Ok(store.Read(s => new
            {
                s.Id,
                s.AccountLevel,
                s.AccountXp,
                accountXpNext = GameConfig.XpForAccountLevel(s.AccountLevel),
                s.Gold,
                s.Kaeros,
                s.OwnedWaifus,
                s.Shards,
                s.Ascension,
                s.ActiveWaifuId,
                s.BestiaryKills,
                inventory = s.Inventory.Values,
                s.RunsPlayed,
                s.RunsWon,
                s.TierClears,
                pity = s.Pity,
                dailies = contracts
            }));
        });

        api.MapPost("/account/active-waifu", (ActiveWaifuRequest req, AccountStore store) =>
        {
            if (!Waifus.ById.ContainsKey(req.WaifuId)) return Results.BadRequest(new { error = "waifu desconhecida" });
            return store.Mutate(s =>
            {
                if (!s.OwnedWaifus.Contains(req.WaifuId)) return Results.BadRequest(new { error = "não possuída" });
                s.ActiveWaifuId = req.WaifuId;
                return Results.Ok(new { s.ActiveWaifuId });
            });
        });

        // ---- gacha ----
        api.MapPost("/gacha/pull", (PullRequest req, GachaService gacha) =>
        {
            try { return Results.Ok(gacha.Pull(req.BannerId, req.Count)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        api.MapPost("/waifus/ascend", (AscendRequest req, GachaService gacha) =>
        {
            try { return Results.Ok(gacha.Ascend(req.WaifuId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ---- dailies ----
        api.MapPost("/dailies/claim", (ClaimRequest req, DailyService dailies) =>
        {
            try { return Results.Ok(dailies.Claim(req.ContractId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ---- inventory ----
        api.MapPost("/items/sell", (SellRequest req, AccountStore store) =>
            store.Mutate(s =>
            {
                if (!s.Inventory.TryGetValue(req.ItemId, out var stack) || stack.Count < req.Count || req.Count <= 0)
                    return Results.BadRequest(new { error = "quantidade inválida" });
                stack.Count -= req.Count;
                if (stack.Count == 0) s.Inventory.Remove(req.ItemId);
                var gold = (long)req.Count * ItemValue(req.ItemId);
                s.Gold += gold;
                return Results.Ok(new { goldGained = gold, s.Gold });
            }));
    }

    /// <summary>Flat sell value; loot rarity comes from drop chance, not price tables.</summary>
    private static int ItemValue(int itemId) => 15 + itemId % 35;

    public sealed record ActiveWaifuRequest(string WaifuId);
    public sealed record PullRequest(string BannerId, int Count);
    public sealed record AscendRequest(string WaifuId);
    public sealed record ClaimRequest(string ContractId);
    public sealed record SellRequest(int ItemId, int Count);
}
