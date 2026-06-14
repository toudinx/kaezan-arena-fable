using KaezanArenaFable.Api.Content;
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
        api.MapGet("/catalog", (
            GameData data, ContentStore content, MonsterRegistry registry, KaeliRegistry kaelis,
            ItemRegistry itemRegistry) => Results.Ok(new
        {
            waifus = kaelis.All,
            classes = Classes.All,
            skills = Classes.Skills.Values,
            cards = Cards.All,
            tiers = content.Tiers,
            banners = GachaService.Banners,
            pullCost = GameConfig.PullCostKaeros,
            ascensionShardCost = GameConfig.AscensionShardCost,
            addonAscensions = new[] { GameConfig.AddonOneAscension, GameConfig.AddonTwoAscension },
            bestiaryRanks = GameConfig.BestiaryRankKills,
            itemFallbackSalePrice = GameConfig.ItemFallbackSalePrice,
            masteryTrees = Mastery.TreeByWaifu,
            affinity = new
            {
                maxLevel = GameConfig.AffinityMaxLevel,
                xpPerLevel = Enumerable.Range(1, GameConfig.AffinityMaxLevel - 1)
                    .Select(GameConfig.XpForAffinityLevel).ToArray(),
                statBonusPerLevel = GameConfig.AffinityStatBonusPerLevel,
                loreLevels = GameConfig.AffinityLoreLevels,
                kaerosRewards = GameConfig.AffinityKaerosRewards,
                giftsPerDay = GameConfig.GiftsPerKaeliPerDay,
                giftFavoriteMultiplier = GameConfig.GiftFavoriteMultiplier,
                giftBaseXp = GameConfig.GiftBaseXp,
                giftXpPerGold = GameConfig.GiftXpPerGold,
                giftXpCap = GameConfig.GiftXpCap
            },
            mastery = new
            {
                respecGold = GameConfig.MasteryRespecGold,
                pointsPerVictory = GameConfig.MasteryPointsPerVictory,
                pointsPerDefeat = GameConfig.MasteryPointsPerDefeat
            },
            items = itemRegistry.All.Values.Select(i => new
            {
                i.ItemId,
                i.Name,
                salePrice = itemRegistry.Value(i.ItemId),
                i.Slot,
                i.WeaponType,
                i.Attack,
                i.Armor,
                i.Defense,
                i.MountLookType,
                i.MountSpeed,
                i.Description,
                appearanceItemId = i.AppearanceItemId,
                i.SourceItemId,
                i.IsAuthored,
                i.Element,
                i.ElementDamage,
                i.SkillPower,
                i.CritChance,
                i.CritDamage,
                i.LifeStealChance,
                i.LifeStealAmount,
                i.CooldownReduction,
                i.MoveSpeedPercent,
                i.PhysicalResistance,
                i.FireResistance,
                i.IceResistance,
                i.EarthResistance,
                i.EnergyResistance,
                i.DeathResistance,
                i.HolyResistance,
                allowedClassIds = i.AllowedClassIds ?? [],
                i.RequiredMasteryPoints
            }),
            monsters = registry.All.Select(m => new
            {
                id = m.StableId,
                m.Name,
                m.Description,
                m.Health,
                m.Experience,
                m.IsBoss,
                m.BestiaryClass,
                m.Origin,
                m.BossRace,
                m.Corpse,
                m.Outfit,
                source = m.IsAuthored ? "authored" : "legacy",
                m.Rank,
                m.Element,
                m.BehaviorId,
                m.StatPresetId,
                m.HpMultiplier,
                m.DamageMultiplier,
                m.SpeedMultiplier,
                m.CadenceMultiplier,
                m.PowerTier,
                resistances = m.Elements,
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
                s.AffinityXp,
                affinity = s.OwnedWaifus.ToDictionary(id => id, id =>
                {
                    var xp = s.AffinityXp.GetValueOrDefault(id);
                    var (into, toNext) = KaeliService.AffinityProgress(xp);
                    return new { level = KaeliService.AffinityLevelFor(xp), xpIntoLevel = into, xpToNext = toNext };
                }),
                giftsToday = s.GiftsDate == DateTime.UtcNow.ToString("yyyy-MM-dd")
                    ? s.GiftsToday
                    : new Dictionary<string, int>(),
                s.OwnedSkins,
                s.SelectedSkins,
                mastery = s.Mastery,
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

        // ---- kaeli depth: presentes / skins / maestria ----
        api.MapPost("/kaelis/gift", (GiftRequest req, KaeliService kaelis) =>
        {
            try { return Results.Ok(kaelis.Gift(req.WaifuId, req.ItemId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        api.MapPost("/kaelis/skin/select", (SkinRequest req, KaeliService kaelis) =>
        {
            try { return Results.Ok(kaelis.SelectSkin(req.WaifuId, req.SkinId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        api.MapPost("/kaelis/skin/buy", (SkinRequest req, KaeliService kaelis) =>
        {
            try { return Results.Ok(kaelis.BuySkin(req.WaifuId, req.SkinId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        api.MapPost("/kaelis/mastery/unlock", (MasteryNodeRequest req, KaeliService kaelis) =>
        {
            try { return Results.Ok(kaelis.UnlockMasteryNode(req.WaifuId, req.NodeId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        api.MapPost("/kaelis/mastery/respec", (MasteryRespecRequest req, KaeliService kaelis) =>
        {
            try { return Results.Ok(kaelis.RespecMastery(req.WaifuId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ---- dailies ----
        api.MapPost("/dailies/claim", (ClaimRequest req, DailyService dailies) =>
        {
            try { return Results.Ok(dailies.Claim(req.ContractId)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ---- inventory ----
        api.MapPost("/items/sell", (SellRequest req, AccountStore store, ItemRegistry itemRegistry) =>
            store.Mutate(s =>
            {
                if (!s.Inventory.TryGetValue(req.ItemId, out var stack) || stack.Count < req.Count || req.Count <= 0)
                    return Results.BadRequest(new { error = "quantidade inválida" });
                stack.Count -= req.Count;
                if (stack.Count == 0) s.Inventory.Remove(req.ItemId);
                var gold = (long)req.Count * itemRegistry.Value(req.ItemId);
                s.Gold += gold;
                return Results.Ok(new { goldGained = gold, s.Gold });
            }));

        api.MapPost("/equipment/equip", (
            EquipRequest req, AccountStore store, ItemRegistry items, KaeliRegistry kaelis) =>
            store.Mutate(s =>
            {
                if (!s.OwnedWaifus.Contains(req.WaifuId))
                    return Results.BadRequest(new { error = "Kaeli não possuída" });
                if (!EquipmentSlots.IsValid(req.Slot))
                    return Results.BadRequest(new { error = "slot inválido" });
                if (items.Get(req.ItemId) is not { } item || item.Slot != req.Slot)
                    return Results.BadRequest(new { error = "item incompatível com o slot" });
                if (kaelis.Find(req.WaifuId) is not { } waifu)
                    return Results.BadRequest(new { error = "Kaeli desconhecida" });
                if (item.AllowedClassIds is { Count: > 0 }
                    && !item.AllowedClassIds.Contains(waifu.ClassId, StringComparer.OrdinalIgnoreCase))
                    return Results.BadRequest(new { error = $"{item.Name} nao pode ser usado pela classe {waifu.ClassId}" });
                var masteryPoints = s.Mastery.TryGetValue(req.WaifuId, out var mastery)
                    ? mastery.Points + mastery.Spent
                    : 0;
                if (masteryPoints < item.RequiredMasteryPoints)
                    return Results.BadRequest(new
                    {
                        error = $"{item.Name} exige {item.RequiredMasteryPoints} pontos totais de maestria"
                    });
                if (!s.Inventory.TryGetValue(req.ItemId, out var stack) || stack.Count <= 0)
                    return Results.BadRequest(new { error = "item não está na Mochila" });

                if (!s.Equipment.TryGetValue(req.WaifuId, out var loadout))
                    s.Equipment[req.WaifuId] = loadout = [];

                stack.Count--;
                if (stack.Count == 0) s.Inventory.Remove(req.ItemId);

                if (loadout.TryGetValue(req.Slot, out var previousItemId)
                    && items.Get(previousItemId) is { } previous)
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

        api.MapPost("/equipment/unequip", (UnequipRequest req, AccountStore store, ItemRegistry items) =>
            store.Mutate(s =>
            {
                if (!EquipmentSlots.IsValid(req.Slot))
                    return Results.BadRequest(new { error = "slot inválido" });
                if (!s.Equipment.TryGetValue(req.WaifuId, out var loadout)
                    || !loadout.Remove(req.Slot, out var itemId)
                    || items.Get(itemId) is not { } item)
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

        // ---- admin: autoria de conteúdo (editor in-game) ----
        var admin = api.MapGroup("/admin");

        // dados de apoio pro editor: lista de mobs e bosses disponíveis (de monsters.json)
        admin.MapGet("/monsters", (MonsterRegistry registry) => Results.Ok(
            registry.All
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(m => new
                {
                    id = m.StableId,
                    m.Name,
                    m.IsBoss,
                    m.BestiaryClass,
                    m.Origin,
                    m.BossRace,
                    m.Health,
                    m.Experience,
                    source = m.IsAuthored ? "authored" : "legacy"
                })));

        admin.MapGet("/monster-authoring", (GameData data) => Results.Ok(new
        {
            behaviors = GameConfig.MonsterBehaviorProfiles,
            elements = GameConfig.MonsterElementProfiles,
            presets = GameConfig.MonsterStatPresets,
            statLines = GameConfig.MonsterStatLines,
            modifierMin = GameConfig.AuthoredModifierMin,
            modifierMax = GameConfig.AuthoredModifierMax,
            resistanceMin = GameConfig.AuthoredResistanceMin,
            resistanceMax = GameConfig.AuthoredResistanceMax,
            appearances = data.MonsterAppearances
        }));

        admin.MapGet("/content/monsters", (ContentStore content) => Results.Ok(content.Monsters));

        admin.MapPost("/content/monsters", (MonsterDefinition request, ContentStore content, GameData data) =>
        {
            var definition = MonsterAuthoring.Normalize(request);
            var error = ValidateMonsterDefinition(definition, content, data);
            if (error is not null) return Results.BadRequest(new { error });
            try { return Results.Ok(content.CreateMonster(definition)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        admin.MapPut("/content/monsters/{id}", (
            string id, MonsterDefinition request, ContentStore content, GameData data) =>
        {
            var definition = MonsterAuthoring.Normalize(request, id);
            var error = ValidateMonsterDefinition(definition, content, data, id);
            if (error is not null) return Results.BadRequest(new { error });
            try { return Results.Ok(content.UpdateMonster(id, definition)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        });

        admin.MapDelete("/content/monsters/{id}", (string id, ContentStore content) =>
        {
            try
            {
                var removed = content.DeleteMonster(id);
                return Results.Ok(new { removed.Id, removed.Name });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        admin.MapGet("/content/tiers", (ContentStore content) => Results.Ok(content.Tiers));

        admin.MapPut("/content/tiers", (List<DungeonTier> tiers, ContentStore content, MonsterRegistry registry) =>
        {
            // validação de conteúdo (mobs/boss existem) antes de persistir
            if (tiers is null || tiers.Count == 0)
                return Results.BadRequest(new { error = "envie ao menos um tier" });
            if (tiers.Select(t => t.Tier).Distinct().Count() != tiers.Count)
                return Results.BadRequest(new { error = "números de tier duplicados" });

            foreach (var t in tiers)
            {
                if (string.IsNullOrWhiteSpace(t.Name))
                    return Results.BadRequest(new { error = $"tier {t.Tier}: nome vazio" });
                if (t.CommonMobs.Length == 0)
                    return Results.BadRequest(new { error = $"tier {t.Tier}: precisa de ao menos 1 mob comum" });

                foreach (var mob in t.CommonMobs.Concat(t.EliteMobs))
                    if (!registry.Contains(mob))
                        return Results.BadRequest(new { error = $"tier {t.Tier}: mob desconhecido '{mob}'" });

                // boss pode ser qualquer monstro — o engine escala o HP de quem for escolhido
                if (!registry.Contains(t.Boss))
                    return Results.BadRequest(new { error = $"tier {t.Tier}: boss desconhecido '{t.Boss}'" });
            }

            return Results.Ok(content.ReplaceTiers(tiers));
        });

        // ---- admin: Outfit Studio (skins autorais de Kaeli) ----

        // metadados de apoio: roster (pra atribuir a skin), regras de desbloqueio e tamanho da paleta
        admin.MapGet("/kaeli-authoring", () => Results.Ok(new
        {
            kaelis = Waifus.All.Select(w => new
            {
                w.Id,
                w.Name,
                w.Title,
                w.Rarity,
                w.Element,
                w.ClassId,
                defaultSkin = new { w.DefaultSkin.LookType, w.DefaultSkin.Head, w.DefaultSkin.Body, w.DefaultSkin.Legs, w.DefaultSkin.Feet }
            }),
            unlockKinds = KaeliAuthoring.UnlockKinds,
            outfitColorCount = KaeliAuthoring.OutfitColorCount,
            affinityMaxLevel = GameConfig.AffinityMaxLevel
        }));

        admin.MapGet("/content/kaeli-skins", (ContentStore content) => Results.Ok(content.AuthoredKaeliSkins));

        admin.MapPost("/content/kaeli-skins", (KaeliSkinDefinition request, ContentStore content, KaeliRegistry kaelis) =>
        {
            var definition = KaeliAuthoring.Normalize(request);
            var error = ValidateKaeliSkin(definition, kaelis);
            if (error is not null) return Results.BadRequest(new { error });
            try { return Results.Ok(content.CreateKaeliSkin(definition)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        admin.MapPut("/content/kaeli-skins/{id}", (
            string id, KaeliSkinDefinition request, ContentStore content, KaeliRegistry kaelis) =>
        {
            var definition = KaeliAuthoring.Normalize(request, id);
            var error = ValidateKaeliSkin(definition, kaelis, id);
            if (error is not null) return Results.BadRequest(new { error });
            try { return Results.Ok(content.UpdateKaeliSkin(id, definition)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        });

        admin.MapDelete("/content/kaeli-skins/{id}", (string id, ContentStore content) =>
        {
            try
            {
                var removed = content.DeleteKaeliSkin(id);
                return Results.Ok(new { removed.Id, removed.Name });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // ---- admin: Item Studio (biblioteca Canary + CRUD de itens Kaezan) ----

        admin.MapGet("/items", (GameData data, ContentStore content, ItemRegistry items) =>
            Results.Ok(new
            {
                library = data.Items.Values
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => ItemView(item, data)),
                authored = content.AuthoredItems
                    .Select(definition => items.Get(definition.ItemId))
                    .Where(item => item is not null)
                    .Cast<ItemType>()
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => ItemView(item, data)),
                classes = Classes.All.Select(item => new { item.Id, item.Name }),
                elements = ItemAuthoring.Elements
            }));

        admin.MapPost("/items", (
            AuthoredItemDefinition request, ContentStore content, GameData data, ItemRegistry items) =>
        {
            var definition = ItemAuthoring.Normalize(request with { ItemId = 0 });
            var source = data.Items.GetValueOrDefault(definition.SourceItemId);
            var error = ItemAuthoring.Validate(definition, source, content.AuthoredItems);
            if (error is not null) return Results.BadRequest(new { error });
            var created = content.CreateAuthoredItem(definition);
            return Results.Ok(ItemView(items.Get(created.ItemId)!, data));
        });

        admin.MapPut("/items/{id:int}", (
            int id, AuthoredItemDefinition request, ContentStore content, GameData data, ItemRegistry items) =>
        {
            var definition = ItemAuthoring.Normalize(request, id);
            var source = data.Items.GetValueOrDefault(definition.SourceItemId);
            var error = ItemAuthoring.Validate(definition, source, content.AuthoredItems, id);
            if (error is not null) return Results.BadRequest(new { error });
            try
            {
                content.UpdateAuthoredItem(id, definition);
                return Results.Ok(ItemView(items.Get(id)!, data));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        admin.MapPost("/items/{id:int}/grant", (int id, ItemGrantRequest request,
            ContentStore content, ItemRegistry items, AccountStore accounts) =>
        {
            if (!content.AuthoredItems.Any(item => item.ItemId == id)
                || items.Get(id) is not { } item)
                return Results.NotFound(new { error = $"item autoral desconhecido: {id}" });
            if (request.Count is < 1 or > GameConfig.AdminItemGrantMax)
                return Results.BadRequest(new
                {
                    error = $"quantidade deve ficar entre 1 e {GameConfig.AdminItemGrantMax}"
                });

            return accounts.Mutate(state =>
            {
                if (state.Inventory.TryGetValue(id, out var stack))
                    stack.Count += request.Count;
                else
                    state.Inventory[id] = new InventoryStack
                    {
                        ItemId = id,
                        Name = item.Name,
                        Count = request.Count
                    };
                return Results.Ok(new { item.ItemId, item.Name, added = request.Count });
            });
        });

        admin.MapDelete("/items/{id:int}", (int id, ContentStore content, AccountStore accounts) =>
        {
            var referenced = accounts.Read(state =>
                state.Inventory.ContainsKey(id)
                || state.Equipment.Values.Any(loadout => loadout.Values.Contains(id)));
            if (referenced)
                return Results.BadRequest(new { error = "remova o item dos inventarios e equipamentos antes de excluir" });
            try
            {
                var removed = content.DeleteAuthoredItem(id);
                return Results.Ok(new { removed.ItemId, removed.Name });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });
    }

    private static object ItemView(ItemType item, GameData data)
    {
        var (category, subcategory) =
            ItemCategories.Of(item, data.IsFood(item.AppearanceItemId));
        return new
        {
            item.ItemId,
            item.SourceItemId,
            appearanceItemId = item.AppearanceItemId,
            item.IsAuthored,
            item.Name,
            item.Description,
            item.Slot,
            item.WeaponType,
            item.Attack,
            item.Armor,
            item.Defense,
            item.MountLookType,
            item.MountSpeed,
            item.SalePrice,
            item.Element,
            item.ElementDamage,
            item.SkillPower,
            item.CritChance,
            item.CritDamage,
            item.LifeStealChance,
            item.LifeStealAmount,
            item.CooldownReduction,
            item.MoveSpeedPercent,
            item.PhysicalResistance,
            item.FireResistance,
            item.IceResistance,
            item.EarthResistance,
            item.EnergyResistance,
            item.DeathResistance,
            item.HolyResistance,
            allowedClassIds = item.AllowedClassIds ?? ItemAuthoring.DefaultClasses(item),
            item.RequiredMasteryPoints,
            category,
            subcategory,
            capabilities = ItemCapabilities.For(item)
        };
    }

    private static string? ValidateKaeliSkin(
        KaeliSkinDefinition definition, KaeliRegistry kaelis, string? currentId = null)
    {
        var error = KaeliAuthoring.Validate(definition);
        if (error is not null) return error;
        // a skin não pode colidir com nenhuma skin estática ou autoral existente (exceto ela mesma)
        if (kaelis.SkinById.TryGetValue(definition.Id, out _)
            && !definition.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase))
            return $"id de skin ja existe: {definition.Id}";
        return null;
    }

    private static string? ValidateMonsterDefinition(
        MonsterDefinition definition,
        ContentStore content,
        GameData data,
        string? currentId = null)
    {
        var error = MonsterAuthoring.Validate(definition);
        if (error is not null) return error;
        if (data.Monsters.ContainsKey(definition.Name))
            return $"o nome '{definition.Name}' pertence a um placeholder legado; escolha um nome novo";
        if (content.Monsters.Any(m =>
                !m.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase)
                && m.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)))
            return $"nome ja existe: {definition.Name}";
        if (content.Monsters.Any(m =>
                !m.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase)
                && m.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
            return $"id ja existe: {definition.Id}";
        if (!string.IsNullOrWhiteSpace(definition.AppearanceId)
            && !data.MonsterAppearances.Any(appearance =>
                appearance.Id.Equals(definition.AppearanceId, StringComparison.OrdinalIgnoreCase)))
            return $"aparencia Canary desconhecida: {definition.AppearanceId}";
        return null;
    }

    public sealed record ActiveWaifuRequest(string WaifuId);
    public sealed record GiftRequest(string WaifuId, int ItemId);
    public sealed record SkinRequest(string WaifuId, string SkinId);
    public sealed record MasteryNodeRequest(string WaifuId, string NodeId);
    public sealed record MasteryRespecRequest(string WaifuId);
    public sealed record PullRequest(string BannerId, int Count);
    public sealed record AscendRequest(string WaifuId);
    public sealed record ClaimRequest(string ContractId);
    public sealed record SellRequest(int ItemId, int Count);
    public sealed record EquipRequest(string WaifuId, string Slot, int ItemId);
    public sealed record UnequipRequest(string WaifuId, string Slot);
    public sealed record ItemGrantRequest(int Count = 1);
}
