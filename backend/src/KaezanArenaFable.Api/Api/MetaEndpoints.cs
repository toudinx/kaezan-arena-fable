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
            itemFallbackSalePrice = GameConfig.ItemFallbackSalePrice,
            items = data.Items.Values.Select(i => new
            {
                i.ItemId,
                i.Name,
                salePrice = data.ItemValue(i.ItemId),
                i.Slot,
                i.WeaponType,
                i.Attack,
                i.Armor,
                i.Defense,
                i.MountLookType,
                i.MountSpeed
            }),
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
                s.Equipment,
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
        api.MapPost("/items/sell", (SellRequest req, AccountStore store, GameData data) =>
            store.Mutate(s =>
            {
                if (!s.Inventory.TryGetValue(req.ItemId, out var stack) || stack.Count < req.Count || req.Count <= 0)
                    return Results.BadRequest(new { error = "quantidade inválida" });
                stack.Count -= req.Count;
                if (stack.Count == 0) s.Inventory.Remove(req.ItemId);
                var gold = (long)req.Count * data.ItemValue(req.ItemId);
                s.Gold += gold;
                return Results.Ok(new { goldGained = gold, s.Gold });
            }));

        api.MapPost("/equipment/equip", (EquipRequest req, AccountStore store, GameData data) =>
            store.Mutate(s =>
            {
                if (!s.OwnedWaifus.Contains(req.WaifuId))
                    return Results.BadRequest(new { error = "Kaeli não possuída" });
                if (!EquipmentSlots.IsValid(req.Slot))
                    return Results.BadRequest(new { error = "slot inválido" });
                if (!data.Items.TryGetValue(req.ItemId, out var item) || item.Slot != req.Slot)
                    return Results.BadRequest(new { error = "item incompatível com o slot" });
                if (!s.Inventory.TryGetValue(req.ItemId, out var stack) || stack.Count <= 0)
                    return Results.BadRequest(new { error = "item não está na Mochila" });

                if (!s.Equipment.TryGetValue(req.WaifuId, out var loadout))
                    s.Equipment[req.WaifuId] = loadout = [];

                stack.Count--;
                if (stack.Count == 0) s.Inventory.Remove(req.ItemId);

                if (loadout.TryGetValue(req.Slot, out var previousItemId)
                    && data.Items.TryGetValue(previousItemId, out var previous))
                {
                    if (s.Inventory.TryGetValue(previousItemId, out var previousStack))
                        previousStack.Count++;
                    else
                        s.Inventory[previousItemId] = new InventoryStack
                        {
                            ItemId = previous.ItemId,
                            Name = previous.Name,
                            Count = 1
                        };
                }

                loadout[req.Slot] = req.ItemId;
                return Results.Ok(new { req.WaifuId, req.Slot, req.ItemId });
            }));

        api.MapPost("/equipment/unequip", (UnequipRequest req, AccountStore store, GameData data) =>
            store.Mutate(s =>
            {
                if (!EquipmentSlots.IsValid(req.Slot))
                    return Results.BadRequest(new { error = "slot inválido" });
                if (!s.Equipment.TryGetValue(req.WaifuId, out var loadout)
                    || !loadout.Remove(req.Slot, out var itemId)
                    || !data.Items.TryGetValue(itemId, out var item))
                    return Results.BadRequest(new { error = "slot vazio" });

                if (s.Inventory.TryGetValue(itemId, out var stack))
                    stack.Count++;
                else
                    s.Inventory[itemId] = new InventoryStack
                    {
                        ItemId = item.ItemId,
                        Name = item.Name,
                        Count = 1
                    };

                if (loadout.Count == 0) s.Equipment.Remove(req.WaifuId);
                return Results.Ok(new { req.WaifuId, req.Slot, itemId });
            }));
    }

    public sealed record ActiveWaifuRequest(string WaifuId);
    public sealed record PullRequest(string BannerId, int Count);
    public sealed record AscendRequest(string WaifuId);
    public sealed record ClaimRequest(string ContractId);
    public sealed record SellRequest(int ItemId, int Count);
    public sealed record EquipRequest(string WaifuId, string Slot, int ItemId);
    public sealed record UnequipRequest(string WaifuId, string Slot);
}
