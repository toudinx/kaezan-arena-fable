namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Canonical authored Kaezan content used to bootstrap local editable content.
/// Runtime .data files remain editable, but the initial migration lives in source control.
/// </summary>
public static class KaezanContentSeed
{
    private static readonly MonsterOutfit[] PlaceholderOutfits =
    [
        new(146, 95, 93, 38, 59, 3),
        new(22, 95, 95, 95, 95, 0),
        new(25, 88, 88, 88, 88, 0),
        new(35, 70, 70, 70, 70, 0),
        new(53, 94, 94, 94, 94, 0),
        new(61, 38, 38, 38, 38, 0),
        new(62, 57, 57, 57, 57, 0),
        new(64, 86, 86, 86, 86, 0),
        new(73, 115, 115, 115, 115, 0),
        new(80, 114, 114, 114, 114, 0),
    ];

    private static readonly int[] PlaceholderCorpses =
        [7349, 5965, 5967, 5974, 5978, 5980, 5983, 5989, 5995, 6003];

    private static readonly MonsterSeed[] MonsterSeeds =
    [
        new(1, "monster:t1-crystal-rat", "Crystal Rat", "common", "bruiser", "physical", "balanced", "physical", "earth"),
        new(1, "monster:t1-moss-sprout", "Moss Sprout", "common", "support", "earth", "balanced", "fire", "earth"),
        new(1, "monster:t1-lumen-worm", "Lumen Worm", "common", "skirmisher", "earth", "swift", "ice", "earth"),
        new(1, "monster:t1-frost-bat", "Frost Bat", "common", "skirmisher", "ice", "swift", "fire", "ice"),
        new(1, "monster:t1-dew-spider", "Dew Spider", "common", "controller", "earth", "caster", "fire", "earth"),
        new(1, "monster:t1-living-pebble", "Living Pebble", "common", "juggernaut", "physical", "tank", "earth", "physical"),
        new(1, "monster:t1-moss-matriarch", "Moss Matriarch", "elite", "support", "earth", "tank", "fire", "earth"),
        new(1, "monster:t1-frost-warg", "Frost Warg", "elite", "ranger", "ice", "swift", "fire", "ice"),
        new(1, "monster:t1-cave-brute", "Cave Brute", "elite", "bruiser", "physical", "tank", "ice", "physical"),
        new(1, "monster:t1-boss-heart-of-the-den", "Heart of the Den", "boss", "juggernaut", "earth", "tank", "physical", "earth"),

        new(2, "monster:t2-iron-sentry", "Iron Sentry", "common", "bruiser", "physical", "balanced", "energy", "physical"),
        new(2, "monster:t2-ember-raider", "Ember Raider", "common", "skirmisher", "fire", "swift", "ice", "fire"),
        new(2, "monster:t2-siege-slinger", "Siege Slinger", "common", "ranger", "physical", "balanced", "earth", "physical"),
        new(2, "monster:t2-ash-bomber", "Ash Bomber", "common", "artillery", "fire", "caster", "ice", "fire"),
        new(2, "monster:t2-clay-shieldbearer", "Clay Shieldbearer", "common", "juggernaut", "earth", "tank", "fire", "earth"),
        new(2, "monster:t2-forge-hound", "Forge Hound", "common", "breather", "fire", "glass", "ice", "fire"),
        new(2, "monster:t2-war-captain", "War Captain", "elite", "bruiser", "physical", "tank", "energy", "physical"),
        new(2, "monster:t2-ember-shaman", "Ember Shaman", "elite", "support", "fire", "caster", "ice", "fire"),
        new(2, "monster:t2-siege-beast", "Siege Beast", "elite", "juggernaut", "earth", "tank", "fire", "earth"),
        new(2, "monster:t2-boss-marshal-ferrum", "Marshal Ferrum", "boss", "bruiser", "physical", "balanced", "energy", "physical"),

        new(3, "monster:t3-candle-wraith", "Candle Wraith", "common", "controller", "holy", "caster", "death", "holy"),
        new(3, "monster:t3-bone-chorister", "Bone Chorister", "common", "support", "death", "balanced", "holy", "death"),
        new(3, "monster:t3-grave-acolyte", "Grave Acolyte", "common", "ranger", "death", "caster", "holy", "death"),
        new(3, "monster:t3-frost-revenant", "Frost Revenant", "common", "bruiser", "ice", "tank", "fire", "ice"),
        new(3, "monster:t3-silver-specter", "Silver Specter", "common", "skirmisher", "holy", "swift", "death", "holy"),
        new(3, "monster:t3-ossuary-guard", "Ossuary Guard", "common", "juggernaut", "physical", "tank", "death", "physical"),
        new(3, "monster:t3-crypt-hierophant", "Crypt Hierophant", "elite", "support", "holy", "caster", "death", "holy"),
        new(3, "monster:t3-widow-shade", "Widow Shade", "elite", "controller", "death", "glass", "holy", "death"),
        new(3, "monster:t3-frost-lich", "Frost Lich", "elite", "artillery", "ice", "caster", "fire", "ice"),
        new(3, "monster:t3-boss-abbess-noctra", "Abbess Noctra", "boss", "controller", "death", "caster", "holy", "death"),

        new(4, "monster:t4-ember-drake", "Ember Drake", "common", "breather", "fire", "balanced", "ice", "fire"),
        new(4, "monster:t4-basalt-crawler", "Basalt Crawler", "common", "juggernaut", "earth", "tank", "fire", "earth"),
        new(4, "monster:t4-storm-wyrmling", "Storm Wyrmling", "common", "skirmisher", "energy", "swift", "earth", "energy"),
        new(4, "monster:t4-lava-acolyte", "Lava Acolyte", "common", "artillery", "fire", "caster", "ice", "fire"),
        new(4, "monster:t4-slag-imp", "Slag Imp", "common", "ranger", "fire", "glass", "ice", "fire"),
        new(4, "monster:t4-ashscale-guardian", "Ashscale Guardian", "common", "bruiser", "earth", "tank", "fire", "earth"),
        new(4, "monster:t4-thunder-hydra", "Thunder Hydra", "elite", "breather", "energy", "balanced", "earth", "energy"),
        new(4, "monster:t4-magma-colossus", "Magma Colossus", "elite", "juggernaut", "fire", "tank", "ice", "fire"),
        new(4, "monster:t4-terra-dragon", "Terra Dragon", "elite", "artillery", "earth", "caster", "fire", "earth"),
        new(4, "monster:t4-boss-aurelion-ashscale", "Aurelion Ashscale", "boss", "breather", "fire", "balanced", "ice", "fire"),

        new(5, "monster:t5-void-cultist", "Void Cultist", "common", "controller", "death", "caster", "holy", "death"),
        new(5, "monster:t5-halo-breaker", "Halo Breaker", "common", "bruiser", "holy", "glass", "death", "holy"),
        new(5, "monster:t5-doom-hound", "Doom Hound", "common", "skirmisher", "death", "swift", "holy", "death"),
        new(5, "monster:t5-storm-bound", "Storm Bound", "common", "ranger", "energy", "balanced", "earth", "energy"),
        new(5, "monster:t5-pyre-oracle", "Pyre Oracle", "common", "artillery", "fire", "caster", "ice", "fire"),
        new(5, "monster:t5-abyss-warden", "Abyss Warden", "common", "juggernaut", "physical", "tank", "holy", "physical"),
        new(5, "monster:t5-abyss-herald", "Abyss Herald", "elite", "controller", "death", "caster", "holy", "death"),
        new(5, "monster:t5-solar-revenant", "Solar Revenant", "elite", "support", "holy", "balanced", "death", "holy"),
        new(5, "monster:t5-storm-tyrant", "Storm Tyrant", "elite", "artillery", "energy", "glass", "earth", "energy"),
        new(5, "monster:t5-boss-echo-of-kaezan", "Echo of Kaezan", "boss", "controller", "death", "balanced", "holy", "death"),
    ];

