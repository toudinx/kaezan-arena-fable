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
    /// <summary>Roster efetivo: cada Kaeli com suas skins estáticas + autorais (anexadas no fim,
    /// preservando a skin padrão em índice 0).</summary>
    public IReadOnlyList<WaifuDef> All
    {
        get
        {
            var authoredByWaifu = content.AuthoredKaeliSkins
                .GroupBy(s => s.WaifuId)
                .ToDictionary(g => g.Key, g => g.Select(KaeliAuthoring.ToSkin).ToList());

            if (authoredByWaifu.Count == 0) return Waifus.All;

            return Waifus.All
                .Select(waifu => authoredByWaifu.TryGetValue(waifu.Id, out var extra)
                    ? waifu with { Skins = [.. waifu.Skins, .. extra] }
                    : waifu)
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
