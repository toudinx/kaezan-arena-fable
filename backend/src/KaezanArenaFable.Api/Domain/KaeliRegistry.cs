using KaezanArenaFable.Api.Content;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Superfície única de consulta de Kaelis durante a fase de skins autorais: parte do roster
/// estático (<see cref="Waifus.All"/>) e anexa as skins criadas no Outfit Studio
/// (<see cref="ContentStore.AuthoredKaeliSkins"/>) à lista de skins de cada Kaeli. Recalcula a
/// cada acesso (poucas Kaelis), então salvar uma skin no admin reflete na próxima leitura sem
/// reinício. Espelha o papel do <see cref="MonsterRegistry"/> para monstros.
/// </summary>
public sealed class KaeliRegistry(ContentStore content)
{
    /// <summary>
    /// Roster efetivo: cada Kaeli com suas skins resolvidas. Uma skin autoral cujo id bate com uma
    /// skin estática (do código) <b>sobrescreve</b> a estática no lugar — é como o admin edita a skin
    /// padrão e as demais sem renomear ids (a invariante de ids estáveis fica intacta; remover o
    /// override restaura a definição do código). As autorais com id novo continuam anexadas ao fim,
    /// preservando a skin padrão em índice 0.
    /// </summary>
    public IReadOnlyList<WaifuDef> All
    {
        get
        {
            var authored = content.AuthoredKaeliSkins;
            if (authored.Count == 0) return Waifus.All;

            // override por id (última vence) só para ids que existem como skin estática
            var overrides = authored
                .Where(s => Waifus.SkinById.ContainsKey(s.Id))
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => KaeliAuthoring.ToSkin(g.Last()), StringComparer.OrdinalIgnoreCase);
            // skins genuinamente novas (id não-estático) anexam ao fim, agrupadas por Kaeli
            var appendByWaifu = authored
                .Where(s => !Waifus.SkinById.ContainsKey(s.Id))
                .GroupBy(s => s.WaifuId)
                .ToDictionary(g => g.Key, g => g.Select(KaeliAuthoring.ToSkin).ToList());

            return Waifus.All
                .Select(waifu =>
                {
                    var statics = waifu.Skins
                        .Select(s => overrides.TryGetValue(s.Id, out var ov) ? ov : s);
                    var extra = appendByWaifu.GetValueOrDefault(waifu.Id) ?? [];
                    return waifu with { Skins = [.. statics, .. extra] };
                })
                .ToList();
        }
    }

    public IReadOnlyDictionary<string, WaifuDef> ById => All.ToDictionary(w => w.Id);

    public IReadOnlyDictionary<string, SkinDef> SkinById =>
        All.SelectMany(w => w.Skins).ToDictionary(s => s.Id);

    /// <summary>skin id → waifu id (dona da skin).</summary>
    public IReadOnlyDictionary<string, string> SkinOwner =>
        All.SelectMany(w => w.Skins.Select(s => (s.Id, w.Id))).ToDictionary(p => p.Item1, p => p.Item2);

    public WaifuDef? Find(string waifuId) => ById.GetValueOrDefault(waifuId);
}
