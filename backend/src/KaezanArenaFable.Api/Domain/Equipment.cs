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
    int MountLookType)
{
    public static readonly EquipmentStats Empty = new(0, 0, 0, 0, 0);
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
            moveSpeed,
            mountLookType);
    }
}
