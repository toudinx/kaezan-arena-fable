using System.Text.Json;

namespace KaezanArenaFable.Api.Domain;

// ---- monster data converted from Canary .lua (tools/convert-monsters) ----

public sealed record MonsterOutfit(int LookType, int Head, int Body, int Legs, int Feet, int Addons);

public sealed record MonsterAttack(
    string Kind, string Label, int Interval, int Chance, int Range, int Radius,
    int Length, int Spread, bool Target, int MinDamage, int MaxDamage,
    string DamageType, int ShootEffect, int AreaEffect);

public sealed record MonsterLoot(int ItemId, string Name, int Chance, int MaxCount);

public sealed record MonsterType(
    string Name, string Description, int Experience, int Health, int Speed, int Corpse,
    MonsterOutfit Outfit, int TargetDistance, int StaticAttackChance, int RunOnHealth,
    int Armor, int Defense, double Mitigation, bool IsBoss, string BestiaryClass,
    List<MonsterAttack> Attacks, Dictionary<string, double> Elements,
    List<MonsterLoot> Loot, List<string> Voices);

public sealed class GameData
{
    public IReadOnlyDictionary<string, MonsterType> Monsters { get; }

    public GameData(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "monsters.json");
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = JsonSerializer.Deserialize<List<MonsterType>>(json, options)
                   ?? throw new InvalidOperationException("monsters.json missing or invalid");
        Monsters = list.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
    }

    public MonsterType Get(string name) => Monsters[name];
}
