namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// All simulation/meta constants live here (kaezan-arena ArenaConfig convention).
/// Never hardcode a gameplay value anywhere else.
/// </summary>
public static class GameConfig
{
    // ---- simulation ----
    public const int TickMs = 100;
    public const int PlayerBaseSpeed = 250;
    public const int MonsterSpeedMultiplier = 2;
    public const int GroundFriction = 100;
    public const double DiagonalStepFactor = 1.4;
    public const int MinStepMs = 160;
    public const int MaxStepMs = 1400;
    public const int StepGraceMs = 80;
    public const int MonsterAggroRange = 8;
    public const int AggroDropRange = 12;
    public const int AggroDropOutOfRangeMs = 4000;
    public const int AggroDropNoLosMs = 6000;
    public const int MonsterWanderIntervalMs = 1600;
    public const int PlayerAutoAttackMs = 1800;
    public const int MeleeRange = 1;
    public const int BowRange = 5;
    public const int WandRange = 4;
    public const double CritChance = 0.05;
    public const double CritMultiplier = 1.5;
    public const int CorpseDecayMs = 30000;
    public const int LootPickupNoticeMs = 1200;
    public const int VoiceIntervalMs = 9000;
    public const int VoiceChancePercent = 4;

    // ---- monster kit fidelity (T-53: conditions/summons/healing/speed from canary data) ----
    /// <summary>Tames raw tibia damage numbers into arena pacing (applies to hits and DoTs).</summary>
    // MVP/dificuldade: baixado de 0.35 — recebíamos dano demais. Sobe pra punir mais.
    public const double MonsterDamageTuning = 0.26;
    public const int ConditionMaxTicks = 10;
    public const int ConditionDefaultTickMs = 2000;
    public const double ConditionResistCap = 0.85;
    /// <summary>Canary speedChange is an absolute speed delta; divide by this to get a factor.</summary>
    public const double SpeedChangeReference = 600.0;
    public const double SlowFactorFloor = 0.40;
    public const double HasteFactorCap = 1.5;
    public const int SlowDurationCapMs = 6000;
    public const int DefaultHasteDurationMs = 5000;
    public const int MaxAliveSummons = 8;
    public const int SummonMinIntervalMs = 1000;
    /// <summary>Single heal proc never restores more than this fraction of the monster's max HP.</summary>
    public const double MonsterHealCapFraction = 0.10;

    // ---- authored monsters (admin content) ----
    public const double AuthoredModifierMin = 0.65;
    public const double AuthoredModifierMax = 1.50;
    public const int AuthoredResistanceMin = -100;
    public const int AuthoredResistanceMax = 100;

    /// <summary>
    /// Direct arena-scale baselines. Authored bosses use these values and never receive the
    /// legacy BossHpScale multiplier.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, MonsterStatLine> MonsterStatLines =
        new Dictionary<string, MonsterStatLine>
        {
            ["1:common"] = new(30, 8, 2, 80, 15),
            ["1:elite"] = new(75, 13, 4, 84, 38),
            ["1:boss"] = new(260, 18, 6, 82, 120),
            ["2:common"] = new(85, 18, 6, 85, 45),
            ["2:elite"] = new(210, 29, 9, 90, 110),
            ["2:boss"] = new(650, 42, 13, 88, 350),
            ["3:common"] = new(220, 40, 12, 90, 120),
            ["3:elite"] = new(520, 64, 18, 95, 300),
            ["3:boss"] = new(1450, 90, 24, 94, 850),
            ["4:common"] = new(550, 85, 22, 100, 320),
            ["4:elite"] = new(1250, 132, 32, 105, 800),
            ["4:boss"] = new(3100, 185, 42, 102, 1900),
            ["5:common"] = new(1400, 175, 36, 110, 850),
            ["5:elite"] = new(3100, 270, 52, 116, 2100),
            ["5:boss"] = new(6200, 360, 66, 112, 4800),
        };

    public static readonly MonsterStatPreset[] MonsterStatPresets =
    [
        new("balanced", "Equilibrado", "Sem desvio do baseline.", 1, 1, 1, 1),
        new("tank", "Resistente", "Mais vida, menos dano e mobilidade.", 1.25, 0.85, 0.90, 0.90),
        new("glass", "Canhao de vidro", "Mais dano, menos vida.", 0.80, 1.20, 1, 1.05),
        new("swift", "Veloz", "Move e ataca mais rapido, com menos impacto.", 0.90, 0.90, 1.20, 1.15),
        new("caster", "Conjurador", "Ataques fortes e ritmo deliberado.", 0.90, 1.10, 0.95, 0.95),
    ];