    private static readonly ClassItemLine[] ClassItemLines =
    [
        new(Classes.WarriorId, "Seren", "physical", "sword", 3288, 3392, 3370,
            "Astral Blade", "Zenith Visor", "Astral Plate", true),
        new(Classes.OracleId, "Eloa", "holy", "wand", 3070, 3011, 3569,
            "Dawn Scepter", "Vigil Crown", "Absolution Mantle", false),
        new(Classes.NecromancerId, "Velvet", "death", "wand", 3072, 3210, 3567,
            "Nightmare Wand", "Nocturne Veil", "Black Lake Vestment", false),
        new(Classes.PyromancerId, "Rin", "fire", "wand", 3071, 827, 826,
            "Pact Ember", "Contract Monocle", "Ashwing Coat", false),
        new(Classes.StormcallerId, "Rynna", "energy", "axe", 802, 828, 825,
            "Thunder Fang", "Conductor Crest", "Stormscale Mail", true),
        new(Classes.CryomancerId, "Lunara", "ice", "sword", 680, 829, 824,
            "Moonfrost Edge", "Crescent Mask", "Newmoon Robe", true),
        new(Classes.ShamanId, "Gaia", "earth", "distance", 29417, 830, 811,
            "Monolith Bow", "Bedrock Hood", "Rootmantle", false),
    ];

