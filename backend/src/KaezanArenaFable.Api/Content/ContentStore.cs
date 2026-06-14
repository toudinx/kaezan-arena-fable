using System.Text.Json;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Content;

/// <summary>
/// Conteúdo de jogo EDITÁVEL pelo painel admin (tiers, monstros autorais e skins de Kaeli).
/// Mora em JSON gravável (`.data/content/`), seedado a partir dos defaults em código
/// (<see cref="GameConfig.Tiers"/>) na primeira execução. Diferente de <see cref="GameData"/>,
/// que é conteúdo somente-leitura (monsters.json/items.json), e de AccountStore, que é estado de
/// conta. Singleton: runs novas leem daqui, então editar + salvar afeta a próxima run.
/// </summary>
public sealed class ContentStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _tiersPath;
    private readonly string _monstersPath;
    private readonly string _kaeliSkinsPath;
    private readonly object _lock = new();
    private List<DungeonTier> _tiers;
    private List<MonsterDefinition> _monsters;
    private List<KaeliSkinDefinition> _kaeliSkins;

    public ContentStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, ".data", "content");
        Directory.CreateDirectory(dir);
        _tiersPath = Path.Combine(dir, "tiers.json");
        _monstersPath = Path.Combine(dir, "monsters.json");
        _kaeliSkinsPath = Path.Combine(dir, "kaeli-skins.json");
        _tiers = LoadTiers();
        _monsters = LoadMonsters();
        _kaeliSkins = LoadKaeliSkins();
    }

    private List<DungeonTier> LoadTiers()
    {
        if (File.Exists(_tiersPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<DungeonTier>>(
                    File.ReadAllText(_tiersPath), JsonOpts);
                if (loaded is { Count: > 0 }) return loaded;
            }
            catch (JsonException)
            {
                // arquivo corrompido: cai pro seed dos defaults em vez de derrubar o boot
            }
        }

        var seed = GameConfig.Tiers.ToList();
        WriteTiers(seed);
        return seed;
    }

    private void WriteTiers(List<DungeonTier> tiers) =>
        File.WriteAllText(_tiersPath, JsonSerializer.Serialize(tiers, JsonOpts));

    private List<MonsterDefinition> LoadMonsters()
    {
        if (!File.Exists(_monstersPath))
        {
            WriteMonsters([]);
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<MonsterDefinition>>(
                       File.ReadAllText(_monstersPath), JsonOpts)
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void WriteMonsters(List<MonsterDefinition> monsters) =>
        File.WriteAllText(_monstersPath, JsonSerializer.Serialize(monsters, JsonOpts));

    private List<KaeliSkinDefinition> LoadKaeliSkins()
    {
        if (!File.Exists(_kaeliSkinsPath))
        {
            WriteKaeliSkins([]);
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<KaeliSkinDefinition>>(
                       File.ReadAllText(_kaeliSkinsPath), JsonOpts)
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void WriteKaeliSkins(List<KaeliSkinDefinition> skins) =>
        File.WriteAllText(_kaeliSkinsPath, JsonSerializer.Serialize(skins, JsonOpts));

    public IReadOnlyList<DungeonTier> Tiers
    {
        get { lock (_lock) return _tiers.ToList(); }
    }

    public DungeonTier? Tier(int tier)
    {
        lock (_lock) return _tiers.FirstOrDefault(t => t.Tier == tier);
    }

    public IReadOnlyList<MonsterDefinition> Monsters
    {
        get { lock (_lock) return _monsters.ToList(); }
    }

    public MonsterDefinition CreateMonster(MonsterDefinition definition)
    {
        lock (_lock)
        {
            if (_monsters.Any(m => m.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"id ja existe: {definition.Id}");
            _monsters.Add(definition);
            WriteMonsters(_monsters);
            return definition;
        }
    }

    public MonsterDefinition UpdateMonster(string id, MonsterDefinition definition)
    {
        lock (_lock)
        {
            var index = _monsters.FindIndex(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"monstro autoral desconhecido: {id}");
            _monsters[index] = definition;
            WriteMonsters(_monsters);
            return definition;
        }
    }

    public MonsterDefinition DeleteMonster(string id)
    {
        lock (_lock)
        {
            var index = _monsters.FindIndex(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"monstro autoral desconhecido: {id}");

            var monster = _monsters[index];
            var references = _tiers
                .Where(tier =>
                    tier.CommonMobs.Any(reference => Matches(reference, monster))
                    || tier.EliteMobs.Any(reference => Matches(reference, monster))
                    || Matches(tier.Boss, monster))
                .Select(tier => $"Tier {tier.Tier} ({tier.Name})")
                .ToArray();
            if (references.Length > 0)
                throw new InvalidOperationException(
                    $"remova '{monster.Name}' das dungeons antes de excluir: {string.Join(", ", references)}");

            _monsters.RemoveAt(index);
            WriteMonsters(_monsters);
            return monster;
        }
    }

    private static bool Matches(string reference, MonsterDefinition monster) =>
        reference.Equals(monster.Id, StringComparison.OrdinalIgnoreCase)
        || reference.Equals(monster.Name, StringComparison.OrdinalIgnoreCase);

    // ---- Kaeli skins (Outfit Studio) ----

    public IReadOnlyList<KaeliSkinDefinition> AuthoredKaeliSkins
    {
        get { lock (_lock) return _kaeliSkins.ToList(); }
    }

    public KaeliSkinDefinition CreateKaeliSkin(KaeliSkinDefinition definition)
    {
        lock (_lock)
        {
            if (_kaeliSkins.Any(s => s.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"id ja existe: {definition.Id}");
            _kaeliSkins.Add(definition);
            WriteKaeliSkins(_kaeliSkins);
            return definition;
        }
    }

    public KaeliSkinDefinition UpdateKaeliSkin(string id, KaeliSkinDefinition definition)
    {
        lock (_lock)
        {
            var index = _kaeliSkins.FindIndex(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"skin autoral desconhecida: {id}");
            _kaeliSkins[index] = definition;
            WriteKaeliSkins(_kaeliSkins);
            return definition;
        }
    }

    public KaeliSkinDefinition DeleteKaeliSkin(string id)
    {
        lock (_lock)
        {
            var index = _kaeliSkins.FindIndex(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"skin autoral desconhecida: {id}");
            var skin = _kaeliSkins[index];
            _kaeliSkins.RemoveAt(index);
            WriteKaeliSkins(_kaeliSkins);
            return skin;
        }
    }

    /// <summary>
    /// Substitui o conjunto inteiro de tiers (o editor envia os 5 de uma vez) e persiste.
    /// A validação de conteúdo (mobs/boss existem) é feita no endpoint, que tem o GameData.
    /// </summary>
    public IReadOnlyList<DungeonTier> ReplaceTiers(IEnumerable<DungeonTier> tiers)
    {
        var next = tiers.OrderBy(t => t.Tier).ToList();
        lock (_lock)
        {
            _tiers = next;
            WriteTiers(_tiers);
            return _tiers.ToList();
        }
    }
}
