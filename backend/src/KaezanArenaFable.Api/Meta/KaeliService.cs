using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Kaeli depth: gifts (affinity), skins and mastery.
/// All meta — never runs inside the tick.
/// </summary>
public sealed class KaeliService(AccountStore store, KaeliRegistry kaelis, ItemRegistry items)
{
    // ---- affinity ----

    /// <summary>Affinity level (1..max) derived from accumulated XP.</summary>
    public static int AffinityLevelFor(long xp)
    {
        var level = 1;
        while (level < GameConfig.AffinityMaxLevel)
        {
            var need = GameConfig.XpForAffinityLevel(level);
            if (xp < need) break;
            xp -= need;
            level++;
        }
        return level;
    }

    /// <summary>XP already within the current level and how much is needed for the next (0 at maximum).</summary>
    public static (long Into, long ToNext) AffinityProgress(long xp)
    {
        var level = 1;
        while (level < GameConfig.AffinityMaxLevel)
        {
            var need = GameConfig.XpForAffinityLevel(level);
            if (xp < need) return (xp, need);
            xp -= need;
            level++;
        }
        return (0, 0);
    }

    /// <summary>
    /// Grants affinity XP and applies level milestones (Kaeros, lore). Appends to `notes`.
    /// Returns the resulting level. Used by gifts and run-end (RewardService).
    /// </summary>
    public static int GrantAffinityXp(AccountState state, WaifuDef waifu, long xp, List<string> notes)
    {
        var before = AffinityLevelFor(state.AffinityXp.GetValueOrDefault(waifu.Id));
        var total = state.AffinityXp.GetValueOrDefault(waifu.Id) + xp;
        state.AffinityXp[waifu.Id] = total;
        var after = AffinityLevelFor(total);

        for (var level = before + 1; level <= after; level++)
        {
            notes.Add($"{waifu.Name}'s Affinity rose to {level}!");
            if (GameConfig.AffinityKaerosRewards.TryGetValue(level, out var kaeros))
            {
                state.Kaeros += kaeros;
                notes.Add($"Affinity milestone {level}: +{kaeros} Kaeros");
            }
            var loreIndex = Array.IndexOf(GameConfig.AffinityLoreLevels, level);
            if (loreIndex >= 0 && loreIndex < waifu.Lore.Count)
                notes.Add($"New memory echo unlocked: \"{waifu.Name} — Echo {loreIndex + 1}\"");
            foreach (var skin in waifu.Skins)
                if (skin.Unlock == "affinity" && skin.UnlockValue == level)
                    notes.Add($"Skin unlocked: {skin.Name}");
        }
        return after;
    }

    // ---- gifts ----

    public object Gift(string waifuId, int itemId)
    {
        var waifu = kaelis.Find(waifuId)
                    ?? throw new ArgumentException("unknown Kaeli");
        if (items.Get(itemId) is not { } item)
            throw new ArgumentException("unknown item");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli not recruited");

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (state.GiftsDate != today)
            {
                state.GiftsDate = today;
                state.GiftsToday.Clear();
            }
            var given = state.GiftsToday.GetValueOrDefault(waifuId);
            if (given >= GameConfig.GiftsPerKaeliPerDay)
                throw new InvalidOperationException(
                    $"{waifu.Name} has already received {GameConfig.GiftsPerKaeliPerDay} gifts today");

            if (!state.Inventory.TryGetValue(itemId, out var stack) || stack.Count <= 0)
                throw new InvalidOperationException("item not in Backpack");
            stack.Count--;
            if (stack.Count == 0) state.Inventory.Remove(itemId);

            var favorite = waifu.FavoriteGiftItemIds.Contains(itemId);
            var value = items.Value(itemId);
            var xp = (long)Math.Min(
                (GameConfig.GiftBaseXp + value * GameConfig.GiftXpPerGold)
                * (favorite ? GameConfig.GiftFavoriteMultiplier : 1.0),
                GameConfig.GiftXpCap);

            var notes = new List<string>();
            var level = GrantAffinityXp(state, waifu, xp, notes);
            state.GiftsToday[waifuId] = given + 1;

