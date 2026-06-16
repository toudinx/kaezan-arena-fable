namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Item autoral do Kaezan. O SourceItemId aponta para um objeto imutavel do Canary e fornece
/// sprite, slot e tipo de arma; os demais campos sao balanceamento proprio do jogo.
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
    int RequiredMasteryPoints)
{
    public ItemType Apply(ItemType source) => source with
    {
        ItemId = ItemId,
        SourceItemId = SourceItemId,
        IsAuthored = true,
        Name = Name,
        Description = Description,
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
        source.SkillPower,
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
        DefaultClasses(source),
        0);

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
            SkillPower = Math.Clamp(definition.SkillPower, 0, GameConfig.ItemMaxSkillPower),
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
            RequiredMasteryPoints = Math.Max(0, definition.RequiredMasteryPoints)
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
        if (definition.AllowedClassIds.Count == 0 && source.Slot is not null)
            return "selecione ao menos uma classe permitida";
        if (existing.Any(item =>
                item.ItemId != currentId
                && item.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)))
            return $"nome ja existe: {definition.Name}";

        var caps = ItemCapabilities.For(source);
        if (!caps.Attack && definition.Attack != 0) return "ataque so pode ser definido em armas";
        if (!caps.Armor && definition.Armor != 0) return "armadura so pode ser definida em equipamentos defensivos";
        if (!caps.Defense && definition.Defense != 0) return "defesa so pode ser definida em armas ou equipamentos defensivos";
        if (!caps.MountSpeed && definition.MountSpeed != 0) return "velocidade de montaria so pode ser definida em montarias";
        if (!caps.Offense && HasOffense(definition)) return "atributos ofensivos so podem ser definidos em armas";
        if (!caps.Support && (definition.CooldownReduction != 0 || definition.MoveSpeedPercent != 0))
            return "atributos de suporte exigem arma ou equipamento";
        if (!caps.Resistance && HasResistance(definition))
            return "resistencias exigem arma ou equipamento defensivo";
        return null;
    }

    public static IReadOnlyList<string> DefaultClasses(ItemType source) =>
        source.WeaponType?.ToLowerInvariant() switch
        {
            "fist" => [Classes.MonkId, Classes.WarriorId],
            "sword" or "axe" or "club" => [Classes.WarriorId, Classes.MonkId],
            "distance" or "ammunition" => [Classes.SentinelId, Classes.ShamanId],
            "wand" or "rod" or "spellbook" => [Classes.SentinelId, Classes.ShamanId, Classes.WizardId, Classes.NecromancerId],
            "shield" => [Classes.WarriorId, Classes.SentinelId],
            _ => Classes.All.Select(c => c.Id).ToArray()
        };

    private static double Clamp(double value, double max) => Math.Clamp(value, 0, max);
    private static bool HasOffense(AuthoredItemDefinition item) =>
        item.ElementDamage != 0 || item.SkillPower != 0 || item.CritChance != 0
        || item.CritDamage != 0 || item.LifeStealChance != 0 || item.LifeStealAmount != 0;
    private static bool HasResistance(AuthoredItemDefinition item) =>
        item.PhysicalResistance != 0 || item.FireResistance != 0 || item.IceResistance != 0
        || item.EarthResistance != 0 || item.EnergyResistance != 0
        || item.DeathResistance != 0 || item.HolyResistance != 0;
}

public sealed record ItemCapabilities(
    bool Attack, bool Armor, bool Defense, bool MountSpeed,
    bool Offense, bool Support, bool Resistance)
{
    public static ItemCapabilities For(ItemType item)
    {
        var weapon = item.Slot == EquipmentSlots.Weapon || !string.IsNullOrWhiteSpace(item.WeaponType);
        var defensive = item.Slot is EquipmentSlots.Helmet or EquipmentSlots.Armor
            or EquipmentSlots.Necklace or EquipmentSlots.Ring;
        return new(
            weapon,
            defensive,
            weapon || defensive,
            item.Slot == EquipmentSlots.Mount,
            weapon,
            weapon || defensive,
            weapon || defensive);
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
