using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Repara contas após mudanças de roster (refundação 2026-06-12: Tessa, Nyx, Lyra e Rosa
/// saíram do jogo). Kaelis desconhecidas são removidas com refund em Kaeros; equipamento
/// delas volta para a Mochila; referências órfãs (skins, maestria, afinidade) são limpas.
/// Roda uma vez no boot, fora do tick.
/// </summary>
public static class AccountSanitizer
{
    public static bool Sanitize(AccountState state, ItemRegistry items, KaeliRegistry kaelis)
    {
        var changed = false;

        // Migração sets por tier: loadout único legado (chave = waifuId) vira o set do tier 1
        // (chave = "waifuId#1"). Idempotente — chaves já com "#" são ignoradas.
        var legacyKeys = state.Equipment.Keys.Where(k => !k.Contains('#')).ToList();
        foreach (var key in legacyKeys)
        {
            var migrated = AccountState.EquipKey(key, 1);
            if (!state.Equipment.ContainsKey(migrated))
                state.Equipment[migrated] = state.Equipment[key];
            state.Equipment.Remove(key);
            changed = true;
        }

        // Kaelis removidas do roster: refund + devolução de equipamento (de todos os tiers)
        var unknown = state.OwnedWaifus.Where(id => !Waifus.ById.ContainsKey(id)).ToList();
        foreach (var waifuId in unknown)
        {
            state.OwnedWaifus.Remove(waifuId);
            state.Kaeros += GameConfig.CutKaeliRefundKaeros;
            state.Shards.Remove(waifuId);
            state.Ascension.Remove(waifuId);
            state.AffinityXp.Remove(waifuId);
            state.GiftsToday.Remove(waifuId);
            state.SelectedSkins.Remove(waifuId);
            state.Mastery.Remove(waifuId);

            var loadoutKeys = state.Equipment.Keys
                .Where(k => AccountState.ParseEquipKey(k).WaifuId == waifuId).ToList();
            foreach (var key in loadoutKeys)
            {
                if (!state.Equipment.Remove(key, out var loadout)) continue;
                foreach (var itemId in loadout.Values)
                {
                    if (items.Get(itemId) is not { } item) continue;
                    if (state.Inventory.TryGetValue(itemId, out var stack)) stack.Count++;
                    else state.Inventory[itemId] = new InventoryStack
                    {
                        ItemId = item.ItemId, Name = item.Name, Count = 1
                    };
                }
            }
            changed = true;
        }

        if (state.OwnedWaifus.Count == 0)
        {
            state.OwnedWaifus.Add(Waifus.StarterWaifuId);
            changed = true;
        }

        if (!state.OwnedWaifus.Contains(state.ActiveWaifuId))
        {
            state.ActiveWaifuId = state.OwnedWaifus.Contains(Waifus.StarterWaifuId)
                ? Waifus.StarterWaifuId
                : state.OwnedWaifus[0];
            changed = true;
        }

        // skins órfãs ou que não pertencem mais à Kaeli selecionada (inclui skins autorais)
        var skinById = kaelis.SkinById;
        var skinOwner = kaelis.SkinOwner;
        var badSkins = state.OwnedSkins.Where(id => !skinById.ContainsKey(id)).ToList();
        foreach (var skinId in badSkins)
        {
            state.OwnedSkins.Remove(skinId);
            changed = true;
        }
        var badSelections = state.SelectedSkins
            .Where(kv => !skinById.ContainsKey(kv.Value)
                         || skinOwner.GetValueOrDefault(kv.Value) != kv.Key)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var waifuId in badSelections)
        {
            state.SelectedSkins.Remove(waifuId);
            changed = true;
        }

        // nodes de maestria que não existem mais: devolve os pontos
        foreach (var (waifuId, mastery) in state.Mastery)
        {
            var invalid = mastery.Nodes.Where(id => !Mastery.NodeById.ContainsKey(id)).ToList();
            if (invalid.Count == 0) continue;
            foreach (var nodeId in invalid) mastery.Nodes.Remove(nodeId);
            var validCost = mastery.Nodes.Sum(id => Mastery.NodeById[id].Cost);
            mastery.Points += Math.Max(mastery.Spent - validCost, 0);
            mastery.Spent = validCost;
            changed = true;
            _ = waifuId;
        }

        return changed;
    }
}
