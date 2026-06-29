using KaezanArenaFable.Api.Domain;

namespace BalanceSim;

/// <summary>
/// Synthesizes the "recommended" gear for a tier (full BIS set by slot) and aggregates it into
/// <see cref="EquipmentStats"/> — the same path as the real game (<see cref="ItemAuthoring"/>
/// normalizes recommended stats by tier; <see cref="EquipmentStatAggregator.Aggregate"/> sums slots).
/// This is the equipment the sweep uses to measure TTK (gear x mob on the same tier).
/// </summary>
internal static class RecommendedGear
{
    public static EquipmentStats ForTier(int tier)
    {
        var loadout = new Dictionary<string, int>();
        var items = new Dictionary<int, ItemType>();
        var nextId = 1;

        void Add(string slot, string? weaponType, AuthoredItemDefinition flagged)
        {
            var id = nextId++;
            // Normalize recomputes every stat from tier recommendations based on slot + enabled
            // capabilities (field > 0 becomes the full recommended stat).
            var normalized = ItemAuthoring.Normalize(flagged with { Slot = slot, WeaponType = weaponType }, id);
            var source = new ItemType(id, $"sim:{slot}", 0, slot, weaponType);
            items[id] = normalized.Apply(source);
            loadout[slot] = id;
        }

        var blank = Blank(tier);
        Add(EquipmentSlots.Weapon, "sword", blank with { CritDamage = 1 });
        Add(EquipmentSlots.Armor, null, blank with { PhysicalResistance = 1 });
        Add(EquipmentSlots.Helmet, null, blank with { LifeStealChance = 1, LifeStealAmount = 1, CooldownReduction = 1 });
        Add(EquipmentSlots.Ring, null, blank with { CritChance = 1 });
        Add(EquipmentSlots.Necklace, null, blank with { ElementDamage = 1 });
        Add(EquipmentSlots.Mount, null, blank with { MoveSpeedPercent = 1 });

        return EquipmentStatAggregator.Aggregate(loadout, items);
    }

    private static AuthoredItemDefinition Blank(int tier) => new(
        ItemId: 0, SourceItemId: 0, Name: "sim", Description: "", SalePrice: 0,
        Attack: 0, Armor: 0, Defense: 0, MountSpeed: 0,
        Element: "physical", ElementDamage: 0, SkillPower: 0,
        CritChance: 0, CritDamage: 0, LifeStealChance: 0, LifeStealAmount: 0,
        CooldownReduction: 0, MoveSpeedPercent: 0,
        PhysicalResistance: 0, FireResistance: 0, IceResistance: 0, EarthResistance: 0,
        EnergyResistance: 0, DeathResistance: 0, HolyResistance: 0,
        AllowedClassIds: Array.Empty<string>(), RequiredMasteryPoints: 0,
        Tier: tier);
}
