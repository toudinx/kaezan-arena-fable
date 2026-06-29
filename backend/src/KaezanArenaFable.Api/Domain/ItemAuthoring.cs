namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Authored Kaezan item. SourceItemId points to an immutable Canary object and provides the
/// sprite; slot, type, and balance are Kaezan-owned identity.
/// Percent values use fractions (0.10 = 10%).
/// </summary>
public sealed record AuthoredItemDefinition(
    int ItemId,
    int SourceItemId,
    string Name,
    string Description,
    int SalePrice,
    int Attack,
    int Armor,
    int Defense,
    int MountSpeed,
    string Element,
    int ElementDamage,
    int SkillPower,
    double CritChance,
    double CritDamage,
    double LifeStealChance,
    double LifeStealAmount,
    double CooldownReduction,
    double MoveSpeedPercent,
    double PhysicalResistance,
    double FireResistance,
    double IceResistance,
    double EarthResistance,
    double EnergyResistance,
    double DeathResistance,
    double HolyResistance,
    IReadOnlyList<string> AllowedClassIds,
    int RequiredMasteryPoints,
    int Tier = 0,
    string? Slot = null,
    string? WeaponType = null,
    string Tag = GameConfig.AuthoredItemTagNormal,
    double StatMultiplier = 1)
{
    public ItemType Apply(ItemType source)
    {
        var effectiveSlot = Slot ?? source.Slot;
        return source with
        {
            Tier = Tier,
            ItemId = ItemId,
            SourceItemId = SourceItemId,
            IsAuthored = true,
            Name = Name,
            Description = Description,
            Slot = effectiveSlot,
            WeaponType = effectiveSlot == EquipmentSlots.Weapon ? (WeaponType ?? source.WeaponType) : null,
            SalePrice = SalePrice,
            Attack = Attack,
            Armor = Armor,
            Defense = Defense,
            MountSpeed = MountSpeed,
            Element = Element,
            ElementDamage = ElementDamage,
            SkillPower = SkillPower,
            CritChance = CritChance,
            CritDamage = CritDamage,
            LifeStealChance = LifeStealChance,
            LifeStealAmount = LifeStealAmount,
            CooldownReduction = CooldownReduction,
            MoveSpeedPercent = MoveSpeedPercent,
            PhysicalResistance = PhysicalResistance,
            FireResistance = FireResistance,
            IceResistance = IceResistance,
            EarthResistance = EarthResistance,
            EnergyResistance = EnergyResistance,
            DeathResistance = DeathResistance,
            HolyResistance = HolyResistance,
            AllowedClassIds = AllowedClassIds,
            RequiredMasteryPoints = RequiredMasteryPoints,
            Tag = Tag,
            StatMultiplier = StatMultiplier
        };
    }
}

public static class ItemAuthoring
{
    public static readonly string[] Elements =
        ["physical", "fire", "ice", "earth", "energy", "death", "holy"];

    public static AuthoredItemDefinition FromSource(ItemType source) => new(
        0,
        source.ItemId,
        source.Name,
        "",
        source.SalePrice,
        source.Attack,
        source.Armor,
        source.Defense,
        source.MountSpeed,
        source.Element,
        source.ElementDamage,
        0,
        source.CritChance,
        source.CritDamage,
        source.LifeStealChance,
        source.LifeStealAmount,
        source.CooldownReduction,
        source.MoveSpeedPercent,
        source.PhysicalResistance,
        source.FireResistance,
        source.IceResistance,
        source.EarthResistance,
        source.EnergyResistance,
        source.DeathResistance,
        source.HolyResistance,
        [],
        0,
        source.Tier,
        source.Slot,
        source.WeaponType,
        source.Tag,
        source.StatMultiplier);

