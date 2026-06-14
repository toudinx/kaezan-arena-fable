using System.Globalization;
using System.Text;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Uma skin autoral criada no Outfit Studio do painel admin: pertence a uma Kaeli do roster
/// (<see cref="WaifuId"/>) e descreve o visual (outfit + recolor das 4 regiões + addons + montaria)
/// junto da regra de desbloqueio. Persistida em <c>.data/content/kaeli-skins.json</c> e mesclada
/// ao roster estático pelo <see cref="KaeliRegistry"/>. IDs <c>skin:*</c> são estáveis.
/// </summary>
public sealed record KaeliSkinDefinition(
    string WaifuId,
    string Id,
    string Name,
    string Description,
    int LookType,
    int Head,
    int Body,
    int Legs,
    int Feet,
    int Addons,
    int MountLookType,
    string Unlock,
    int UnlockValue);

public static class KaeliAuthoring
{
    public static readonly string[] UnlockKinds = ["default", "affinity", "gold", "kaeros"];

    /// <summary>Faixa de cor HSI do Tibia: 7 bandas SI × 19 matizes = 133 cores (0..132).</summary>
    public const int OutfitColorCount = 133;

    public static KaeliSkinDefinition Normalize(KaeliSkinDefinition def, string? forcedId = null)
    {
        var waifuId = def.WaifuId.Trim().ToLowerInvariant();
        var id = forcedId ?? def.Id;
        return def with
        {
            WaifuId = waifuId,
            Id = string.IsNullOrWhiteSpace(id) ? CreateId(waifuId, def.Name) : id.Trim().ToLowerInvariant(),
            Name = def.Name.Trim(),
            Description = def.Description.Trim(),
            LookType = Math.Max(0, def.LookType),
            Head = ClampColor(def.Head),
            Body = ClampColor(def.Body),
            Legs = ClampColor(def.Legs),
            Feet = ClampColor(def.Feet),
            Addons = Math.Clamp(def.Addons, 0, 3),
            MountLookType = Math.Max(0, def.MountLookType),
            Unlock = KnownUnlock(def.Unlock),
            UnlockValue = Math.Max(0, def.UnlockValue)
        };
    }

    public static string? Validate(KaeliSkinDefinition def)
    {
        if (!Waifus.ById.ContainsKey(def.WaifuId)) return "Kaeli desconhecida";
        if (string.IsNullOrWhiteSpace(def.Name)) return "nome vazio";
        if (!def.Id.StartsWith("skin:", StringComparison.Ordinal)) return "id deve comecar com 'skin:'";
        if (def.LookType <= 0) return "lookType invalido";
        if (!UnlockKinds.Contains(def.Unlock)) return "regra de desbloqueio invalida";
        if (def.Unlock == "affinity" && (def.UnlockValue < 1 || def.UnlockValue > GameConfig.AffinityMaxLevel))
            return $"nivel de afinidade deve estar entre 1 e {GameConfig.AffinityMaxLevel}";
        return null;
    }

    public static SkinDef ToSkin(KaeliSkinDefinition def) => new(
        def.Id, def.Name, def.Description,
        def.LookType, def.Head, def.Body, def.Legs, def.Feet,
        def.Unlock, def.UnlockValue, def.Addons, def.MountLookType);

    public static string CreateId(string waifuId, string name)
    {
        var owner = Slug(waifuId.StartsWith("waifu:", StringComparison.Ordinal) ? waifuId[6..] : waifuId);
        var slug = Slug(name);
        return $"skin:{owner}:{(string.IsNullOrEmpty(slug) ? "skin" : slug)}";
    }

    private static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var separator = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                separator = false;
            }
            else if (!separator && builder.Length > 0)
            {
                builder.Append('-');
                separator = true;
            }
        }
        return builder.ToString().Trim('-');
    }

    private static int ClampColor(int color) => Math.Clamp(color, 0, OutfitColorCount - 1);

    private static string KnownUnlock(string unlock) =>
        UnlockKinds.Contains(unlock?.Trim().ToLowerInvariant()) ? unlock!.Trim().ToLowerInvariant() : "default";
}
