using KaezanArenaFable.Api.Content;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Catalogo efetivo: itens imutaveis do Canary mais itens autorais persistidos pelo admin.
/// </summary>
public sealed class ItemRegistry(GameData data, ContentStore content)
{
    public IReadOnlyDictionary<int, ItemType> Base => data.Items;

    public IReadOnlyDictionary<int, ItemType> All
    {
        get
        {
            var merged = new Dictionary<int, ItemType>(data.Items);
            foreach (var definition in content.AuthoredItems)
            {
                if (data.Items.TryGetValue(definition.SourceItemId, out var source))
                    merged[definition.ItemId] = definition.Apply(source);
            }
            return merged;
        }
    }

    public ItemType? Get(int itemId)
    {
        if (data.Items.TryGetValue(itemId, out var source)) return source;
        var definition = content.AuthoredItems.FirstOrDefault(item => item.ItemId == itemId);
        return definition is not null && data.Items.TryGetValue(definition.SourceItemId, out source)
            ? definition.Apply(source)
            : null;
    }

    public int Value(int itemId)
    {
        var item = Get(itemId);
        return item is { SalePrice: > 0 } ? item.SalePrice : GameConfig.ItemFallbackSalePrice;
    }
}