    private static readonly string[] TierSuffixes = ["I", "II", "III", "IV", "V"];
    private static readonly int[] AttackByTier = [0, 20, 42, 76, 128, 205];
    private static readonly int[] ArmorByTier = [0, 6, 14, 29, 50, 76];
    private static readonly int[] DefenseByTier = [0, 12, 28, 58, 92, 128];
    private static readonly double[] CritDamageByTier = [0, 0.16, 0.25, 0.42, 0.62, 0.82];
    private static readonly double[] CritChanceByTier = [0, 0.05, 0.08, 0.12, 0.16, 0.21];
    private static readonly double[] CooldownByTier = [0, 0.03, 0.06, 0.09, 0.12, 0.16];
    private static readonly double[] LeechChanceByTier = [0, 0.05, 0.08, 0.12, 0.16, 0.21];
    private static readonly double[] LeechAmountByTier = [0, 0.03, 0.05, 0.08, 0.11, 0.14];
    private static readonly double[] ResistByTier = [0, 0.05, 0.09, 0.13, 0.17, 0.22];
    private static readonly int[] ElementDamageByTier = [0, 8, 16, 32, 52, 78];
    private static readonly int[] MountSpeedByTier = [0, 14, 24, 38, 54, 70];

    public static IReadOnlyList<MonsterDefinition> Monsters =>
        MonsterSeeds.Select((seed, index) => Monster(seed, index)).ToList();

    public static IReadOnlyList<AuthoredItemDefinition> AuthoredItems =>
        BuildItems().Select(item => ItemAuthoring.Normalize(item, item.ItemId)).ToList();

    public static readonly DungeonTier[] Tiers =
    [
        new(1, "Toca Ecoante", "Cavernas de cristal vivo onde a terra ainda aprende a lutar.",
            TierRefs(1, "common"), TierRefs(1, "elite"), "monster:t1-boss-heart-of-the-den", 1, 1.0),
        new(2, "Forte Ferrum", "Um bastiao de ferro, brasa e terra compactada.",
            TierRefs(2, "common"), TierRefs(2, "elite"), "monster:t2-boss-marshal-ferrum", 2, 1.35),
        new(3, "Cripta Noctra", "Catacumbas de velas frias, ecos santos e morte acumulada.",
            TierRefs(3, "common"), TierRefs(3, "elite"), "monster:t3-boss-abbess-noctra", 3, 1.8),
        new(4, "Covil Ashscale", "Ninhos vulcanicos onde fogo, pedra e raio dividem a mesma pele.",
            TierRefs(4, "common"), TierRefs(4, "elite"), "monster:t4-boss-aurelion-ashscale", 4, 2.4),
        new(5, "Abismo Kaezan", "O fundo da descida, onde os ecos autorais tomam forma final.",
            TierRefs(5, "common"), TierRefs(5, "elite"), "monster:t5-boss-echo-of-kaezan", 5, 3.2),
    ];

    private static MonsterDefinition Monster(MonsterSeed seed, int index)
    {
        var outfit = PlaceholderOutfits[index % PlaceholderOutfits.Length];
        var corpse = PlaceholderCorpses[index % PlaceholderCorpses.Length];
        var resistances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (seed.ResistTo != "physical") resistances[seed.ResistTo] = seed.Rank == "boss" ? 20 : 10;
        if (!string.IsNullOrWhiteSpace(seed.WeakTo)) resistances[seed.WeakTo] = seed.Rank == "boss" ? -15 : -10;

        var modifier = seed.Rank switch
        {
            "boss" => 1.08,
            "elite" => 1.03,
            _ => 1.0
        };

        return MonsterAuthoring.Normalize(new MonsterDefinition(
            seed.Id,
            seed.Name,
            $"Criatura Kaezan T{seed.Tier} {seed.Rank} de elemento {seed.Element}.",
            outfit,
            corpse,
            seed.Tier,
            seed.Rank,
            seed.Behavior,
            seed.Element,
            seed.Preset,
            modifier,
            modifier,
            1,
            1,
            $"Kaezan T{seed.Tier}",
            resistances,
            "",
            true), seed.Id);
    }