    public static AuthoredItemDefinition Normalize(
        AuthoredItemDefinition definition, int? itemId = null)
    {
        var classes = definition.AllowedClassIds
            .Where(id => Classes.ById.ContainsKey(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var element = Elements.Contains(definition.Element, StringComparer.OrdinalIgnoreCase)
            ? definition.Element.ToLowerInvariant()
            : "physical";
        var slot = string.IsNullOrWhiteSpace(definition.Slot)
            ? null
            : definition.Slot.ToLowerInvariant();
        if (slot is not null && !EquipmentSlots.IsValid(slot))
            slot = null;
        var weaponType = slot == EquipmentSlots.Weapon && !string.IsNullOrWhiteSpace(definition.WeaponType)
            ? definition.WeaponType.Trim().ToLowerInvariant()
            : null;
        var tag = definition.Tag.Equals(GameConfig.AuthoredItemTagRelic, StringComparison.OrdinalIgnoreCase)
            ? GameConfig.AuthoredItemTagRelic
            : GameConfig.AuthoredItemTagNormal;
        var statMultiplier = tag == GameConfig.AuthoredItemTagRelic
            ? Math.Clamp(
                definition.StatMultiplier > 0 ? definition.StatMultiplier : GameConfig.AuthoredItemRelicMultiplierDefault,
                GameConfig.AuthoredItemRelicMultiplierMin,
                GameConfig.AuthoredItemRelicMultiplierMax)
            : 1;
        var normalized = definition with
        {
            ItemId = itemId ?? definition.ItemId,
            Name = definition.Name.Trim(),
            Description = definition.Description.Trim(),
            SalePrice = GameConfig.AuthoredItemSalePrice(definition.Tier),
            Attack = Math.Clamp(definition.Attack, 0, GameConfig.ItemMaxAttack),
            Armor = Math.Clamp(definition.Armor, 0, GameConfig.ItemMaxArmor),
            Defense = Math.Clamp(definition.Defense, 0, GameConfig.ItemMaxDefense),
            MountSpeed = Math.Clamp(definition.MountSpeed, 0, GameConfig.ItemMaxMountSpeed),
            Element = element,
            ElementDamage = Math.Clamp(definition.ElementDamage, 0, GameConfig.ItemMaxElementDamage),
            SkillPower = 0,
            CritChance = Clamp(definition.CritChance, GameConfig.EquipmentCritChanceCap),
            CritDamage = Clamp(definition.CritDamage, GameConfig.EquipmentCritDamageCap),
            LifeStealChance = Clamp(definition.LifeStealChance, GameConfig.EquipmentLifeStealChanceCap),
            LifeStealAmount = Clamp(definition.LifeStealAmount, GameConfig.EquipmentLifeStealAmountCap),
            CooldownReduction = Clamp(definition.CooldownReduction, GameConfig.EquipmentCooldownReductionCap),
            MoveSpeedPercent = Clamp(definition.MoveSpeedPercent, GameConfig.EquipmentMoveSpeedCap),
            PhysicalResistance = Clamp(definition.PhysicalResistance, GameConfig.EquipmentResistanceCap),
            FireResistance = Clamp(definition.FireResistance, GameConfig.EquipmentResistanceCap),
            IceResistance = Clamp(definition.IceResistance, GameConfig.EquipmentResistanceCap),
            EarthResistance = Clamp(definition.EarthResistance, GameConfig.EquipmentResistanceCap),
            EnergyResistance = Clamp(definition.EnergyResistance, GameConfig.EquipmentResistanceCap),
            DeathResistance = Clamp(definition.DeathResistance, GameConfig.EquipmentResistanceCap),
            HolyResistance = Clamp(definition.HolyResistance, GameConfig.EquipmentResistanceCap),
            AllowedClassIds = classes,
            RequiredMasteryPoints = 0,
            Tier = Math.Clamp(definition.Tier, 0, 5),
            Slot = slot,
            WeaponType = weaponType,
            Tag = tag,
            StatMultiplier = statMultiplier
        };
        return ApplyTierBalance(normalized);
    }

    public static string? Validate(
        AuthoredItemDefinition definition,
        ItemType? source,
        IEnumerable<AuthoredItemDefinition> existing,
        int? currentId = null)
    {
        if (source is null || source.IsAuthored)
            return $"unknown Canary item: {definition.SourceItemId}";
        if (string.IsNullOrWhiteSpace(definition.Name))
            return "name is required";
        if (existing.Any(item =>
                item.ItemId != currentId
                && item.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)))
            return $"name already exists: {definition.Name}";

        var effective = definition.Apply(source);
        var caps = ItemCapabilities.For(effective);
        if (!caps.Attack && definition.Attack != 0) return "attack can only be defined on weapons";
        if (!caps.Armor && definition.Armor != 0) return "armor can only be defined on defensive equipment";
        if (!caps.Defense && definition.Defense != 0) return "defense can only be defined on weapons or defensive equipment";
        if (!caps.MountSpeed && definition.MountSpeed != 0) return "mount speed can only be defined on mounts";
        if (!caps.CritDamage && definition.CritDamage != 0) return "critical damage can only be defined on weapons";
        if (!caps.CritChance && definition.CritChance != 0) return "critical chance can only be defined on rings";
        if (!caps.Vampiric && (definition.LifeStealChance != 0 || definition.LifeStealAmount != 0))
            return "vampirism can only be defined on helmets";
        if (!caps.CooldownReduction && definition.CooldownReduction != 0)
            return "cooldown reduction can only be defined on helmets";
        if (!caps.MoveSpeed && definition.MoveSpeedPercent != 0)
            return "movement can only be defined on mounts";
        if (!caps.ElementAffinity && definition.ElementDamage != 0)
            return "elemental affinity can only be defined on amulets";
        if (!caps.PhysicalResistance && definition.PhysicalResistance != 0)
            return "physical resistance can only be defined on armor";
        if (!caps.ElementResistance && HasElementResistance(definition))
            return "elemental resistance can only be defined on armor";
        if (ElementResistanceCount(definition) > 1)
            return "armor can have at most one elemental resistance";
        return null;
    }

    public static IReadOnlyList<string> DefaultClasses(ItemType source) =>
        source.WeaponType?.ToLowerInvariant() switch
        {
            "fist" => [Classes.BarbarianId, Classes.WarriorId],
            "sword" or "axe" or "club" => [Classes.WarriorId, Classes.BarbarianId],
            "distance" or "ammunition" => [Classes.SentinelId],
            "wand" or "rod" or "spellbook" => [Classes.OracleId, Classes.ShamanId, Classes.CryomancerId, Classes.PyromancerId, Classes.StormcallerId, Classes.NecromancerId],
            "shield" => [Classes.WarriorId, Classes.SentinelId],
            _ => Classes.All.Select(c => c.Id).ToArray()
        };

    private static double Clamp(double value, double max) => Math.Clamp(value, 0, max);
    private static bool HasElementResistance(AuthoredItemDefinition item) => ElementResistanceCount(item) > 0;

    private static AuthoredItemDefinition ApplyTierBalance(AuthoredItemDefinition item)
    {
        var slot = item.Slot;
        var hasCritDamage = item.CritDamage > 0;
        var hasCritChance = item.CritChance > 0;
        var hasVampiric = item.LifeStealChance > 0 || item.LifeStealAmount > 0;
        var hasCooldown = item.CooldownReduction > 0;
        var hasMoveSpeed = item.MoveSpeedPercent > 0;
        var hasElementAffinity = item.ElementDamage > 0;
        var hasPhysicalResistance = item.PhysicalResistance > 0;
        var elementResistance = SelectedElementResistance(item);
        var tier = Math.Clamp(item.Tier, 0, 5);
        var multiplier = item.StatMultiplier;

        var next = item with
        {
            Tier = tier,
            SalePrice = ScaleInt(GameConfig.AuthoredItemSalePrice(tier), multiplier, GameConfig.ItemMaxSalePrice),
            Attack = slot == EquipmentSlots.Weapon ? ScaleRecommendedInt(tier, "attack", multiplier, GameConfig.ItemMaxAttack) : 0,
            Armor = slot is EquipmentSlots.Armor or EquipmentSlots.Helmet
                ? ScaleRecommendedInt(tier, "armor", multiplier, GameConfig.ItemMaxArmor)
                : 0,
            Defense = slot is EquipmentSlots.Ring or EquipmentSlots.Necklace
                ? ScaleRecommendedInt(tier, "defense", multiplier, GameConfig.ItemMaxDefense)
                : 0,
            MountSpeed = slot == EquipmentSlots.Mount
                ? ScaleRecommendedInt(tier, "mountSpeed", multiplier, GameConfig.ItemMaxMountSpeed)
                : 0,
            ElementDamage = 0,
            SkillPower = 0,
            CritChance = 0,
            CritDamage = 0,
            LifeStealChance = 0,
            LifeStealAmount = 0,
            CooldownReduction = 0,
            MoveSpeedPercent = 0,
            PhysicalResistance = 0,
            FireResistance = 0,
            IceResistance = 0,
            EarthResistance = 0,
            EnergyResistance = 0,
            DeathResistance = 0,
            HolyResistance = 0
        };

        if (slot == EquipmentSlots.Weapon && hasCritDamage)
            next = next with { CritDamage = ScaleRecommended(tier, "critDamage", multiplier, GameConfig.EquipmentCritDamageCap) };
        if (slot == EquipmentSlots.Ring && hasCritChance)
            next = next with { CritChance = ScaleRecommended(tier, "critChance", multiplier, GameConfig.EquipmentCritChanceCap) };
        if (slot == EquipmentSlots.Helmet && hasVampiric)
            next = next with
            {
                LifeStealChance = ScaleRecommended(tier, "lifeStealChance", multiplier, GameConfig.EquipmentLifeStealChanceCap),
                LifeStealAmount = ScaleRecommended(tier, "lifeStealAmount", multiplier, GameConfig.EquipmentLifeStealAmountCap)
            };
        if (slot == EquipmentSlots.Helmet && hasCooldown)
            next = next with { CooldownReduction = ScaleRecommended(tier, "cooldownReduction", multiplier, GameConfig.EquipmentCooldownReductionCap) };
        if (slot == EquipmentSlots.Mount && hasMoveSpeed)
            next = next with { MoveSpeedPercent = ScaleRecommended(tier, "moveSpeedPercent", multiplier, GameConfig.EquipmentMoveSpeedCap) };
        if (slot == EquipmentSlots.Necklace && hasElementAffinity)
            next = next with { ElementDamage = ScaleRecommendedInt(tier, "elementDamage", multiplier, GameConfig.ItemMaxElementDamage) };
        if (slot == EquipmentSlots.Armor && hasPhysicalResistance)
            next = next with { PhysicalResistance = ScaleRecommended(tier, "resistance", multiplier, GameConfig.EquipmentResistanceCap) };
        if (slot == EquipmentSlots.Armor && elementResistance is { } element)
            next = SetElementResistance(next, element, ScaleRecommended(tier, "resistance", multiplier, GameConfig.EquipmentResistanceCap));

        return next;
    }

    private static int ScaleRecommendedInt(int tier, string stat, double multiplier, int max) =>
        ScaleInt(GameConfig.AuthoredItemRecommendedInt(tier, stat), multiplier, max);

    private static int ScaleInt(int value, double multiplier, int max) =>
        Math.Clamp((int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero), 0, max);

    private static double ScaleRecommended(int tier, string stat, double multiplier, double max) =>
        Math.Clamp(GameConfig.AuthoredItemRecommendedValue(tier, stat) * multiplier, 0, max);

    private static string? SelectedElementResistance(AuthoredItemDefinition item)
    {
        if (item.FireResistance > 0) return "fire";
        if (item.IceResistance > 0) return "ice";
        if (item.EarthResistance > 0) return "earth";
        if (item.EnergyResistance > 0) return "energy";
        if (item.DeathResistance > 0) return "death";
        return item.HolyResistance > 0 ? "holy" : null;
    }

    private static AuthoredItemDefinition SetElementResistance(
        AuthoredItemDefinition item, string element, double value) =>
        element switch
        {
            "fire" => item with { FireResistance = value },
            "ice" => item with { IceResistance = value },
            "earth" => item with { EarthResistance = value },
            "energy" => item with { EnergyResistance = value },
            "death" => item with { DeathResistance = value },
            "holy" => item with { HolyResistance = value },
            _ => item
        };

    private static int ElementResistanceCount(AuthoredItemDefinition item) =>
        Count(item.FireResistance) + Count(item.IceResistance) + Count(item.EarthResistance)
        + Count(item.EnergyResistance) + Count(item.DeathResistance) + Count(item.HolyResistance);

    private static int Count(double value) => value != 0 ? 1 : 0;
}

public sealed record ItemCapabilities(
    bool Attack, bool Armor, bool Defense, bool MountSpeed,
    bool Offense, bool Support, bool Resistance,
    bool CritChance, bool CritDamage, bool Vampiric, bool CooldownReduction,
    bool MoveSpeed, bool PhysicalResistance, bool ElementResistance, bool ElementAffinity)
{
    public static ItemCapabilities For(ItemType item)
    {
        var weapon = item.Slot == EquipmentSlots.Weapon || !string.IsNullOrWhiteSpace(item.WeaponType);
        var defensive = item.Slot is EquipmentSlots.Helmet or EquipmentSlots.Armor
            or EquipmentSlots.Necklace or EquipmentSlots.Ring;
        var armor = item.Slot == EquipmentSlots.Armor;
        var helmet = item.Slot == EquipmentSlots.Helmet;
        var ring = item.Slot == EquipmentSlots.Ring;
        var necklace = item.Slot == EquipmentSlots.Necklace;
        var mount = item.Slot == EquipmentSlots.Mount;
        return new(
            weapon,
            item.Slot is EquipmentSlots.Armor or EquipmentSlots.Helmet,
            weapon || defensive,
            mount,
            weapon,
            weapon || defensive,
            armor,
            ring,
            weapon,
            helmet,
            helmet,
            mount,
            armor,
            armor,
            necklace);
    }
}

/// <summary>TibiaWiki-like taxonomy used only for presenting the visual library.</summary>
public static class ItemCategories
{
    public static (string Category, string Subcategory) Of(ItemType item, bool isFood)
    {
        if (!string.IsNullOrWhiteSpace(item.WeaponType))
            return ("Weapons", WeaponSub(item.WeaponType!));
        return item.Slot switch
        {
            "helmet" => ("Equipment", "Helmets"),
            "armor" => ("Equipment", "Armors"),
            "necklace" => ("Equipment", "Amulets"),
            "ring" => ("Equipment", "Rings"),
            "mount" => ("Equipment", "Mounts"),
            "weapon" => ("Weapons", "Other weapons"),
            _ => ("Other", OtherSub(item.Name, isFood))
        };
    }