            var (into, toNext) = AffinityProgress(state.AffinityXp[waifuId]);
            return new
            {
                waifuId,
                xpGained = xp,
                favorite,
                level,
                xpIntoLevel = into,
                xpToNext = toNext,
                giftsLeftToday = GameConfig.GiftsPerKaeliPerDay - given - 1,
                notes
            };
        });
    }

    // ---- skins ----

    /// <summary>Is a skin available? (default: always; affinity: by level; gold/kaeros: if purchased).</summary>
    public static bool SkinUnlocked(AccountState state, WaifuDef waifu, SkinDef skin) => skin.Unlock switch
    {
        "default" => true,
        "affinity" => AffinityLevelFor(state.AffinityXp.GetValueOrDefault(waifu.Id)) >= skin.UnlockValue,
        _ => state.OwnedSkins.Contains(skin.Id)
    };

    public object SelectSkin(string waifuId, string skinId)
    {
        var waifu = kaelis.Find(waifuId)
                    ?? throw new ArgumentException("unknown Kaeli");
        var skin = waifu.Skins.FirstOrDefault(s => s.Id == skinId)
                   ?? throw new ArgumentException("skin does not belong to this Kaeli");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli not recruited");
            if (!SkinUnlocked(state, waifu, skin))
                throw new InvalidOperationException("skin not yet unlocked");

            if (skin.Id == waifu.DefaultSkin.Id) state.SelectedSkins.Remove(waifuId);
            else state.SelectedSkins[waifuId] = skin.Id;
            return new { waifuId, skinId = skin.Id };
        });
    }

    public object BuySkin(string waifuId, string skinId)
    {
        var waifu = kaelis.Find(waifuId)
                    ?? throw new ArgumentException("unknown Kaeli");
        var skin = waifu.Skins.FirstOrDefault(s => s.Id == skinId)
                   ?? throw new ArgumentException("skin does not belong to this Kaeli");
        if (skin.Unlock is not ("gold" or "kaeros"))
            throw new ArgumentException("this skin is not for sale");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli not recruited");
            if (state.OwnedSkins.Contains(skin.Id))
                throw new InvalidOperationException("skin already purchased");

            if (skin.Unlock == "gold")
            {
                if (state.Gold < skin.UnlockValue) throw new InvalidOperationException("insufficient gold");
                state.Gold -= skin.UnlockValue;
            }
            else
            {
                if (state.Kaeros < skin.UnlockValue) throw new InvalidOperationException("insufficient Kaeros");
                state.Kaeros -= skin.UnlockValue;
            }

            state.OwnedSkins.Add(skin.Id);
            state.SelectedSkins[waifuId] = skin.Id;
            return new { waifuId, skinId = skin.Id, state.Gold, state.Kaeros };
        });
    }

    // ---- mastery ----

    public object UnlockMasteryNode(string waifuId, string nodeId)
    {
        if (!Waifus.ById.ContainsKey(waifuId)) throw new ArgumentException("unknown Kaeli");
        var tree = Mastery.TreeByWaifu[waifuId];
        var node = tree.FirstOrDefault(n => n.Id == nodeId)
                   ?? throw new ArgumentException("node does not belong to this Kaeli");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli not recruited");
            if (!state.Mastery.TryGetValue(waifuId, out var mastery))
                state.Mastery[waifuId] = mastery = new MasteryState();

            if (mastery.Nodes.Contains(nodeId))
                throw new InvalidOperationException("node already unlocked");
            if (node.Order > 1)
            {
                var previous = tree.First(n => n.Branch == node.Branch && n.Order == node.Order - 1);
                if (!mastery.Nodes.Contains(previous.Id))
                    throw new InvalidOperationException($"requires \"{previous.Name}\" first");
            }
            if (mastery.Points < node.Cost)
                throw new InvalidOperationException($"insufficient points ({mastery.Points}/{node.Cost})");

            mastery.Points -= node.Cost;
            mastery.Spent += node.Cost;
            mastery.Nodes.Add(nodeId);
            return new { waifuId, nodeId, mastery.Points, mastery.Spent, nodes = mastery.Nodes };
        });
    }

    public object RespecMastery(string waifuId)
    {
        if (!Waifus.ById.ContainsKey(waifuId)) throw new ArgumentException("unknown Kaeli");
        return store.Mutate(state =>
        {
            if (!state.Mastery.TryGetValue(waifuId, out var mastery) || mastery.Spent == 0)
                throw new InvalidOperationException("nothing to reset");
            if (state.Gold < GameConfig.MasteryRespecGold)
                throw new InvalidOperationException($"requires {GameConfig.MasteryRespecGold} gold");

            state.Gold -= GameConfig.MasteryRespecGold;
            mastery.Points += mastery.Spent;
            mastery.Spent = 0;
            mastery.Nodes.Clear();
            return new { waifuId, mastery.Points, state.Gold };
        });
    }
}
