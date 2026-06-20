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
    private readonly string _authoredItemsPath;
    private readonly object _lock = new();
    private List<DungeonTier> _tiers;
    private List<MonsterDefinition> _monsters;
    private List<KaeliSkinDefinition> _kaeliSkins;
    private List<AuthoredItemDefinition> _authoredItems;

    public ContentStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, ".data", "content");
        Directory.CreateDirectory(dir);
        _tiersPath = Path.Combine(dir, "tiers.json");
        _monstersPath = Path.Combine(dir, "monsters.json");
        _kaeliSkinsPath = Path.Combine(dir, "kaeli-skins.json");
        _authoredItemsPath = Path.Combine(dir, "authored-items.json");
        _tiers = LoadTiers();
        _monsters = LoadMonsters();
        _kaeliSkins = LoadKaeliSkins();
        _authoredItems = LoadAuthoredItems();
    }

    private List<DungeonTier> LoadTiers()
    {
        if (File.Exists(_tiersPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<DungeonTier>>(
                    File.ReadAllText(_tiersPath), JsonOpts);
                if (loaded is { Count: > 0 } && !ShouldSeedTiers(loaded)) return loaded;
            }
            catch (JsonException)
            {
                // arquivo corrompido: cai pro seed dos defaults em vez de derrubar o boot
            }
        }

        var seed = KaezanContentSeed.Tiers.ToList();
        WriteTiers(seed);
        return seed;
    }

    private void WriteTiers(List<DungeonTier> tiers) =>
        File.WriteAllText(_tiersPath, JsonSerializer.Serialize(tiers, JsonOpts));

    private List<MonsterDefinition> LoadMonsters()
    {
        if (!File.Exists(_monstersPath))
        {
            var seed = KaezanContentSeed.Monsters.ToList();
            WriteMonsters(seed);
            return seed;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<List<MonsterDefinition>>(
                       File.ReadAllText(_monstersPath), JsonOpts)
                   ?? [];
            if (!ShouldSeedMonsters(loaded)) return loaded;
            var seed = KaezanContentSeed.Monsters.ToList();
            WriteMonsters(seed);
            return seed;
        }
        catch (JsonException)
        {
            var seed = KaezanContentSeed.Monsters.ToList();
            WriteMonsters(seed);
            return seed;
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

    private List<AuthoredItemDefinition> LoadAuthoredItems()
    {
        if (!File.Exists(_authoredItemsPath))
        {
            var seed = NormalizeAuthoredItems(KaezanContentSeed.AuthoredItems);
            WriteAuthoredItems(seed);
            return seed;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<List<AuthoredItemDefinition>>(
                   File.ReadAllText(_authoredItemsPath), JsonOpts)
                   ?? [];
            if (ShouldSeedItems(loaded))
            {
                var seed = NormalizeAuthoredItems(KaezanContentSeed.AuthoredItems);
                WriteAuthoredItems(seed);
                return seed;
            }

            var normalized = MergeMissingSeedRelics(NormalizeAuthoredItems(loaded));
            WriteAuthoredItems(normalized);
            return normalized;
        }
        catch (JsonException)
        {
            var seed = NormalizeAuthoredItems(KaezanContentSeed.AuthoredItems);
            WriteAuthoredItems(seed);
            return seed;
        }
    }

    private static List<AuthoredItemDefinition> NormalizeAuthoredItems(IEnumerable<AuthoredItemDefinition> items) =>
        items.Select(item => ItemAuthoring.Normalize(item, item.ItemId)).ToList();

    private static List<AuthoredItemDefinition> MergeMissingSeedRelics(List<AuthoredItemDefinition> items)
    {
        var existingIds = items.Select(item => item.ItemId).ToHashSet();
        var missingRelics = KaezanContentSeed.AuthoredItems
            .Where(item => item.Tag == GameConfig.AuthoredItemTagRelic && !existingIds.Contains(item.ItemId));
        items.AddRange(missingRelics);
        return items;
    }

    private void WriteAuthoredItems(IEnumerable<AuthoredItemDefinition> items) =>
        File.WriteAllText(_authoredItemsPath,
            JsonSerializer.Serialize(items.OrderBy(item => item.ItemId).ToList(), JsonOpts));

    private static bool ShouldSeedTiers(IReadOnlyList<DungeonTier> tiers) =>
        tiers.Count != KaezanContentSeed.Tiers.Length
        || tiers.Any(tier =>
            tier.CommonMobs.Concat(tier.EliteMobs).Append(tier.Boss)
                .Any(reference => !reference.StartsWith("monster:", StringComparison.OrdinalIgnoreCase)));

    private static bool ShouldSeedMonsters(IReadOnlyList<MonsterDefinition> monsters) =>
        monsters.Count == 0
        || monsters.All(monster =>
            monster.Id.Equals("monster:achad-echo", StringComparison.OrdinalIgnoreCase));

    private static bool ShouldSeedItems(IReadOnlyList<AuthoredItemDefinition> items) =>
        items.Count == 0
        || items.All(item => item.ItemId is 900000 or 900001 or 900002);

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
    /// Reordena as skins autorais de uma Kaeli na ordem dos ids recebidos, preservando a posição
    /// relativa das skins das outras Kaelis (o <see cref="KaeliRegistry"/> anexa as autorais na
    /// ordem persistida, então isso controla a ordem exibida no guarda-roupa/seletor de skin).
    /// Ids desconhecidos são ignorados; skins omitidas mantêm a ordem atual ao final.
    /// </summary>
    public IReadOnlyList<KaeliSkinDefinition> ReorderKaeliSkins(string waifuId, IReadOnlyList<string> orderedIds)
    {
        var owner = waifuId.Trim().ToLowerInvariant();
        lock (_lock)
        {
            bool Owns(KaeliSkinDefinition s) => s.WaifuId.Equals(owner, StringComparison.OrdinalIgnoreCase);
            var mine = _kaeliSkins.Where(Owns).ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<KaeliSkinDefinition>();
            foreach (var id in orderedIds)
                if (mine.Remove(id, out var skin)) ordered.Add(skin);
            // skins não citadas preservam a ordem persistida original
            ordered.AddRange(_kaeliSkins.Where(s => Owns(s) && mine.ContainsKey(s.Id)));

            var queue = new Queue<KaeliSkinDefinition>(ordered);
            for (var i = 0; i < _kaeliSkins.Count; i++)
                if (Owns(_kaeliSkins[i])) _kaeliSkins[i] = queue.Dequeue();
            WriteKaeliSkins(_kaeliSkins);
            return _kaeliSkins.ToList();
        }
    }

    // ---- itens autorais (Item Studio) ----

    public IReadOnlyList<AuthoredItemDefinition> AuthoredItems
    {
        get { lock (_lock) return _authoredItems.ToList(); }
    }

    /// <summary>Cria um item Kaezan com ID reservado, mantendo a fonte Canary imutavel.</summary>
    public AuthoredItemDefinition CreateAuthoredItem(AuthoredItemDefinition definition)
    {
        lock (_lock)
        {
            var nextId = _authoredItems.Count == 0
                ? GameConfig.AuthoredItemIdBase
                : Math.Max(GameConfig.AuthoredItemIdBase, _authoredItems.Max(item => item.ItemId) + 1);
            var created = ItemAuthoring.Normalize(definition with { ItemId = nextId }, nextId);
            _authoredItems.Add(created);
            WriteAuthoredItems(_authoredItems);
            return created;
        }
    }

    public AuthoredItemDefinition UpdateAuthoredItem(int itemId, AuthoredItemDefinition definition)
    {
        lock (_lock)
        {
            var index = _authoredItems.FindIndex(item => item.ItemId == itemId);
            if (index < 0) throw new KeyNotFoundException($"item autoral desconhecido: {itemId}");
            var updated = ItemAuthoring.Normalize(definition with { ItemId = itemId }, itemId);
            _authoredItems[index] = updated;
            WriteAuthoredItems(_authoredItems);
            return updated;
        }
    }

    public AuthoredItemDefinition DeleteAuthoredItem(int itemId)
    {
        lock (_lock)
        {
            var index = _authoredItems.FindIndex(item => item.ItemId == itemId);
            if (index < 0) throw new KeyNotFoundException($"item autoral desconhecido: {itemId}");
            var removed = _authoredItems[index];
            _authoredItems.RemoveAt(index);
            WriteAuthoredItems(_authoredItems);
            return removed;
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
