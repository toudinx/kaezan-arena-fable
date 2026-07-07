using KaezanArenaFable.Api.Content;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Single lookup surface during the migration: authored IDs take precedence while Canary
/// species remain available by their legacy names as read-only placeholders.
/// </summary>
public sealed class MonsterRegistry(GameData legacy, ContentStore content)
{
    public IReadOnlyList<MonsterType> All =>
        legacy.Monsters.Values
            .Concat(content.Monsters.Where(m => m.Enabled).Select(MonsterAuthoring.Resolve))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool TryGet(string reference, out MonsterType monster)
    {
        MonsterDefinition? authored = content.Monsters.FirstOrDefault(m =>
            m.Enabled && (m.Id.Equals(reference, StringComparison.OrdinalIgnoreCase)
                          || m.Name.Equals(reference, StringComparison.OrdinalIgnoreCase)));
        if (authored is not null)
        {
            monster = MonsterAuthoring.Resolve(authored);
            return true;
        }

        return legacy.Monsters.TryGetValue(reference, out monster!);
    }

    public MonsterType Get(string reference) =>
        TryGet(reference, out MonsterType monster)
            ? monster
            : throw new KeyNotFoundException($"unknown monster: {reference}");

    public bool Contains(string reference) => TryGet(reference, out _);

    public bool Has(string name) => TryGet(name, out _);
}
