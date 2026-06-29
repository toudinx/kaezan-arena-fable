using System.Text.Json;

namespace KaezanArenaFable.Api.Domain;

// ---- monster data converted from Canary .lua (tools/convert-monsters) ----

public sealed record MonsterOutfit(int LookType, int Head, int Body, int Legs, int Feet, int Addons);

/// <summary>DoT/debuff payload of an attack (canary `condition` field).</summary>
public sealed record MonsterCondition(string Type, int TotalDamage, int TickMs, int DurationMs);

/// <summary>A creature the monster can summon (canary `monster.summon` block).</summary>
public sealed record MonsterSummon(string Name, int Chance, int IntervalMs, int Count);

/// <summary>Reactive kit from the canary `defenses` array: self-heal or self-haste.</summary>
public sealed record MonsterDefense(
    string Kind, int IntervalMs, int Chance, int MinValue, int MaxValue,
    int SpeedChange, int DurationMs, int AreaEffect);

public sealed record MonsterAttack(
    string Kind, string Label, int Interval, int Chance, int Range, int Radius,
    int Length, int Spread, bool Target, int MinDamage, int MaxDamage,
    string DamageType, int ShootEffect, int AreaEffect,
    MonsterCondition? Condition = null, int SpeedChange = 0, int DurationMs = 0,
    bool IsHealing = false);

public sealed record MonsterLoot(int ItemId, string Name, int Chance, int MaxCount);
public sealed record ItemType(
    int ItemId,
    string Name,
    int SalePrice,
    string? Slot = null,
    string? WeaponType = null,
    int Attack = 0,
    int Armor = 0,
    int Defense = 0,
    int MountLookType = 0,
    int MountSpeed = 0,
    string Description = "",
    int SourceItemId = 0,
    bool IsAuthored = false,
    string Element = "physical",
    int ElementDamage = 0,
    int SkillPower = 0,
    double CritChance = 0,
    double CritDamage = 0,
    double LifeStealChance = 0,
    double LifeStealAmount = 0,
    double CooldownReduction = 0,
    double MoveSpeedPercent = 0,
    double PhysicalResistance = 0,
    double FireResistance = 0,
    double IceResistance = 0,
    double EarthResistance = 0,
    double EnergyResistance = 0,
    double DeathResistance = 0,
    double HolyResistance = 0,
    IReadOnlyList<string>? AllowedClassIds = null,
    int RequiredMasteryPoints = 0,
    int Tier = 0,
    string Tag = "normal",
    double StatMultiplier = 1)
{
    public int AppearanceItemId => SourceItemId != 0 ? SourceItemId : ItemId;

    public double Resistance(string damageType) => damageType.ToLowerInvariant() switch
    {
        "physical" => PhysicalResistance,
        "fire" => FireResistance,
        "ice" => IceResistance,
        "earth" => EarthResistance,
        "energy" => EnergyResistance,
        "death" => DeathResistance,
        "holy" => HolyResistance,
        _ => 0
    };
}

public sealed record MonsterType(
    string Name, string Description, int Experience, int Health, int Speed, int Corpse,
    MonsterOutfit Outfit, int TargetDistance, int StaticAttackChance, int RunOnHealth,
    int Armor, int Defense, double Mitigation, bool IsBoss, string BestiaryClass,
    List<MonsterAttack> Attacks, Dictionary<string, double> Elements,
    List<MonsterLoot> Loot, List<string> Voices,
    int MaxSummons = 0, List<MonsterSummon>? Summons = null, List<MonsterDefense>? Defenses = null,
    string? Origin = null, string? BossRace = null,
    string? Id = null, string Rank = "legacy", string Element = "physical",
    string BehaviorId = "legacy", string StatPresetId = "legacy",
    double HpMultiplier = 1, double DamageMultiplier = 1,
    double SpeedMultiplier = 1, double CadenceMultiplier = 1,
    int PowerTier = 0, bool IsAuthored = false,
    // G-08B: card keyword resistance (sin/curse/burn/charge/frost/posture/combo/prey).
    Dictionary<string, double>? KeywordResistances = null)
{
    public List<MonsterSummon> Summons { get; init; } = Summons ?? [];
    public List<MonsterDefense> Defenses { get; init; } = Defenses ?? [];
    public Dictionary<string, double> KeywordResistances { get; init; } = KeywordResistances ?? [];
    public string StableId => string.IsNullOrWhiteSpace(Id) ? Name : Id;
}