    public static readonly MonsterElementProfile[] MonsterElementProfiles =
    [
        new("physical", "Fisico", 1, 0, null),
        new("fire", "Fogo", 16, 4, "fire"),
        new("ice", "Gelo", 44, 29, "freeze"),
        new("energy", "Energia", 12, 5, "energy"),
        new("earth", "Terra", 17, 30, "poison"),
        new("holy", "Sagrado", 40, 31, "dazzle"),
        new("death", "Morte", 18, 11, "curse"),
    ];

    public static readonly MonsterBehaviorProfile[] MonsterBehaviorProfiles =
    [
        new("bruiser", "Brutamontes", "Pressao corpo a corpo com golpes de pesos diferentes.", 1, 85,
        [
            new("melee", 1, 0, 0, 0, false, 1700, 100, 0.72, 1.08, false, 0),
            new("spell", 1, 1, 0, 0, false, 3200, 28, 0.48, 0.78, true, 0),
        ]),
        new("skirmisher", "Escaramucador", "Ataques curtos, rapidos e menos previsiveis.", 1, 65,
        [
            new("melee", 1, 0, 0, 0, false, 1150, 82, 0.52, 0.88, false, 0),
            new("melee", 1, 0, 0, 0, false, 2300, 45, 0.75, 1.12, true, 0),
        ]),
        new("ranger", "Atirador", "Mantem distancia e alterna disparos fisicos e elementais.", 5, 90,
        [
            new("spell", 5, 0, 0, 0, true, 1750, 88, 0.62, 1.02, false, 0),
            new("spell", 5, 0, 0, 0, true, 2850, 52, 0.78, 1.18, true, 0),
        ]),
        new("artillery", "Artilharia", "Ataques lentos em area com alto impacto.", 6, 95,
        [
            new("spell", 6, 2, 0, 0, true, 2700, 78, 0.78, 1.22, true, 0),
            new("spell", 6, 3, 0, 0, true, 4400, 35, 0.95, 1.38, true, 0),
        ]),
        new("breather", "Soprador", "Combina mordida e cone elemental.", 2, 85,
        [
            new("melee", 1, 0, 0, 0, false, 1750, 92, 0.60, 0.96, false, 0),
            new("spell", 0, 0, 4, 2, false, 3100, 62, 0.72, 1.18, true, 0),
        ]),
        new("controller", "Controlador", "Dano moderado com condicao elemental ocasional.", 4, 88,
        [
            new("spell", 4, 0, 0, 0, true, 1850, 86, 0.52, 0.86, true, 0),
            new("spell", 4, 1, 0, 0, true, 3600, 42, 0.40, 0.72, true, 0.75),
        ]),
        new("support", "Sustentacao", "Pressao leve com cura propria intermitente.", 4, 90,
        [
            new("spell", 4, 0, 0, 0, true, 2050, 88, 0.55, 0.90, true, 0),
        ], 0.07, 4200),
        new("juggernaut", "Juggernaut", "Lento, resistente e perigoso quando conecta.", 1, 95,
        [
            new("melee", 1, 0, 0, 0, false, 2300, 100, 0.82, 1.28, false, 0),
            new("spell", 1, 2, 0, 0, false, 4200, 32, 0.60, 1.02, true, 0),
        ]),
    ];

    /// <summary>Per-tick FX by condition type (tibia CONST_ME ids).</summary>
    public static readonly IReadOnlyDictionary<string, int> ConditionTickFx = new Dictionary<string, int>
    {
        ["poison"] = 17, ["fire"] = 16, ["energy"] = 12, ["bleed"] = 1,
        ["curse"] = 18, ["freeze"] = 44, ["drown"] = 54, ["dazzle"] = 40,
    };

    /// <summary>PT-BR labels for run-end reason ("morta por veneno").</summary>
    public static readonly IReadOnlyDictionary<string, string> ConditionLabelPt = new Dictionary<string, string>
    {
        ["poison"] = "veneno", ["fire"] = "queimadura", ["energy"] = "eletrocussão", ["bleed"] = "sangramento",
        ["curse"] = "maldição", ["freeze"] = "congelamento", ["drown"] = "afogamento", ["dazzle"] = "ofuscamento",
    };

    // ---- run / leveling ----
    public const int MaxRunLevel = 30;
    public static long XpForRunLevel(int level) => (long)(40 * Math.Pow(level, 1.65));
    public const int CardChoicesPerOffer = 3;
    public const int MaxCardStacks = 3;
    public const int CardOfferTimeoutMs = 20000;
    public const int UltimateGaugeMax = 100;
    public const int GaugeFillPerKill = 8;
    public const double GaugeFillPerDamageTaken = 0.5;