    private static IReadOnlyList<AuthoredItemDefinition> BuildItems()
    {
        var items = new List<AuthoredItemDefinition>(135);
        for (var tier = 1; tier <= 5; tier++)
        {
            foreach (var line in ClassItemLines)
            {
                items.Add(Weapon(items.Count, tier, line));
                items.Add(Helmet(items.Count, tier, line));
                items.Add(Armor(items.Count, tier, line));
            }

            items.Add(Ring(items.Count, tier, "Precision Ring", 3006, CritChanceByTier[tier], DefenseByTier[tier] / 4));
            items.Add(Ring(items.Count, tier, "Guard Ring", 3098, CritChanceByTier[tier] * 0.6, DefenseByTier[tier] / 2));
            items.Add(Amulet(items.Count, tier, "Resonance Amulet", 3014, PrimaryAmuletElement(tier), ElementDamageByTier[tier]));
            items.Add(Amulet(items.Count, tier, "Counterpoint Amulet", 3021, SecondaryAmuletElement(tier), (int)Math.Round(ElementDamageByTier[tier] * 0.85)));
            items.Add(Mount(items.Count, tier));
        }

        items.Add(Relic(items.Count, 1, "Heartroot Relic", 3021, EquipmentSlots.Necklace, "earth"));
        items.Add(Relic(items.Count, 2, "Ferrum Signet", 3006, EquipmentSlots.Ring, "physical"));
        items.Add(Relic(items.Count, 3, "Noctra Reliquary", 3014, EquipmentSlots.Necklace, "death"));
        items.Add(Relic(items.Count, 4, "Ashscale Cinderheart", 3021, EquipmentSlots.Necklace, "fire"));
        items.Add(Relic(items.Count, 5, "Echo of Kaezan Core", 3014, EquipmentSlots.Necklace, "death"));

        return items;
    }

    private static AuthoredItemDefinition Weapon(int offset, int tier, ClassItemLine line) =>
        Item(
            offset,
            line.WeaponSource,
            $"{line.WeaponName} {TierSuffixes[tier - 1]}",
            $"{line.ClassName} weapon line, tier {tier}.",
            tier,
            EquipmentSlots.Weapon,
            line.WeaponType,
            line.Element,
            attack: AttackByTier[tier] + (line.Melee ? 4 : 0),
            defense: Math.Max(DefenseByTier[tier] / (line.Melee ? 3 : 5), 1),
            critDamage: CritDamageByTier[tier],
            classes: [line.ClassId]);

    private static AuthoredItemDefinition Helmet(int offset, int tier, ClassItemLine line) =>
        Item(
            offset,
            line.HelmetSource,
            $"{line.HelmetName} {TierSuffixes[tier - 1]}",
            $"{line.ClassName} helmet line, tier {tier}.",
            tier,
            EquipmentSlots.Helmet,
            null,
            line.Element,
            armor: Math.Max(ArmorByTier[tier] / 2, 1),
            defense: Math.Max(DefenseByTier[tier] / 3, 1),
            cooldownReduction: line.Melee ? CooldownByTier[tier] * 0.65 : CooldownByTier[tier],
            lifeStealChance: line.Melee ? LeechChanceByTier[tier] : LeechChanceByTier[tier] * 0.65,
            lifeStealAmount: line.Melee ? LeechAmountByTier[tier] : LeechAmountByTier[tier] * 0.65,
            classes: [line.ClassId]);

    private static AuthoredItemDefinition Armor(int offset, int tier, ClassItemLine line) =>
        Item(
            offset,
            line.ArmorSource,
            $"{line.ArmorName} {TierSuffixes[tier - 1]}",
            $"{line.ClassName} armor line, tier {tier}.",
            tier,
            EquipmentSlots.Armor,
            null,
            line.Element,
            armor: ArmorByTier[tier] + (line.Melee ? 2 : 0),
            defense: DefenseByTier[tier],
            physicalResistance: ResistByTier[tier] * (line.Melee ? 1.0 : 0.75),
            elementalResistance: (line.Element, ResistByTier[tier]),
            classes: [line.ClassId]);

    private static AuthoredItemDefinition Ring(
        int offset, int tier, string name, int source, double critChance, int defense) =>
        Item(
            offset,
            source,
            $"{name} {TierSuffixes[tier - 1]}",
            $"Generic ring line, tier {tier}.",
            tier,
            EquipmentSlots.Ring,
            null,
            "physical",
            defense: defense,
            critChance: critChance);

