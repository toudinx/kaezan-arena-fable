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
public sealed record ItemType(int ItemId, string Name, int SalePrice);

public sealed record MonsterType(
    string Name, string Description, int Experience, int Health, int Speed, int Corpse,
    MonsterOutfit Outfit, int TargetDistance, int StaticAttackChance, int RunOnHealth,
    int Armor, int Defense, double Mitigation, bool IsBoss, string BestiaryClass,
    List<MonsterAttack> Attacks, Dictionary<string, double> Elements,
    List<MonsterLoot> Loot, List<string> Voices,
    int MaxSummons = 0, List<MonsterSummon>? Summons = null, List<MonsterDefense>? Defenses = null)
{
    public List<MonsterSummon> Summons { get; init; } = Summons ?? [];
    public List<MonsterDefense> Defenses { get; init; } = Defenses ?? [];
}

public sealed class GameData
{
    public IReadOnlyDictionary<string, MonsterType> Monsters { get; }
    public IReadOnlyDictionary<int, ItemType> Items { get; }

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
    }

    public MonsterType Get(string name) => Monsters[name];
    public int ItemValue(int itemId) =>
        Items.TryGetValue(itemId, out var item) && item.SalePrice > 0
            ? item.SalePrice
            : GameConfig.ItemFallbackSalePrice;
}