    private static string WeaponSub(string weaponType) => weaponType.ToLowerInvariant() switch
    {
        "sword" => "Swords",
        "axe" => "Axes",
        "club" or "fist" => "Maces and fists",
        "distance" => "Distance",
        "wand" or "rod" => "Wands",
        "spellbook" => "Spellbooks",
        "shield" => "Shields",
        "ammunition" => "Ammunition",
        _ => "Other weapons"
    };

    private static readonly string[] ValuableWords =
        ["coin", "gem", "pearl", "sapphire", "emerald", "amethyst", "ruby", "diamond",
         "topaz", "nugget", "ingot", "gold", "jewel", "crystal", "talon"];
    private static readonly string[] CreatureProductWords =
        ["fang", "claw", "paw", "hide", "leather", "fur", "skin", "tooth", "teeth", "bone",
         "scale", "wing", "eye", "heart", "tail", "horn", "feather", "tentacle", "silk",
         "powder", "essence", "blood", "brain", "tongue", "gland", "slime", "hoof", "shell",
         "carapace", "hair", "ear"];

    private static string OtherSub(string name, bool isFood)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("rune")) return "Runes";
        if (lower.Contains("potion")) return "Potions";
        if (isFood) return "Food";
        if (ValuableWords.Any(lower.Contains)) return "Valuables";
        if (CreatureProductWords.Any(word => lower.Split(' ', '_').Contains(word)))
            return "Creature products";
        return "Miscellaneous";
    }
}
