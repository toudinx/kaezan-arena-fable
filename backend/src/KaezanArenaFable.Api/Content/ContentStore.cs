using System.Text.Json;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Content;

/// <summary>
/// Game content EDITABLE via the admin panel (tiers, authored monsters and Kaeli skins).
/// Lives in writable JSON (`.data/content/`), seeded from code defaults
/// (<see cref="GameConfig.Tiers"/>) on first run. Unlike <see cref="GameData"/>,
/// which is read-only content (monsters.json/items.json), and AccountStore, which is account state.
/// Singleton: new runs read from here, so editing + saving affects the next run.
/// </summary>
public sealed class ContentStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _tiersPath;
    private readonly string _biomesPath;
    private readonly string _monstersPath;
    private readonly string _kaeliSkinsPath;
    private readonly string _authoredItemsPath;
    private readonly string _bannersPath;
    private readonly string _roleTuningPath;
    private readonly object _lock = new();
    private List<DungeonTier> _tiers;
    private List<BiomeRow> _biomes;
    private List<MonsterDefinition> _monsters;
    private List<KaeliSkinDefinition> _kaeliSkins;
    private List<AuthoredItemDefinition> _authoredItems;
    private List<string> _activeBannerWaifuIds;
    private Dictionary<KaeliRole, RoleTuning> _roleTunings;

    public ContentStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, ".data", "content");
        Directory.CreateDirectory(dir);
        _tiersPath = Path.Combine(dir, "tiers.json");
        _biomesPath = Path.Combine(dir, "biomes.json");
        _monstersPath = Path.Combine(dir, "monsters.json");
        _kaeliSkinsPath = Path.Combine(dir, "kaeli-skins.json");
        _authoredItemsPath = Path.Combine(dir, "authored-items.json");
        _bannersPath = Path.Combine(dir, "banners.json");
        _roleTuningPath = Path.Combine(dir, "role-tuning.json");
        _tiers = LoadTiers();
        _biomes = LoadBiomes();
        _monsters = LoadMonsters();
        _kaeliSkins = LoadKaeliSkins();
        _authoredItems = LoadAuthoredItems();
        _activeBannerWaifuIds = LoadActiveBanners();
        _roleTunings = LoadRoleTunings();
    }

    private List<DungeonTier> LoadTiers()
    {
        if (File.Exists(_tiersPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<DungeonTier>>(
                    File.ReadAllText(_tiersPath), JsonOpts);
                if (loaded is { Count: > 0 } && !ShouldSeedTiers(loaded))
                {
                    var (tiers, changed) = AlignSeedTierDisplay(loaded);
                    if (changed) WriteTiers(tiers);
                    return tiers;
                }
            }
            catch (JsonException)
            {
                // corrupted file: fall back to seeding defaults instead of crashing on boot
            }
        }

        var seed = KaezanContentSeed.Tiers.ToList();
        WriteTiers(seed);
        return seed;
    }

    private void WriteTiers(List<DungeonTier> tiers) =>
        File.WriteAllText(_tiersPath, JsonSerializer.Serialize(tiers, JsonOpts));

    // ---- LM-08: data-driven biomes (same pattern as tiers; canonical defaults in Domain.Biomes) ----

    private List<BiomeRow> LoadBiomes()
    {
        if (File.Exists(_biomesPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<BiomeRow>>(
                    File.ReadAllText(_biomesPath), JsonOpts);
                if (loaded is not null && !ShouldSeedBiomes(loaded)) return loaded;
            }
            catch (JsonException)
            {
                // corrupted file: fall back to seeding defaults instead of crashing on boot
            }
        }

        var seed = KaezanContentSeed.Biomes.ToList();
        WriteBiomes(seed);
        return seed;
    }

    private void WriteBiomes(List<BiomeRow> biomes) =>
        File.WriteAllText(_biomesPath, JsonSerializer.Serialize(biomes, JsonOpts));

    /// <summary>Re-seed when the file does not contain exactly the 5 strata (1–5) — defaults are canonical.</summary>
    private static bool ShouldSeedBiomes(IReadOnlyList<BiomeRow> biomes) =>
        biomes.Count != KaezanContentSeed.Biomes.Count
        || biomes.Select(b => b.Tier).OrderBy(t => t).SequenceEqual([1, 2, 3, 4, 5]) == false;

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
            if (ShouldSeedMonsters(loaded))
            {
                var seed = KaezanContentSeed.Monsters.ToList();
                WriteMonsters(seed);
                return seed;
            }
            // G-08B: add-only — new seed monsters (e.g. signature creatures) merge in without clobbering edits.
            var merged = AlignSeedMonsterDisplay(MergeMissingSeedMonsters(loaded));
            WriteMonsters(merged);
            return merged;
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

    /// <summary>G-08B: appends seed monsters whose id does not yet exist in the file (add-only, preserves edits).</summary>
    private static List<MonsterDefinition> MergeMissingSeedMonsters(List<MonsterDefinition> monsters)
    {
        var existing = monsters.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        monsters.AddRange(KaezanContentSeed.Monsters.Where(m => !existing.Contains(m.Id)));
        return monsters;
    }

    private static (List<DungeonTier> Tiers, bool Changed) AlignSeedTierDisplay(List<DungeonTier> tiers)
    {
        var changed = false;
        var seedByTier = KaezanContentSeed.Tiers.ToDictionary(t => t.Tier);
        for (var i = 0; i < tiers.Count; i++)
        {
            var current = tiers[i];
            if (!seedByTier.TryGetValue(current.Tier, out var seed)) continue;
            if (!SameTierReferences(current, seed)) continue;
            if (current.Name == seed.Name && current.Description == seed.Description) continue;
            tiers[i] = current with { Name = seed.Name, Description = seed.Description };
            changed = true;
        }
        return (tiers, changed);
    }

    private static bool SameTierReferences(DungeonTier current, DungeonTier seed) =>
        current.Boss.Equals(seed.Boss, StringComparison.OrdinalIgnoreCase)
        && current.CommonMobs.SequenceEqual(seed.CommonMobs, StringComparer.OrdinalIgnoreCase)
        && current.EliteMobs.SequenceEqual(seed.EliteMobs, StringComparer.OrdinalIgnoreCase);

    private static List<MonsterDefinition> AlignSeedMonsterDisplay(List<MonsterDefinition> monsters)
    {
        var seedById = KaezanContentSeed.Monsters.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < monsters.Count; i++)
        {
            var current = monsters[i];
            if (!seedById.TryGetValue(current.Id, out var seed)) continue;
            if (!LooksLikeLegacySeedDisplay(current)) continue;
            monsters[i] = current with { Name = seed.Name, Description = seed.Description };
        }
        return monsters;
    }

    private static bool LooksLikeLegacySeedDisplay(MonsterDefinition monster) =>
        monster.Description.StartsWith("Criatura Kaezan ", StringComparison.OrdinalIgnoreCase);

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

    private static bool ShouldSeedTiers(IReadOnlyList<DungeonTier> tiers)
    {
        if (tiers.Count != KaezanContentSeed.Tiers.Length) return true;
        if (tiers.Any(tier =>
            tier.CommonMobs.Concat(tier.EliteMobs).Append(tier.Boss)
                .Any(reference => !reference.StartsWith("monster:", StringComparison.OrdinalIgnoreCase))))
            return true;

        // G-08B: re-seed when the seed brings new mobs (signature creatures) not yet in the persisted pools
        // — keeps new archetypes alive in runs without requiring a manual .data wipe.
        var persisted = tiers
            .SelectMany(tier => tier.CommonMobs.Concat(tier.EliteMobs).Append(tier.Boss))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seeded = KaezanContentSeed.Tiers
            .SelectMany(tier => tier.CommonMobs.Concat(tier.EliteMobs).Append(tier.Boss));
        return seeded.Any(reference => !persisted.Contains(reference));
    }

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

    // ---- LM-08: biomes (the Hub resolves the run biome from here; admin reads/edits via the 3 LM-09 endpoints) ----

    public IReadOnlyList<BiomeRow> Biomes
    {
        get { lock (_lock) return _biomes.ToList(); }
    }

    /// <summary>The current <see cref="BiomeDef"/> for the tier, or null if absent (the Hub falls back to Biomes.ForTier).</summary>
    public BiomeDef? Biome(int tier)
    {
        lock (_lock) return _biomes.FirstOrDefault(b => b.Tier == tier)?.Def;
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
                throw new InvalidOperationException($"id already exists: {definition.Id}");
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
            if (index < 0) throw new KeyNotFoundException($"unknown authored monster: {id}");
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
            if (index < 0) throw new KeyNotFoundException($"unknown authored monster: {id}");

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
                    $"remove '{monster.Name}' from dungeons before deleting: {string.Join(", ", references)}");

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
                throw new InvalidOperationException($"id already exists: {definition.Id}");
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
            if (index < 0) throw new KeyNotFoundException($"unknown authored skin: {id}");
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
            if (index < 0) throw new KeyNotFoundException($"unknown authored skin: {id}");
            var skin = _kaeliSkins[index];
            _kaeliSkins.RemoveAt(index);
            WriteKaeliSkins(_kaeliSkins);
            return skin;
        }
    }

    /// <summary>
    /// Reorders a Kaeli's authored skins to match the received id order, preserving the relative
    /// position of other Kaelis' skins (the <see cref="KaeliRegistry"/> appends authored skins in
    /// persisted order, so this controls the display order in the wardrobe/skin selector).
    /// Unknown ids are ignored; omitted skins retain their current order at the end.
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
            // omitted skins preserve the original persisted order
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

    /// <summary>Creates a Kaezan item with a reserved ID, keeping the Canary source immutable.</summary>
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
            if (index < 0) throw new KeyNotFoundException($"unknown authored item: {itemId}");
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
            if (index < 0) throw new KeyNotFoundException($"unknown authored item: {itemId}");
            var removed = _authoredItems[index];
            _authoredItems.RemoveAt(index);
            WriteAuthoredItems(_authoredItems);
            return removed;
        }
    }

    // ---- banners ativos (gacha) ----

    private List<string> LoadActiveBanners()
    {
        if (File.Exists(_bannersPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_bannersPath), JsonOpts);
                if (loaded is { Count: > 0 }) return loaded;
            }
            catch (JsonException) { }
        }
        var seed = new List<string> { Domain.Waifus.FeaturedFiveStarId };
        WriteActiveBanners(seed);
        return seed;
    }

    private void WriteActiveBanners(List<string> ids) =>
        File.WriteAllText(_bannersPath, JsonSerializer.Serialize(ids, JsonOpts));

    public IReadOnlyList<string> ActiveBannerWaifuIds
    {
        get { lock (_lock) return _activeBannerWaifuIds.ToList(); }
    }

    public IReadOnlyList<string> SetActiveBanners(IReadOnlyList<string> waifuIds)
    {
        var next = waifuIds.Distinct().ToList();
        lock (_lock)
        {
            _activeBannerWaifuIds = next;
            WriteActiveBanners(_activeBannerWaifuIds);
            return _activeBannerWaifuIds.ToList();
        }
    }

    // ---- MG-05: role tuning (RoleTuning editable in admin) ----

    private Dictionary<KaeliRole, RoleTuning> LoadRoleTunings()
    {
        if (File.Exists(_roleTuningPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<RoleTuningRow>>(
                    File.ReadAllText(_roleTuningPath), JsonOpts);
                if (ParseRoleTuningRows(loaded) is { } parsed) return parsed;
            }
            catch (JsonException)
            {
                // corrupted file: fall back to seeding defaults instead of crashing on boot
            }
        }

        var seed = new Dictionary<KaeliRole, RoleTuning>(GameConfig.Roles);
        WriteRoleTunings(seed);
        return seed;
    }

    private void WriteRoleTunings(IReadOnlyDictionary<KaeliRole, RoleTuning> tunings) =>
        File.WriteAllText(_roleTuningPath, JsonSerializer.Serialize(ToRoleTuningRows(tunings), JsonOpts));

    /// <summary>
    /// Converts serialized rows into the typed dictionary. An unknown role invalidates everything
    /// (returns null → reseed); a missing role is filled from the code default so a run never
    /// encounters a role without tuning.
    /// </summary>
    private static Dictionary<KaeliRole, RoleTuning>? ParseRoleTuningRows(List<RoleTuningRow>? rows)
    {
        if (rows is null || rows.Count == 0) return null;
        var result = new Dictionary<KaeliRole, RoleTuning>();
        foreach (var row in rows)
        {
            if (!Enum.TryParse<KaeliRole>(row.Role, ignoreCase: true, out var role)) return null;
            result[role] = new RoleTuning(
                row.AutoDmgMult, row.SkillDmgMult, row.BaseAutoAttackMs, row.AutoRange, row.AoeScale);
        }
        foreach (var (role, tuning) in GameConfig.Roles)
            result.TryAdd(role, tuning);
        return result;
    }

    /// <summary>Rows in stable role order (the defaults order), readable in JSON/admin.</summary>
    private static List<RoleTuningRow> ToRoleTuningRows(IReadOnlyDictionary<KaeliRole, RoleTuning> tunings) =>
        GameConfig.Roles.Keys
            .Where(tunings.ContainsKey)
            .Select(role => new RoleTuningRow(
                role.ToString(),
                tunings[role].AutoDmgMult, tunings[role].SkillDmgMult,
                tunings[role].BaseAutoAttackMs, tunings[role].AutoRange, tunings[role].AoeScale))
            .ToList();

    /// <summary>Current table for the run (the Hub injects this into <see cref="GameWorld"/>).</summary>
    public IReadOnlyDictionary<KaeliRole, RoleTuning> RoleTunings
    {
        get { lock (_lock) return new Dictionary<KaeliRole, RoleTuning>(_roleTunings); }
    }

    /// <summary>Rows for the admin editor (GET /admin/content/role-tuning).</summary>
    public IReadOnlyList<RoleTuningRow> RoleTuningTable
    {
        get { lock (_lock) return ToRoleTuningRows(_roleTunings); }
    }

    /// <summary>
    /// Replaces the entire role table (the editor sends all 3 at once) and persists it.
    /// Range validation is done in the endpoint; here only unknown roles are rejected.
    /// </summary>
    public IReadOnlyList<RoleTuningRow> ReplaceRoleTunings(IEnumerable<RoleTuningRow> rows)
    {
        var parsed = ParseRoleTuningRows(rows.ToList())
                     ?? throw new InvalidOperationException("unknown role in tuning table");
        lock (_lock)
        {
            _roleTunings = parsed;
            WriteRoleTunings(_roleTunings);
            return ToRoleTuningRows(_roleTunings);
        }
    }

    /// <summary>
    /// Replaces the entire tier set (the editor sends all 5 at once) and persists it.
    /// Content validation (mobs/boss exist) is done in the endpoint, which has GameData.
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

    /// <summary>
    /// LM-08: replaces the entire biome set (the editor sends all 5 at once) and persists it.
    /// Content validation (non-empty palettes) is done in the endpoint (LM-09).
    /// </summary>
    public IReadOnlyList<BiomeRow> ReplaceBiomes(IEnumerable<BiomeRow> biomes)
    {
        var next = biomes.OrderBy(b => b.Tier).ToList();
        lock (_lock)
        {
            _biomes = next;
            WriteBiomes(_biomes);
            return _biomes.ToList();
        }
    }
}
