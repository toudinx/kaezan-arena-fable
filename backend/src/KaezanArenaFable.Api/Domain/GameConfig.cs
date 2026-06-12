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
    public const double MonsterDamageTuning = 0.35;
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

    // ---- death / rewards ----
    public const double DefeatGoldKeptFraction = 0.5;
    public const int VictoryKaerosBase = 120;
    public const int VictoryKaerosPerTier = 40;
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
    public const int StartingKaeros = 4000;
    public const int StartingGold = 500;
    public const int ItemFallbackSalePrice = 5;
    public static readonly Dictionary<int, int> DupeShards = new() { [3] = 5, [4] = 20, [5] = 50 };
    public static readonly int[] AscensionShardCost = [10, 15, 25, 40, 60, 80]; // A1..A6
    public const int AddonOneAscension = 2;
    public const int AddonTwoAscension = 4;

    // ---- dailies ----
    public const int DailyContractCount = 3;
    public const int DailyKaerosReward = 100;
    public const int DailyGoldReward = 150;
    public const long DailyAccountXpReward = 25;

    // ---- bestiary ----
    public static readonly long[] BestiaryRankKills = [10, 50, 100, 250];
    public const double BestiaryDamageBonusPerRank = 0.01;
}

public sealed record DungeonTier(
    int Tier, string Name, string Description,
    string[] CommonMobs, string[] EliteMobs, string Boss,
    int RequiredAccountLevel, double StatMultiplier);