    // ---- dungeon generation ----
    public const int Floor1Size = 40;
    public const int Floor2Size = 30;
    public const int RoomMin = 5;
    public const int RoomMax = 9;
    public const int RoomsFloor1 = 8;
    public const int RoomsFloor2 = 4;
    public const int ChestsPerFloor = 2;
    public const int ChestAmbushPercent = 25;
    public const int SpawnBudgetBase = 14;
    public const double SpawnBudgetTierGrowth = 0.55;

    // ---- player damage ----
    /// <summary>Multiplicador global do dano do player (autos + skills). MVP/dificuldade: dávamos pouco dano.</summary>
    public const double PlayerDamageMult = 1.4;
    public const double AtkPerRunLevel = 0.06;
    public const double AscensionAtkBonus = 0.08;
    public const double DamageRollMin = 0.85;
    public const double DamageRollMax = 1.15;
    public const double BloodRageAttackMultiplier = 1.20;
    public const double SentinelAegisAttackMultiplier = 1.15;
    public const double SentinelAegisAttackSpeedMultiplier = 1.15;
    public const double NaturesEmbraceHealFraction = 0.45;
    public const double ExposedWeaknessDamageMultiplier = 1.15;
    public const double SappedStrengthDamageMultiplier = 0.90;
    public const double PlayerDamageReductionCap = 0.60;

    // ---- equipment (T-51: raw tibia attributes converted to arena-scale bonuses) ----
    public const double EquipmentAttackScale = 0.25;
    public const int EquipmentHpPerArmor = 6;
    public const int EquipmentHpPerDefense = 2;
    public const double EquipmentDamageReductionPerArmor = 0.004;
    public const double EquipmentDamageReductionPerDefense = 0.002;
    public const double EquipmentDamageReductionCap = 0.35;
    public const int MountHpPerSpeed = 2;
    public const double MountMoveSpeedPercentPerSpeed = 0.005;
    public const double BossMountDropChance = 0.20;
    public static int MountItemId(int lookType) => -lookType;
    public static readonly IReadOnlyDictionary<int, int> TierMountLookTypes = new Dictionary<int, int>
    {
        [1] = 368, // Widow Queen
        [2] = 370, // War Bear
        [3] = 390, // Crystal Wolf
        [4] = 506, // Dragonling
        [5] = 626, // Flamesteed
    };

    // ---- sustain baseline (ponte até os sets: todo Kaeli tem um mínimo, sem depender de card) ----
    /// <summary>Regen passivo por segundo como fração da vida máxima, mesmo sem o card de regen.</summary>
    public const double BaselineRegenPctPerSec = 0.006;
    /// <summary>+regen por run-level (recompensa quem sobe de nível dentro da run).</summary>
    public const double BaselineRegenPctPerRunLevel = 0.0006;
    /// <summary>Life leech mínimo: fração do dano causado que volta como vida, mesmo sem card.</summary>
    public const double BaselineLifesteal = 0.02;

    // ---- drops com função (consumíveis curam ao pegar; lixo vira ouro) ----
    /// <summary>Comida cura esta fração da vida máxima ao ser pega.</summary>
    public const double FoodHealPct = 0.05;
    /// <summary>Poções de vida curam por fração da vida máxima — mais forte a poção, mais cura.</summary>
    public const double PotionHealBasic = 0.15;
    public const double PotionHealStrong = 0.25;
    public const double PotionHealGreat = 0.40;
    /// <summary>Palavras que marcam um item como comida (resolvidas para ids em GameData).</summary>
    public static readonly string[] FoodNameWords =
    [
        "meat", "ham", "cheese", "fish", "cookie", "cherry", "corncob", "mushroom", "worm",
        "egg", "bread", "carrot", "apple", "banana", "grape", "melon", "salmon", "mango",
        "pear", "tomato", "strawberry", "blueberry", "bun", "candy", "roll", "rye"
    ];

    // ---- death / rewards ----
    public const double DefeatGoldKeptFraction = 0.5;
    // MVP/teste: recompensa por run inflada. Produção: Base 120, PerTier 40.
    public const int VictoryKaerosBase = 400;
    public const int VictoryKaerosPerTier = 120;
    public const long AccountXpPerVictory = 60;
    public const long AccountXpPerDefeat = 20;
    public const long AccountXpPerRunLevel = 6;
    public const int RunReconnectGraceMs = 60000;
    public static long XpForAccountLevel(int level) => (long)(80 * Math.Pow(level, 1.7));
    public const int MaxAccountLevel = 100;