    private static AuthoredItemDefinition Amulet(
        int offset, int tier, string name, int source, string element, int elementDamage) =>
        Item(
            offset,
            source,
            $"{name} {TierSuffixes[tier - 1]}",
            $"Generic elemental amulet line, tier {tier}.",
            tier,
            EquipmentSlots.Necklace,
            null,
            element,
            defense: Math.Max(DefenseByTier[tier] / 3, 1),
            elementDamage: elementDamage);

    private static AuthoredItemDefinition Mount(int offset, int tier) =>
        Item(
            offset,
            -390,
            $"Echo Courser {TierSuffixes[tier - 1]}",
            $"Generic mount line, tier {tier}.",
            tier,
            EquipmentSlots.Mount,
            null,
            "physical",
            mountSpeed: MountSpeedByTier[tier],
            moveSpeedPercent: Math.Min(0.02 + tier * 0.025, GameConfig.EquipmentMoveSpeedCap));

    private static AuthoredItemDefinition Relic(
        int offset, int tier, string name, int source, string slot, string element) =>
        Item(
            offset,
            source,
            $"{name} {TierSuffixes[tier - 1]}",
            $"Boss relic line, tier {tier}.",
            tier,
            slot,
            null,
            element,
            defense: slot == EquipmentSlots.Ring ? 1 : 0,
            elementDamage: slot == EquipmentSlots.Necklace ? 1 : 0,
            critChance: slot == EquipmentSlots.Ring ? 0.01 : 0,
            tag: GameConfig.AuthoredItemTagRelic,
            statMultiplier: GameConfig.AuthoredItemRelicMultiplierDefault);

    private static AuthoredItemDefinition Item(
        int offset,
        int sourceItemId,
        string name,
        string description,
        int tier,
        string slot,
        string? weaponType,
        string element,
        int attack = 0,
        int armor = 0,
        int defense = 0,
        int mountSpeed = 0,
        int elementDamage = 0,
        double critChance = 0,
        double critDamage = 0,
        double lifeStealChance = 0,
        double lifeStealAmount = 0,
        double cooldownReduction = 0,
        double moveSpeedPercent = 0,
        double physicalResistance = 0,
        (string Element, double Value)? elementalResistance = null,
        IReadOnlyList<string>? classes = null,
        string tag = GameConfig.AuthoredItemTagNormal,
        double statMultiplier = 1)
    {
        var fire = 0.0;
        var ice = 0.0;
        var earth = 0.0;
        var energy = 0.0;
        var death = 0.0;
        var holy = 0.0;
        if (elementalResistance is { } resistance)
        {
            switch (resistance.Element)
            {
                case "fire": fire = resistance.Value; break;
                case "ice": ice = resistance.Value; break;
                case "earth": earth = resistance.Value; break;
                case "energy": energy = resistance.Value; break;
                case "death": death = resistance.Value; break;
                case "holy": holy = resistance.Value; break;
            }
        }

        return new AuthoredItemDefinition(
            GameConfig.AuthoredItemIdBase + offset,
            sourceItemId,
            name,
            description,
            80 * tier * tier,
            attack,
            armor,
            defense,
            mountSpeed,
            element,
            elementDamage,
            0,
            critChance,
            critDamage,
            lifeStealChance,
            lifeStealAmount,
            cooldownReduction,
            moveSpeedPercent,
            physicalResistance,
            fire,
            ice,
            earth,
            energy,
            death,
            holy,
            classes ?? [],
            0,
            tier,
            slot,
            weaponType,
            tag,
            statMultiplier);
    }

    private static string[] TierRefs(int tier, string rank) =>
        MonsterSeeds
            .Where(seed => seed.Tier == tier && seed.Rank == rank)
            .Select(seed => seed.Id)
            .ToArray();

    private static string PrimaryAmuletElement(int tier) => tier switch
    {
        1 => "earth",
        2 => "fire",
        3 => "death",
        4 => "fire",
        _ => "death"
    };

    private static string SecondaryAmuletElement(int tier) => tier switch
    {
        1 => "ice",
        2 => "physical",
        3 => "holy",
        4 => "energy",
        _ => "energy"
    };

    private sealed record MonsterSeed(
        int Tier,
        string Id,
        string Name,
        string Rank,
        string Behavior,
        string Element,
        string Preset,
        string WeakTo,
        string ResistTo);

    private sealed record ClassItemLine(
        string ClassId,
        string ClassName,
        string Element,
        string WeaponType,
        int WeaponSource,
        int HelmetSource,
        int ArmorSource,
        string WeaponName,
        string HelmetName,
        string ArmorName,
        bool Melee);
}
