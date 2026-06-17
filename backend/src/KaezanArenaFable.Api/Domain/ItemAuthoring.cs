namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Item autoral do Kaezan. O SourceItemId aponta para um objeto imutavel do Canary e fornece
/// sprite; slot, tipo e balanceamento sao identidade propria do Kaezan.
/// Percentuais usam fracao (0.10 = 10%).
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
    string? WeaponType = null)
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
            RequiredMasteryPoints = RequiredMasteryPoints
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
        source.WeaponType);

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
        return definition with
        {
            ItemId = itemId ?? definition.ItemId,
            Name = definition.Name.Trim(),
            Description = definition.Description.Trim(),
            SalePrice = Math.Clamp(definition.SalePrice, 0, GameConfig.ItemMaxSalePrice),
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
            WeaponType = weaponType
        };
    }

    public static string? Validate(
        AuthoredItemDefinition definition,
        ItemType? source,
        IEnumerable<AuthoredItemDefinition> existing,
        int? currentId = null)
    {
        if (source is null || source.IsAuthored)
            return $"item Canary desconhecido: {definition.SourceItemId}";
        if (string.IsNullOrWhiteSpace(definition.Name))
            return "nome obrigatorio";
        if (existing.Any(item =>
                item.ItemId != currentId
                && item.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)))
            return $"nome ja existe: {definition.Name}";

        var effective = definition.Apply(source);
        var caps = ItemCapabilities.For(effective);
        if (!caps.Attack && definition.Attack != 0) return "ataque so pode ser definido em armas";
        if (!caps.Armor && definition.Armor != 0) return "armadura so pode ser definida em equipamentos defensivos";
        if (!caps.Defense && definition.Defense != 0) return "defesa so pode ser definida em armas ou equipamentos defensivos";
        if (!caps.MountSpeed && definition.MountSpeed != 0) return "velocidade de montaria so pode ser definida em montarias";
        if (!caps.CritDamage && definition.CritDamage != 0) return "dano critico so pode ser definido em armas";
        if (!caps.CritChance && definition.CritChance != 0) return "chance critica so pode ser definida em aneis";
        if (!caps.Vampiric && (definition.LifeStealChance != 0 || definition.LifeStealAmount != 0))
            return "vampirismo so pode ser definido em capacetes";
        if (!caps.CooldownReduction && definition.CooldownReduction != 0)
            return "recarga so pode ser definida em capacetes";
        if (!caps.MoveSpeed && definition.MoveSpeedPercent != 0)
            return "movimento so pode ser definido em montarias";
        if (!caps.ElementAffinity && definition.ElementDamage != 0)
            return "afinidade elemental so pode ser definida em amuletos";
        if (!caps.PhysicalResistance && definition.PhysicalResistance != 0)
            return "resistencia fisica so pode ser definida em armaduras";
        if (!caps.ElementResistance && HasElementResistance(definition))
            return "resistencia elemental so pode ser definida em armaduras";
        if (ElementResistanceCount(definition) > 1)
            return "armaduras podem ter no maximo uma resistencia elemental";
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

/// <summary>Taxonomia tipo TibiaWiki usada apenas na apresentacao do acervo visual.</summary>
public static class ItemCategories
{
    public static (string Category, string Subcategory) Of(ItemType item, bool isFood)
    {
        if (!string.IsNullOrWhiteSpace(item.WeaponType))
            return ("Armas", WeaponSub(item.WeaponType!));
        return item.Slot switch
        {
            "helmet" => ("Equipamentos", "Capacetes"),
            "armor" => ("Equipamentos", "Armaduras"),
            "necklace" => ("Equipamentos", "Amuletos"),
            "ring" => ("Equipamentos", "Aneis"),
            "mount" => ("Equipamentos", "Montarias"),
            "weapon" => ("Armas", "Outras armas"),
            _ => ("Outros", OtherSub(item.Name, isFood))
        };
    }

    private static string WeaponSub(string weaponType) => weaponType.ToLowerInvariant() switch
    {
        "sword" => "Espadas",
        "axe" => "Machados",
        "club" or "fist" => "Macas e punhos",
        "distance" => "Distancia",
        "wand" or "rod" => "Cajados",
        "spellbook" => "Spellbooks",
        "shield" => "Escudos",
        "ammunition" => "Municao",
        _ => "Outras armas"
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
        if (lower.Contains("rune")) return "Runas";
        if (lower.Contains("potion")) return "Pocoes";
        if (isFood) return "Comida";
        if (ValuableWords.Any(lower.Contains)) return "Valiosos";
        if (CreatureProductWords.Any(word => lower.Split(' ', '_').Contains(word)))
            return "Produtos de criatura";
        return "Diversos";
    }
}