    // ---- dungeon tiers (mob salas + boss). Account level gates. ----
    public static readonly DungeonTier[] Tiers =
    [
        new(1, "Toca Ecoante", "Cavernas infestadas de vermes sob o Monte Sternum.",
            ["Rat", "Cave Rat", "Wolf", "Winter Wolf", "Bug", "Spider", "Snake", "Troll"],
            ["Rotworm", "Scorpion", "Centipede", "Troll Champion", "Carrion Worm", "Poison Spider", "Slime"],
            "Rotworm Queen", 1, 1.0),
        new(2, "Forte Uruk", "Um bastião orc tomado pela ganância.",
            ["Orc", "Orc Spearman", "Goblin", "Goblin Scavenger", "Dwarf", "War Wolf"],
            ["Orc Warrior", "Orc Shaman", "Dwarf Soldier", "Orc Rider", "Orc Berserker"],
            "Orc Warlord", 4, 1.35),
        new(3, "Cripta Sombria", "Catacumbas onde os mortos não descansam.",
            ["Skeleton", "Ghoul", "Ghost", "Mummy", "Bonelord"],
            ["Crypt Shambler", "Demon Skeleton", "Witch", "Vampire", "Necromancer", "Banshee"],
            "Black Knight", 8, 1.8),
        new(4, "Covil Escamado", "Ninhos de dragões nas profundezas vulcânicas.",
            ["Minotaur", "Minotaur Archer", "Minotaur Mage", "Minotaur Guard", "Fire Elemental", "Dragon Hatchling"],
            ["Cyclops", "Earth Elemental", "Dragon", "Dragon Lord Hatchling", "Frost Dragon Hatchling"],
            "Dragon Lord", 14, 2.4),
        new(5, "Abismo Ecoante", "Onde os ecos do abismo ganham forma.",
            ["Cyclops", "Fire Devil", "Dragon", "Dragon Lord Hatchling", "Frost Dragon Hatchling"],
            ["Giant Spider", "Dragon Lord", "Frost Dragon", "Hydra", "Hellfire Fighter", "Behemoth", "Hellhound", "Dark Torturer", "Juggernaut"],
            "Demon", 22, 3.2),
    ];

    public static readonly int[] BossHpMultiplier = [1, 8, 10, 4, 5, 2];
    // index by tier: Rotworm Queen 105hp*8, Orc Warlord 950*~2... resolved in code per boss below.
    public static int BossHpScale(string bossName) => bossName switch
    {
        "Rotworm Queen" => 10,   // raid version has tiny hp
        "Orc Warlord" => 2,
        "Black Knight" => 2,
        "Dragon Lord" => 2,
        "Demon" => 1,
        _ => 2
    };

    // ---- gacha ----
    public const int PullCostKaeros = 160;
    public const int FiveStarHardPity = 80;
    public const int FiveStarSoftPityStart = 65;
    public const double FiveStarBaseRate = 0.008;
    public const double FiveStarSoftPityRamp = 0.06;
    public const int FourStarPity = 10;
    public const double FourStarBaseRate = 0.06;
    // MVP/teste: economia generosa pra testar conteúdo. Produção: StartingKaeros 4000, Gold 500.
    public const int StartingKaeros = 20000;
    public const int StartingGold = 3000;
    public const int ItemFallbackSalePrice = 5;
    public static readonly Dictionary<int, int> DupeShards = new() { [3] = 5, [4] = 20, [5] = 50 };
    public static readonly int[] AscensionShardCost = [10, 15, 25, 40, 60, 80]; // A1..A6
    public const int AddonOneAscension = 2;
    public const int AddonTwoAscension = 4;

    // ---- kaeli depth: afinidade / presentes / skins / maestria (refundação 2026-06-12) ----
    public const int AffinityMaxLevel = 10;
    /// <summary>XP necessário para ir do nível N para o N+1.</summary>
    public static long XpForAffinityLevel(int level) => (long)(40 * Math.Pow(level, 1.35));
    /// <summary>+1% ATK e HP por nível de afinidade, aplicado no início da run.</summary>
    public const double AffinityStatBonusPerLevel = 0.01;
    public const long AffinityXpVictory = 50;
    public const long AffinityXpDefeat = 20;
    public const long AffinityXpPerRunLevel = 2;
    /// <summary>Níveis de afinidade que destravam os fragmentos de lore 1..4.</summary>
    public static readonly int[] AffinityLoreLevels = [2, 4, 6, 8];
    /// <summary>Kaeros entregues ao alcançar o nível (marcos de afinidade).</summary>
    public static readonly IReadOnlyDictionary<int, int> AffinityKaerosRewards =
        new Dictionary<int, int> { [3] = 200, [5] = 400, [7] = 600, [10] = 1000 };

