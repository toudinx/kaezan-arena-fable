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
            ItemRegistry itemRegistry, GachaService gacha) => Results.Ok(new
        {
            waifus = kaelis.All,
            classes = Classes.All,
            skills = Classes.Skills.Values,
            cards = Cards.All,
            tiers = content.Tiers,
            banners = gacha.GetBanners(),
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
            farm = new
            {
                autoRepeatDelayMs = GameConfig.AutoRepeatDelayMs,
                minRuns = GameConfig.FarmRunMinCount,
                maxRuns = GameConfig.FarmRunMaxCount,
                energyPerRun = GameConfig.DungeonEnergyPerRun,
                energyCap = GameConfig.DungeonEnergyCap
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
                i.RequiredMasteryPoints,
                i.Tier,
                i.Tag,
                i.StatMultiplier
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
        api.MapGet("/account", (AccountStore store, DailyService dailies, RewardService rewards) =>
        {
            var offlineReward = rewards.ClaimOfflineProgression();
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
                dailies = contracts,
                offlineReward
            }));
        });

        api.MapPost("/account/active-waifu", (ActiveWaifuRequest req, AccountStore store) =>
        {
            if (!Waifus.ById.ContainsKey(req.WaifuId)) return Results.BadRequest(new { error = "unknown waifu" });
            return store.Mutate(s =>
            {
                if (!s.OwnedWaifus.Contains(req.WaifuId)) return Results.BadRequest(new { error = "not owned" });
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
                    return Results.BadRequest(new { error = "invalid quantity" });
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
                    return Results.BadRequest(new { error = "Kaeli not owned" });
                if (!EquipmentSlots.IsValid(req.Slot))
                    return Results.BadRequest(new { error = "invalid slot" });
                if (req.Tier is < 1 or > 5)
                    return Results.BadRequest(new { error = "invalid tier" });
                if (items.Get(req.ItemId) is not { } item || item.Slot != req.Slot)
                    return Results.BadRequest(new { error = "item incompatible with slot" });
                if (item.Tier != 0 && item.Tier != req.Tier)
                    return Results.BadRequest(new { error = $"{item.Name} belongs to tier {item.Tier}, not tier {req.Tier}" });
                if (kaelis.Find(req.WaifuId) is not { } waifu)
                    return Results.BadRequest(new { error = "unknown Kaeli" });
                if (item.AllowedClassIds is { Count: > 0 }
                    && !item.AllowedClassIds.Contains(waifu.ClassId, StringComparer.OrdinalIgnoreCase))
                    return Results.BadRequest(new { error = $"{item.Name} cannot be used by class {waifu.ClassId}" });
                if (!s.Inventory.TryGetValue(req.ItemId, out var stack) || stack.Count <= 0)
                    return Results.BadRequest(new { error = "item not in Backpack" });

                var equipKey = AccountState.EquipKey(req.WaifuId, req.Tier);
                if (!s.Equipment.TryGetValue(equipKey, out var loadout))
                    s.Equipment[equipKey] = loadout = [];

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
                return Results.Ok(new { req.WaifuId, req.Slot, req.ItemId, req.Tier });
            }));

        api.MapPost("/equipment/unequip", (UnequipRequest req, AccountStore store, ItemRegistry items) =>
            store.Mutate(s =>
            {
                if (!EquipmentSlots.IsValid(req.Slot))
                    return Results.BadRequest(new { error = "invalid slot" });
                if (req.Tier is < 1 or > 5)
                    return Results.BadRequest(new { error = "invalid tier" });
                var equipKey = AccountState.EquipKey(req.WaifuId, req.Tier);
                if (!s.Equipment.TryGetValue(equipKey, out var loadout)
                    || !loadout.Remove(req.Slot, out var itemId)
                    || items.Get(itemId) is not { } item)
                    return Results.BadRequest(new { error = "empty slot" });

                if (s.Inventory.TryGetValue(itemId, out var stack))
                    stack.Count++;
                else
                    s.Inventory[itemId] = new InventoryStack
                    {
                        ItemId = item.ItemId,
                        Name = item.Name,
                        Count = 1
                    };

                if (loadout.Count == 0) s.Equipment.Remove(equipKey);
                return Results.Ok(new { req.WaifuId, req.Slot, itemId, req.Tier });
            }));

        // ---- admin: content authoring (in-game editor) ----
        var admin = api.MapGroup("/admin");

        // editor support data: list of available mobs and bosses (from monsters.json)
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
            // G-08B: keyword interaction — card tags and resistance range per keyword.
            keywordTags = GameConfig.MonsterKeywordTags,
            keywordResistMin = GameConfig.KeywordResistMin,
            keywordResistMax = GameConfig.KeywordResistMax,
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
            // validate content (mobs/boss exist) before persisting
            if (tiers is null || tiers.Count == 0)
                return Results.BadRequest(new { error = "provide at least one tier" });
            if (tiers.Select(t => t.Tier).Distinct().Count() != tiers.Count)
                return Results.BadRequest(new { error = "duplicate tier numbers" });

            foreach (var t in tiers)
            {
                if (string.IsNullOrWhiteSpace(t.Name))
                    return Results.BadRequest(new { error = $"tier {t.Tier}: empty name" });
                if (t.CommonMobs.Length == 0)
                    return Results.BadRequest(new { error = $"tier {t.Tier}: requires at least 1 common mob" });

                foreach (var mob in t.CommonMobs.Concat(t.EliteMobs))
                    if (!registry.Contains(mob))
                        return Results.BadRequest(new { error = $"tier {t.Tier}: unknown mob '{mob}'" });

                // boss can be any monster — the engine scales HP of whoever is chosen
                if (!registry.Contains(t.Boss))
                    return Results.BadRequest(new { error = $"tier {t.Tier}: unknown boss '{t.Boss}'" });
            }

            return Results.Ok(content.ReplaceTiers(tiers));
        });

        // ---- admin: tuning por papel (MG-05) ----
        admin.MapGet("/content/role-tuning", (ContentStore content) => Results.Ok(content.RoleTuningTable));

        admin.MapPut("/content/role-tuning", (List<RoleTuningRow> rows, ContentStore content) =>
        {
            if (rows is null || rows.Count == 0)
                return Results.BadRequest(new { error = "provide the roles table" });

            var known = Enum.GetNames<KaeliRole>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Role) || !known.Contains(row.Role.Trim()))
                    return Results.BadRequest(new { error = $"unknown role: '{row.Role}'" });
                if (!seen.Add(row.Role.Trim()))
                    return Results.BadRequest(new { error = $"duplicate role: '{row.Role}'" });
                if (row.AutoDmgMult <= 0 || row.SkillDmgMult <= 0)
                    return Results.BadRequest(new { error = $"{row.Role}: damage multipliers must be > 0" });
                if (row.BaseAutoAttackMs < 400)
                    return Results.BadRequest(new { error = $"{row.Role}: auto speed must be >= 400ms (engine floor)" });
                if (row.AutoRange < 1)
                    return Results.BadRequest(new { error = $"{row.Role}: range must be >= 1" });
                if (row.AoeScale <= 0)
                    return Results.BadRequest(new { error = $"{row.Role}: AoE scale must be > 0" });
            }
            if (!known.All(seen.Contains))
                return Results.BadRequest(new { error = "provide all 3 roles (Mage, Archer, Knight)" });

            try { return Results.Ok(content.ReplaceRoleTunings(rows)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ---- admin: Outfit Studio (authored Kaeli skins) ----

        // support metadata: roster (for assigning skins), unlock rules and palette size
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
                defaultSkin = new { w.DefaultSkin.LookType, w.DefaultSkin.Head, w.DefaultSkin.Body, w.DefaultSkin.Legs, w.DefaultSkin.Feet },
                // code skin ids (so the wardrobe can distinguish static/override) + the default
                staticSkinIds = w.Skins.Select(s => s.Id).ToArray(),
                defaultSkinId = w.DefaultSkin.Id
            }),
            unlockKinds = KaeliAuthoring.UnlockKinds,
            outfitColorCount = KaeliAuthoring.OutfitColorCount,
            affinityMaxLevel = GameConfig.AffinityMaxLevel
        }));

        admin.MapGet("/content/kaeli-skins", (ContentStore content) => Results.Ok(content.AuthoredKaeliSkins));

        admin.MapPost("/content/kaeli-skins", (KaeliSkinDefinition request, ContentStore content) =>
        {
            var definition = KaeliAuthoring.Normalize(request);
            var error = ValidateKaeliSkin(definition, content);
            if (error is not null) return Results.BadRequest(new { error });
            try { return Results.Ok(content.CreateKaeliSkin(definition)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        admin.MapPut("/content/kaeli-skins/{id}", (
            string id, KaeliSkinDefinition request, ContentStore content) =>
        {
            var definition = KaeliAuthoring.Normalize(request, id);
            var error = ValidateKaeliSkin(definition, content, id);
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

        // reorders a Kaeli's authored skins (wardrobe) — controls the order in the skin selector
        admin.MapPost("/content/kaeli-skins/reorder", (ReorderSkinsRequest request, ContentStore content) =>
        {
            if (string.IsNullOrWhiteSpace(request.WaifuId) || request.OrderedIds is null)
                return Results.BadRequest(new { error = "waifuId and orderedIds are required" });
            return Results.Ok(content.ReorderKaeliSkins(request.WaifuId, request.OrderedIds));
        });

        // ---- admin: Item Studio (Canary library + Kaezan item CRUD) ----

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
                elements = ItemAuthoring.Elements,
                balance = new
                {
                    tiers = GameConfig.AuthoredItemSetTiers,
                    grades = GameConfig.AuthoredItemBalanceGrades,
                    ranges = GameConfig.AuthoredItemBalanceRanges,
                    tags = new[]
                    {
                        new { id = GameConfig.AuthoredItemTagNormal, name = "Normal" },
                        new { id = GameConfig.AuthoredItemTagRelic, name = "Relic" }
                    },
                    relicMultiplierDefault = GameConfig.AuthoredItemRelicMultiplierDefault,
                    relicMultiplierMin = GameConfig.AuthoredItemRelicMultiplierMin,
                    relicMultiplierMax = GameConfig.AuthoredItemRelicMultiplierMax
                }
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
                return Results.NotFound(new { error = $"unknown authored item: {id}" });
            if (request.Count is < 1 or > GameConfig.AdminItemGrantMax)
                return Results.BadRequest(new
                {
                    error = $"count must be between 1 and {GameConfig.AdminItemGrantMax}"
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

        admin.MapPost("/grant-kaeros", (GrantKaerosRequest req, AccountStore accounts) =>
            accounts.Mutate(s =>
            {
                s.Kaeros += req.Amount;
                return Results.Ok(new { added = req.Amount, s.Kaeros });
            }));

        // ---- admin: banners ativos ----

        admin.MapGet("/banners", (ContentStore content) => Results.Ok(new
        {
            activeWaifuIds = content.ActiveBannerWaifuIds,
            allWaifuIds = Waifus.All.Select(w => w.Id).ToArray()
        }));

        admin.MapPut("/banners", (BannersRequest req, ContentStore content, GachaService gacha) =>
        {
            if (req.WaifuIds is null || req.WaifuIds.Count == 0)
                return Results.BadRequest(new { error = "provide at least one waifu" });
            if (req.WaifuIds.Count > 3)
                return Results.BadRequest(new { error = "maximum of 3 character banners" });

            var unknown = req.WaifuIds.Where(id => !Waifus.ById.ContainsKey(id)).ToArray();
            if (unknown.Length > 0)
                return Results.BadRequest(new { error = $"unknown waifus: {string.Join(", ", unknown)}" });

            var saved = content.SetActiveBanners(req.WaifuIds);
            return Results.Ok(new { activeWaifuIds = saved, banners = gacha.GetBanners() });
        });

        admin.MapDelete("/items/{id:int}", (int id, ContentStore content, AccountStore accounts) =>
        {
            var referenced = accounts.Read(state =>
                state.Inventory.ContainsKey(id)
                || state.Equipment.Values.Any(loadout => loadout.Values.Contains(id)));
            if (referenced)
                return Results.BadRequest(new { error = "remove the item from inventories and equipment before deleting" });
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
            allowedClassIds = item.AllowedClassIds ?? [],
            item.RequiredMasteryPoints,
            item.Tier,
            item.Tag,
            item.StatMultiplier,
            category,
            subcategory,
            capabilities = ItemCapabilities.For(item)
        };
    }

    private static string? ValidateKaeliSkin(
        KaeliSkinDefinition definition, ContentStore content, string? currentId = null)
    {
        var error = KaeliAuthoring.Validate(definition);
        if (error is not null) return error;

        // static skin override: allowed (edits the code skin), but only for the original owner
        // — the id encodes the owner, so re-linking a static id to another Kaeli is rejected.
        if (Waifus.SkinById.ContainsKey(definition.Id))
        {
            var owner = Waifus.SkinOwner[definition.Id];
            if (!definition.WaifuId.Equals(owner, StringComparison.OrdinalIgnoreCase))
                return $"skin {definition.Id} belongs to {owner} and cannot be re-linked";
            // the default skin must remain free (it is the always-available base visual)
            if (definition.Id.Equals(Waifus.ById[owner].DefaultSkin.Id, StringComparison.OrdinalIgnoreCase)
                && definition.Unlock != "default")
                return "the default skin must keep the Default unlock";
            return null;
        }

        // new id skin: must not collide with another authored skin (except itself when editing)
        if (content.AuthoredKaeliSkins.Any(s => s.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase))
            && !definition.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase))
            return $"skin id already exists: {definition.Id}";
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
            return $"the name '{definition.Name}' belongs to a legacy placeholder; choose a new name";
        if (content.Monsters.Any(m =>
                !m.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase)
                && m.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)))
            return $"name already exists: {definition.Name}";
        if (content.Monsters.Any(m =>
                !m.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase)
                && m.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
            return $"id already exists: {definition.Id}";
        if (!string.IsNullOrWhiteSpace(definition.AppearanceId)
            && !data.MonsterAppearances.Any(appearance =>
                appearance.Id.Equals(definition.AppearanceId, StringComparison.OrdinalIgnoreCase)))
            return $"unknown Canary appearance: {definition.AppearanceId}";
        return null;
    }

    public sealed record ActiveWaifuRequest(string WaifuId);
    public sealed record GiftRequest(string WaifuId, int ItemId);
    public sealed record SkinRequest(string WaifuId, string SkinId);
    public sealed record ReorderSkinsRequest(string WaifuId, List<string> OrderedIds);
    public sealed record MasteryNodeRequest(string WaifuId, string NodeId);
    public sealed record MasteryRespecRequest(string WaifuId);
    public sealed record PullRequest(string BannerId, int Count);
    public sealed record AscendRequest(string WaifuId);
    public sealed record ClaimRequest(string ContractId);
    public sealed record SellRequest(int ItemId, int Count);
    public sealed record EquipRequest(string WaifuId, string Slot, int ItemId, int Tier = 1);
    public sealed record UnequipRequest(string WaifuId, string Slot, int Tier = 1);
    public sealed record ItemGrantRequest(int Count = 1);
    public sealed record GrantKaerosRequest(int Amount = 1600);
    public sealed record BannersRequest(List<string> WaifuIds);
}
