using System.Globalization;
using System.Text;

namespace KaezanArenaFable.Api.Domain;

public sealed record MonsterDefinition(
    string Id,
    string Name,
    string Description,
    MonsterOutfit Outfit,
    int Corpse,
    int PowerTier,
    string Rank,
    string BehaviorId,
    string ElementId,
    string StatPresetId,
    double HpMultiplier,
    double DamageMultiplier,
    double SpeedMultiplier,
    double CadenceMultiplier,
    string BestiaryClass,
    Dictionary<string, double> Resistances,
    string AppearanceId = "",
    bool Enabled = true,
    // G-08B: card keyword resistance (% 0-100; negative amplifies). Separate from elemental Resistances.
    Dictionary<string, double>? KeywordResistances = null);

public sealed record MonsterAppearance(
    string Id,
    string Name,
    string Source,
    MonsterOutfit Outfit,
    int Corpse,
    string BestiaryClass,
    string ClassificationSource,
    string Kind,
    string KindSource,
    bool LegacyImported);

public static class MonsterAuthoring
{
    public static MonsterDefinition Normalize(MonsterDefinition definition, string? forcedId = null)
    {
        var id = forcedId ?? definition.Id;
        return definition with
        {
            Id = string.IsNullOrWhiteSpace(id) ? CreateId(definition.Name) : id.Trim().ToLowerInvariant(),
            Name = definition.Name.Trim(),
            Description = definition.Description.Trim(),
            PowerTier = Math.Clamp(definition.PowerTier, 1, 5),
            Rank = NormalizeRank(definition.Rank),
            BehaviorId = KnownBehavior(definition.BehaviorId),
            ElementId = KnownElement(definition.ElementId),
            StatPresetId = KnownPreset(definition.StatPresetId),
            HpMultiplier = ClampModifier(definition.HpMultiplier),
            DamageMultiplier = ClampModifier(definition.DamageMultiplier),
            SpeedMultiplier = ClampModifier(definition.SpeedMultiplier),
            CadenceMultiplier = ClampModifier(definition.CadenceMultiplier),
            BestiaryClass = string.IsNullOrWhiteSpace(definition.BestiaryClass)
                ? "Authored"
                : definition.BestiaryClass.Trim(),
            AppearanceId = definition.AppearanceId.Trim(),
            Resistances = definition.Resistances
                .Where(pair => GameConfig.MonsterElementProfiles.Any(e =>
                    e.Id.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(
                    pair => pair.Key.ToLowerInvariant(),
                    pair => Math.Clamp(pair.Value, GameConfig.AuthoredResistanceMin, GameConfig.AuthoredResistanceMax),
                    StringComparer.OrdinalIgnoreCase),
            KeywordResistances = (definition.KeywordResistances ?? new Dictionary<string, double>())
                .Where(pair => GameConfig.MonsterKeywordTags.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(
                    pair => pair.Key.ToLowerInvariant(),
                    pair => Math.Clamp(pair.Value, GameConfig.KeywordResistMin, GameConfig.KeywordResistMax),
                    StringComparer.OrdinalIgnoreCase)
        };
    }

    public static string? Validate(MonsterDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name)) return "empty name";
        if (!definition.Id.StartsWith("monster:", StringComparison.Ordinal)) return "id must start with 'monster:'";
        if (definition.Outfit.LookType <= 0) return "invalid lookType";
        if (!GameConfig.MonsterStatLines.ContainsKey($"{definition.PowerTier}:{definition.Rank}"))
            return "invalid tier and rank combination";
        return null;
    }

    public static MonsterType Resolve(MonsterDefinition raw)
    {
        var definition = Normalize(raw, raw.Id);
        var stats = GameConfig.MonsterStatLines[$"{definition.PowerTier}:{definition.Rank}"];
        var behavior = GameConfig.MonsterBehaviorProfiles.First(b => b.Id == definition.BehaviorId);
        var element = GameConfig.MonsterElementProfiles.First(e => e.Id == definition.ElementId);
        var damage = stats.Damage * definition.DamageMultiplier;
        var attacks = behavior.Attacks.Select(pattern =>
        {
            var interval = Math.Max(
                GameConfig.TickMs,
                (int)Math.Round(pattern.IntervalMs / definition.CadenceMultiplier));
            MonsterCondition? condition = null;
            if (pattern.ConditionDamageScale > 0 && element.ConditionType is not null)
            {
                condition = new MonsterCondition(
                    element.ConditionType,
                    Math.Max((int)Math.Round(damage * pattern.ConditionDamageScale), 1),
                    2000,
                    6000);
            }

            return new MonsterAttack(
                pattern.Kind,
                $"{behavior.Id}:{element.Id}",
                interval,
                pattern.Chance,
                pattern.Range,
                pattern.Radius,
                pattern.Length,
                pattern.Spread,
                pattern.Target,
                Math.Max((int)Math.Round(damage * pattern.MinDamageScale), 1),
                Math.Max((int)Math.Round(damage * pattern.MaxDamageScale), 1),
                pattern.UsesElement ? element.Id : "physical",
                pattern.UsesElement ? element.ShootEffect : 0,
                pattern.UsesElement ? element.AreaEffect : 0,
                condition);
        }).ToList();

        // G-08B: summoner fills Summons from the behavior profile without a new dispatch path.
        // The tick reuses TickMonsterSummons, and MonsterRegistry resolves the summon by id/name.
        var summons = new List<MonsterSummon>();
        var maxSummons = 0;
        if (!string.IsNullOrWhiteSpace(behavior.SummonSpecies))
        {
            summons.Add(new MonsterSummon(
                behavior.SummonSpecies,
                Math.Clamp(behavior.SummonChance, 0, 100),
                Math.Max(behavior.SummonIntervalMs, GameConfig.SummonMinIntervalMs),
                Math.Max(behavior.SummonCount, 1)));
            maxSummons = Math.Max(behavior.SummonMax, behavior.SummonCount);
        }

        var defenses = new List<MonsterDefense>();
        if (behavior.HealFraction > 0)
        {
            var health = Math.Max((int)Math.Round(stats.Health * definition.HpMultiplier), 1);
            var heal = Math.Max((int)Math.Round(health * behavior.HealFraction), 1);
            defenses.Add(new MonsterDefense(
                "healing", behavior.HealIntervalMs, 65, heal / 2, heal, 0, 0, 13));
        }

        return new MonsterType(
            definition.Name,
            definition.Description,
            Math.Max(stats.Experience, 1),
            Math.Max((int)Math.Round(stats.Health * definition.HpMultiplier), 1),
            Math.Max((int)Math.Round(stats.Speed * definition.SpeedMultiplier), 1),
            definition.Corpse,
            definition.Outfit,
            behavior.TargetDistance,
            behavior.StaticAttackChance,
            0,
            stats.Armor,
            stats.Armor,
            0,
            definition.Rank == "boss",
            definition.BestiaryClass,
            attacks,
            new Dictionary<string, double>(definition.Resistances, StringComparer.OrdinalIgnoreCase),
            [],
            [],
            maxSummons,
            summons,
            defenses,
            "KAEZAN",
            definition.Rank == "boss" ? "authored" : null,
            definition.Id,
            definition.Rank,
            definition.ElementId,
            definition.BehaviorId,
            definition.StatPresetId,
            definition.HpMultiplier,
            definition.DamageMultiplier,
            definition.SpeedMultiplier,
            definition.CadenceMultiplier,
            definition.PowerTier,
            true,
            new Dictionary<string, double>(definition.KeywordResistances ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase));
    }

    public static string CreateId(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
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
        return $"monster:{builder.ToString().Trim('-')}";
    }

    private static string NormalizeRank(string rank) =>
        rank.ToLowerInvariant() is "elite" or "boss" ? rank.ToLowerInvariant() : "common";

    private static string KnownBehavior(string id) =>
        GameConfig.MonsterBehaviorProfiles.Any(b => b.Id == id) ? id : "bruiser";

    private static string KnownElement(string id) =>
        GameConfig.MonsterElementProfiles.Any(e => e.Id == id) ? id : "physical";

    private static string KnownPreset(string id) =>
        GameConfig.MonsterStatPresets.Any(p => p.Id == id) ? id : "balanced";

    private static double ClampModifier(double value) =>
        Math.Clamp(value <= 0 ? 1 : value, GameConfig.AuthoredModifierMin, GameConfig.AuthoredModifierMax);
}