public sealed class GameData
{
    public IReadOnlyDictionary<string, MonsterType> Monsters { get; }
    public IReadOnlyDictionary<int, ItemType> Items { get; }
    public IReadOnlyList<MonsterAppearance> MonsterAppearances { get; }

    private readonly HashSet<int> _foodIds;

    public GameData(IWebHostEnvironment env)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dataPath = Path.Combine(env.ContentRootPath, "Data");
        var monsters = JsonSerializer.Deserialize<List<MonsterType>>(
                           File.ReadAllText(Path.Combine(dataPath, "monsters.json")), options)
                       ?? throw new InvalidOperationException("monsters.json missing or invalid");
        var items = JsonSerializer.Deserialize<List<ItemType>>(
                        File.ReadAllText(Path.Combine(dataPath, "items.json")), options)
                    ?? throw new InvalidOperationException("items.json missing or invalid");
        Monsters = monsters.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        Items = items.ToDictionary(i => i.ItemId);
        var appearancesPath = Path.Combine(dataPath, "monster-appearances.json");
        MonsterAppearances = File.Exists(appearancesPath)
            ? JsonSerializer.Deserialize<List<MonsterAppearance>>(File.ReadAllText(appearancesPath), options) ?? []
            : monsters.Select(m => new MonsterAppearance(
                $"legacy:{m.StableId}",
                m.Name,
                "monsters.json",
                m.Outfit,
                m.Corpse,
                m.BestiaryClass,
                "legacy",
                m.IsBoss ? "boss" : "normal",
                "legacy",
                true)).ToList();

        // Resolve food from keywords; word matching prevents "legs" from becoming food.
        var foodWords = GameConfig.FoodNameWords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _foodIds = Items.Values
            .Where(i => i.Name.Split(' ').Any(foodWords.Contains))
            .Select(i => i.ItemId)
            .ToHashSet();
    }

    public MonsterType Get(string name) => Monsters[name];
    public ItemType GetItem(int itemId) => Items[itemId];

    /// <summary>
    /// True only for items that fit a slot the game still uses. Raw tibia loot tables are full
    /// of food/junk and gear for slots we removed (legs/feet) — those must never hit the bag.
    /// Curated per-tier set drops will replace this source entirely (sets phase).
    /// </summary>
    public bool IsEquippableLoot(int itemId) =>
        Items.TryGetValue(itemId, out var item)
        && item.Slot is not null
        && EquipmentSlots.IsValid(item.Slot);

    /// <summary>Food: heals a small amount on pickup.</summary>
    public bool IsFood(int itemId) => _foodIds.Contains(itemId);

    /// <summary>
    /// Fraction of max HP healed by a health potion on pickup; 0 for non-health potions.
    /// Stronger potions that drop in higher tiers heal more.
    /// </summary>
    public double PotionHealFraction(int itemId)
    {
        if (!Items.TryGetValue(itemId, out var item)) return 0;
        var name = item.Name.ToLowerInvariant();
        if (!name.Contains("health potion")) return 0;
        if (name.Contains("ultimate") || name.Contains("supreme") || name.Contains("great"))
            return GameConfig.PotionHealGreat;
        if (name.Contains("strong")) return GameConfig.PotionHealStrong;
        return GameConfig.PotionHealBasic;
    }
    public int ItemValue(int itemId) =>
        Items.TryGetValue(itemId, out var item) && item.SalePrice > 0
            ? item.SalePrice
            : GameConfig.ItemFallbackSalePrice;
}