    public const int GiftsPerKaeliPerDay = 3;
    public const double GiftBaseXp = 15;
    public const double GiftXpPerGold = 0.5;
    public const double GiftFavoriteMultiplier = 2.0;
    public const long GiftXpCap = 400;

    public const int MasteryPointsPerVictory = 3;
    public const int MasteryPointsPerDefeat = 1;
    public const long MasteryRespecGold = 1000;

    /// <summary>Kaeros devolvidos por Kaeli removida do roster encontrada numa conta antiga.</summary>
    public const int CutKaeliRefundKaeros = 600;

    // ---- dailies ----
    public const int DailyContractCount = 3;
    // MVP/teste: dailies generosas. Produção: Kaeros 100, Gold 150.
    public const int DailyKaerosReward = 500;
    public const int DailyGoldReward = 600;
    public const long DailyAccountXpReward = 25;

    // ---- bestiary ----
    public static readonly long[] BestiaryRankKills = [10, 50, 100, 250];
    public const double BestiaryDamageBonusPerRank = 0.01;

    // ---- F-E: boss posture / echo break ----
    /// <summary>Base posture pool of a tier-1 boss; scales up per tier and per broken cycle.</summary>
    public const double PostureBaseMax = 120.0;
    /// <summary>+35% posture pool per tier above 1 (tougher bosses take more pressure to break).</summary>
    public const double PostureTierGrowth = 0.35;
    /// <summary>Posture pool regrows after each break: max = base * (1 + cycle * this).</summary>
    public const double PostureMaxGrowthPerCycle = 0.5;
    /// <summary>Posture built by a connecting auto-attack vs. a skill hit (skills pressure harder).</summary>
    public const double PostureGainPerAuto = 7.0;
    public const double PostureGainPerSkill = 16.0;
    /// <summary>Hitting an element the boss is weak to (resist &lt; 0) builds posture faster.</summary>
    public const double PostureWeaknessMult = 1.7;
    /// <summary>Posture only decays after this idle window without a hit.</summary>
    public const int PostureDecayDelayMs = 3000;
    /// <summary>Once idle, posture bleeds this fraction of its max pool per second.</summary>
    public const double PostureDecayFractionPerSec = 0.12;
    /// <summary>Stagger (Echo Break) window where the boss is stunned and amplified.</summary>
    public const int PostureStaggerMs = 4000;
    /// <summary>Raw-damage multiplier during stagger, one entry per cycle (caps at the last).</summary>
    public static readonly double[] PostureDamageMultipliers = [2.5, 3.5, 5.0, 6.5];
    /// <summary>Each valid hit during stagger also adds this fraction of the boss max HP...</summary>
    public const double PostureMaxHpBonusPct = 0.015;
    /// <summary>...but no more than once per this internal cooldown (anti multi-hit exploit).</summary>
    public const int PostureMaxHpBonusCooldownMs = 600;

    // ---- F-E: elemental reactions ----
    /// <summary>How long an element "mark" lingers on a target waiting for a second element.</summary>
    public const int ElementMarkDurationMs = 4000;
}

public sealed record DungeonTier(
    int Tier, string Name, string Description,
    string[] CommonMobs, string[] EliteMobs, string Boss,
    int RequiredAccountLevel, double StatMultiplier);

public sealed record MonsterStatLine(int Health, int Damage, int Armor, int Speed, int Experience);
public sealed record MonsterStatPreset(
    string Id, string Name, string Description,
    double HpMultiplier, double DamageMultiplier, double SpeedMultiplier, double CadenceMultiplier);
public sealed record MonsterElementProfile(
    string Id, string Name, int AreaEffect, int ShootEffect, string? ConditionType);
public sealed record MonsterAttackPattern(
    string Kind, int Range, int Radius, int Length, int Spread, bool Target,
    int IntervalMs, int Chance, double MinDamageScale, double MaxDamageScale,
    bool UsesElement, double ConditionDamageScale);
public sealed record MonsterBehaviorProfile(
    string Id, string Name, string Description, int TargetDistance, int StaticAttackChance,
    MonsterAttackPattern[] Attacks, double HealFraction = 0, int HealIntervalMs = 0);
