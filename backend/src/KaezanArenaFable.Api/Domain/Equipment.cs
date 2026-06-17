namespace KaezanArenaFable.Api.Domain;

public static class EquipmentSlots
{
    public const string Helmet = "helmet";
    public const string Armor = "armor";
    public const string Weapon = "weapon";
    public const string Necklace = "necklace";
    public const string Ring = "ring";
    public const string Mount = "mount";

    public static readonly string[] All = [Helmet, Armor, Weapon, Necklace, Ring, Mount];

    public static bool IsValid(string slot) => All.Contains(slot, StringComparer.Ordinal);
}

public sealed record EquipmentStats(
    double AttackBonus,
    int MaxHpBonus,
    double DamageReduction,
    double MoveSpeedPercent,
    int MountLookType,
    string WeaponElement,
    string Element,
    double ElementDamageBonus,
    double SkillPowerMultiplier,
    double CritChance,
    double CritDamage,
    double LifeStealChance,
    double LifeStealAmount,
    double CooldownReduction,
    IReadOnlyDictionary<string, double> Resistances)
{
    public static readonly EquipmentStats Empty = new(
        0, 0, 0, 0, 0, "physical", "physical", 0, 1, 0, 0, 0, 0, 0,
        new Dictionary<string, double>());

    public double Resistance(string damageType) =>
        Resistances.GetValueOrDefault(damageType.ToLowerInvariant());
}

public static class EquipmentStatAggregator
{
    public static EquipmentStats Aggregate(
        IReadOnlyDictionary<string, int>? loadout,
        IReadOnlyDictionary<int, ItemType> items)
    {
        if (loadout is null || loadout.Count == 0) return EquipmentStats.Empty;

        double attack = 0;
        var hp = 0;
        double reduction = 0;
        double moveSpeed = 0;
        var mountLookType = 0;
        var weaponElement = "physical";
        var strongestElement = "physical";
        double strongestElementDamage = 0;
        var skillPower = 0;
        double critChance = 0, critDamage = 0;
        double lifeStealChance = 0, lifeStealAmount = 0;
        double cooldownReduction = 0;
        var resistances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in EquipmentSlots.All)
        {
            if (!loadout.TryGetValue(slot, out var itemId)
                || !items.TryGetValue(itemId, out var item)
                || item.Slot != slot)
                continue;

            attack += item.Attack * GameConfig.EquipmentAttackScale;
            hp += item.Armor * GameConfig.EquipmentHpPerArmor
                  + item.Defense * GameConfig.EquipmentHpPerDefense;
            reduction += item.Armor * GameConfig.EquipmentDamageReductionPerArmor
                         + item.Defense * GameConfig.EquipmentDamageReductionPerDefense;
            skillPower += item.SkillPower;
            critChance += item.CritChance;
            critDamage += item.CritDamage;
            lifeStealChance += item.LifeStealChance;
            lifeStealAmount += item.LifeStealAmount;
            cooldownReduction += item.CooldownReduction;
            moveSpeed += item.MoveSpeedPercent;

            if (slot == EquipmentSlots.Weapon)
                weaponElement = item.Element;

            var elementBonus = item.IsAuthored
                ? item.ElementDamage / 100.0
                : item.ElementDamage * GameConfig.EquipmentAttackScale / 100.0;
            if (elementBonus > strongestElementDamage)
            {
                strongestElement = item.Element;
                strongestElementDamage = elementBonus;
            }

            AddResistance("physical", item.PhysicalResistance);
            AddResistance("fire", item.FireResistance);
            AddResistance("ice", item.IceResistance);
            AddResistance("earth", item.EarthResistance);
            AddResistance("energy", item.EnergyResistance);
            AddResistance("death", item.DeathResistance);
            AddResistance("holy", item.HolyResistance);

            if (slot == EquipmentSlots.Mount)
            {
                hp += item.MountSpeed * GameConfig.MountHpPerSpeed;
                moveSpeed += item.MountSpeed * GameConfig.MountMoveSpeedPercentPerSpeed;
                mountLookType = item.MountLookType;
            }
        }

        return new EquipmentStats(
            attack,
            hp,
            Math.Min(reduction, GameConfig.EquipmentDamageReductionCap),
            Math.Min(moveSpeed, GameConfig.EquipmentMoveSpeedCap),
            mountLookType,
            weaponElement,
            strongestElement,
            strongestElementDamage,
            1 + skillPower * GameConfig.EquipmentSkillPowerPerPoint,
            Math.Min(critChance, GameConfig.EquipmentCritChanceCap),
            Math.Min(critDamage, GameConfig.EquipmentCritDamageCap),
            Math.Min(lifeStealChance, GameConfig.EquipmentLifeStealChanceCap),
            Math.Min(lifeStealAmount, GameConfig.EquipmentLifeStealAmountCap),
            Math.Min(cooldownReduction, GameConfig.EquipmentCooldownReductionCap),
            resistances);

        void AddResistance(string element, double value)
        {
            if (value <= 0) return;
            resistances[element] = Math.Min(
                resistances.GetValueOrDefault(element) + value,
                GameConfig.EquipmentResistanceCap);
        }
    }
}
