using KaezanArenaFable.Api.Content;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Single lookup surface for Kaelis during the authored-skin phase: starts from the static roster
/// (<see cref="Waifus.All"/>) and appends skins created in Outfit Studio
/// (<see cref="ContentStore.AuthoredKaeliSkins"/>) to each Kaeli's skin list. Recomputed on each
/// access (few Kaelis), so saving an admin skin is reflected on the next read without restart.
/// Mirrors <see cref="MonsterRegistry"/> for monsters.
/// </summary>
public sealed class KaeliRegistry(ContentStore content)
{
    /// <summary>
    /// Effective roster: each Kaeli with resolved skins. An authored skin whose id matches a
    /// static code skin <b>overrides</b> that static skin in place; this is how admin edits the
    /// default skin and the other built-ins without renaming ids. Removing the override restores
    /// the code definition. Authored skins with new ids are appended at the end, preserving the
    /// default skin at index 0.
    /// </summary>
    public IReadOnlyList<WaifuDef> All
    {
        get
        {
            var authored = content.AuthoredKaeliSkins;
            if (authored.Count == 0) return Waifus.All;

            // override by id (last wins), only for ids that already exist as static skins
            var overrides = authored
                .Where(s => Waifus.SkinById.ContainsKey(s.Id))
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => KaeliAuthoring.ToSkin(g.Last()), StringComparer.OrdinalIgnoreCase);
            // genuinely new skins (non-static ids) append at the end, grouped by Kaeli
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

    /// <summary>skin id → waifu id (skin owner).</summary>
    public IReadOnlyDictionary<string, string> SkinOwner =>
        All.SelectMany(w => w.Skins.Select(s => (s.Id, w.Id))).ToDictionary(p => p.Item1, p => p.Item2);

    public WaifuDef? Find(string waifuId) => ById.GetValueOrDefault(waifuId);
}
