using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Profundidade de Kaeli: presentes (afinidade), skins e maestria.
/// Tudo meta — nunca roda dentro do tick.
/// </summary>
public sealed class KaeliService(AccountStore store, GameData data)
{
    // ---- afinidade ----

    /// <summary>Nível de afinidade (1..max) derivado do XP acumulado.</summary>
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

    /// <summary>XP já dentro do nível atual e quanto falta para o próximo (0 quando máximo).</summary>
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
    /// Concede XP de afinidade e aplica marcos de nível (Kaeros, lore). Anota em `notes`.
    /// Retorna o nível resultante. Usado por presentes e pelo fim de run (RewardService).
    /// </summary>
    public static int GrantAffinityXp(AccountState state, WaifuDef waifu, long xp, List<string> notes)
    {
        var before = AffinityLevelFor(state.AffinityXp.GetValueOrDefault(waifu.Id));
        var total = state.AffinityXp.GetValueOrDefault(waifu.Id) + xp;
        state.AffinityXp[waifu.Id] = total;
        var after = AffinityLevelFor(total);

        for (var level = before + 1; level <= after; level++)
        {
            notes.Add($"Afinidade de {waifu.Name} subiu para {level}!");
            if (GameConfig.AffinityKaerosRewards.TryGetValue(level, out var kaeros))
            {
                state.Kaeros += kaeros;
                notes.Add($"Marco de afinidade {level}: +{kaeros} Kaeros");
            }
            var loreIndex = Array.IndexOf(GameConfig.AffinityLoreLevels, level);
            if (loreIndex >= 0 && loreIndex < waifu.Lore.Count)
                notes.Add($"Novo eco de memória desbloqueado: \"{waifu.Name} — Eco {loreIndex + 1}\"");
            foreach (var skin in waifu.Skins)
                if (skin.Unlock == "affinity" && skin.UnlockValue == level)
                    notes.Add($"Skin desbloqueada: {skin.Name}");
        }
        return after;
    }

    // ---- presentes ----

    public object Gift(string waifuId, int itemId)
    {
        var waifu = Waifus.ById.GetValueOrDefault(waifuId)
                    ?? throw new ArgumentException("Kaeli desconhecida");
        if (!data.Items.TryGetValue(itemId, out var item))
            throw new ArgumentException("item desconhecido");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli não recrutada");

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (state.GiftsDate != today)
            {
                state.GiftsDate = today;
                state.GiftsToday.Clear();
            }
            var given = state.GiftsToday.GetValueOrDefault(waifuId);
            if (given >= GameConfig.GiftsPerKaeliPerDay)
                throw new InvalidOperationException(
                    $"{waifu.Name} já recebeu {GameConfig.GiftsPerKaeliPerDay} presentes hoje");

            if (!state.Inventory.TryGetValue(itemId, out var stack) || stack.Count <= 0)
                throw new InvalidOperationException("item não está na Mochila");
            stack.Count--;
            if (stack.Count == 0) state.Inventory.Remove(itemId);

            var favorite = waifu.FavoriteGiftItemIds.Contains(itemId);
            var value = data.ItemValue(itemId);
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

    /// <summary>Uma skin está disponível? (default sempre; affinity por nível; gold/kaeros se comprada).</summary>
    public static bool SkinUnlocked(AccountState state, WaifuDef waifu, SkinDef skin) => skin.Unlock switch
    {
        "default" => true,
        "affinity" => AffinityLevelFor(state.AffinityXp.GetValueOrDefault(waifu.Id)) >= skin.UnlockValue,
        _ => state.OwnedSkins.Contains(skin.Id)
    };

    public object SelectSkin(string waifuId, string skinId)
    {
        var waifu = Waifus.ById.GetValueOrDefault(waifuId)
                    ?? throw new ArgumentException("Kaeli desconhecida");
        var skin = waifu.Skins.FirstOrDefault(s => s.Id == skinId)
                   ?? throw new ArgumentException("skin não pertence a esta Kaeli");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli não recrutada");
            if (!SkinUnlocked(state, waifu, skin))
                throw new InvalidOperationException("skin ainda não desbloqueada");

            if (skin.Id == waifu.DefaultSkin.Id) state.SelectedSkins.Remove(waifuId);
            else state.SelectedSkins[waifuId] = skin.Id;
            return new { waifuId, skinId = skin.Id };
        });
    }

    public object BuySkin(string waifuId, string skinId)
    {
        var waifu = Waifus.ById.GetValueOrDefault(waifuId)
                    ?? throw new ArgumentException("Kaeli desconhecida");
        var skin = waifu.Skins.FirstOrDefault(s => s.Id == skinId)
                   ?? throw new ArgumentException("skin não pertence a esta Kaeli");
        if (skin.Unlock is not ("gold" or "kaeros"))
            throw new ArgumentException("esta skin não está à venda");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli não recrutada");
            if (state.OwnedSkins.Contains(skin.Id))
                throw new InvalidOperationException("skin já comprada");

            if (skin.Unlock == "gold")
            {
                if (state.Gold < skin.UnlockValue) throw new InvalidOperationException("ouro insuficiente");
                state.Gold -= skin.UnlockValue;
            }
            else
            {
                if (state.Kaeros < skin.UnlockValue) throw new InvalidOperationException("Kaeros insuficiente");
                state.Kaeros -= skin.UnlockValue;
            }

            state.OwnedSkins.Add(skin.Id);
            state.SelectedSkins[waifuId] = skin.Id;
            return new { waifuId, skinId = skin.Id, state.Gold, state.Kaeros };
        });
    }

    // ---- maestria ----

    public object UnlockMasteryNode(string waifuId, string nodeId)
    {
        if (!Waifus.ById.ContainsKey(waifuId)) throw new ArgumentException("Kaeli desconhecida");
        var tree = Mastery.TreeByWaifu[waifuId];
        var node = tree.FirstOrDefault(n => n.Id == nodeId)
                   ?? throw new ArgumentException("node não pertence a esta Kaeli");

        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId))
                throw new InvalidOperationException("Kaeli não recrutada");
            if (!state.Mastery.TryGetValue(waifuId, out var mastery))
                state.Mastery[waifuId] = mastery = new MasteryState();

            if (mastery.Nodes.Contains(nodeId))
                throw new InvalidOperationException("node já destravado");
            if (node.Order > 1)
            {
                var previous = tree.First(n => n.Branch == node.Branch && n.Order == node.Order - 1);
                if (!mastery.Nodes.Contains(previous.Id))
                    throw new InvalidOperationException($"requer \"{previous.Name}\" antes");
            }
            if (mastery.Points < node.Cost)
                throw new InvalidOperationException($"pontos insuficientes ({mastery.Points}/{node.Cost})");

            mastery.Points -= node.Cost;
            mastery.Spent += node.Cost;
            mastery.Nodes.Add(nodeId);
            return new { waifuId, nodeId, mastery.Points, mastery.Spent, nodes = mastery.Nodes };
        });
    }

    public object RespecMastery(string waifuId)
    {
        if (!Waifus.ById.ContainsKey(waifuId)) throw new ArgumentException("Kaeli desconhecida");
        return store.Mutate(state =>
        {
            if (!state.Mastery.TryGetValue(waifuId, out var mastery) || mastery.Spent == 0)
                throw new InvalidOperationException("nada para resetar");
            if (state.Gold < GameConfig.MasteryRespecGold)
                throw new InvalidOperationException($"requer {GameConfig.MasteryRespecGold} de ouro");

            state.Gold -= GameConfig.MasteryRespecGold;
            mastery.Points += mastery.Spent;
            mastery.Spent = 0;
            mastery.Nodes.Clear();
            return new { waifuId, mastery.Points, state.Gold };
        });
    }
}
