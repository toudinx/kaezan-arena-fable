using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

public enum Dir { North = 0, East = 1, South = 2, West = 3 }

public sealed class Actor
{
    public int Id;
    public bool IsPlayer;
    public MonsterType? Species;
    public int Floor;
    public int X, Y;
    public int FromX, FromY;
    public long StepStartMs;
    public int StepDurMs;
    public Dir Facing = Dir.South;
    public int Hp, MaxHp;
    public long StunUntilMs;
    public long[] AttackReadyAtMs = [];
    public long NextWanderAtMs;
    public long NextVoiceAtMs;
    public int TargetId;
    public long LastSawPlayerAtMs;
    public long AggroOutOfRangeSinceMs;
    public long ExposedUntilMs;
    public long SappedUntilMs;
    public long TauntedUntilMs;  // taunted (melee): drops kiting and marches into melee
    public bool IsBossActor;
    public bool IsElite;        // G-06: common-room elite: defeating it grants a choice beat
    public bool IsMimic;        // G-09: corrupted Echo chest: drops gear material on death
    public bool IsTrainingDummy; // Training Room: passive, huge-HP/regen target (no attack, no loot, respawns)
    public double StatMult = 1.0;

    // monster kit (T-53): reactive defenses, summon timers, self-haste
    public long[] DefenseReadyAtMs = [];
    public long[] SummonReadyAtMs = [];
    public int OwnerId; // > 0 when this actor was summoned by another monster
    public long HasteUntilMs;
    public double HasteFactor = 1.0;

    // slow applied by the player (trait chiller/shatter)
    public long SlowUntilMs;
    public double SlowFactor = 1.0;

    // G-08B: shieldbearer: echo barrier that absorbs damage before health (granted by an allied shieldbearer).
    public double MonsterShield;
    public long ShieldCastReadyAtMs; // shieldbearer personal cooldown between barrier grants

    // K-04 signature trait state carried per target.
    public int SinStacks;       // Eloa: Judgment Seal (3 = Judged, next hit detonates)
    public long SinUntilMs;
    public int DecayStacks;     // Velvet: Accumulated Curse (raises the execution threshold)
    public long DecayUntilMs;
    public int FrostHits;       // Lunara: Frostbite stacks until shatter
    public long FrostUntilMs;
    public bool IsPrey;         // Gaia: target marked as Prey (HUD/render)
    public bool HasStaticMark;   // Rynna: Static Charge mark, detonated by full Charge / Storm Heart
    public long StaticMarkUntilMs;
    public bool Killed;         // guard: KillMonster processes each death exactly once

    // DoT left on this monster by a player skill (necromancer wither / eternal suffering).
    public readonly List<MonsterDot> Dots = [];

    // F-E: boss posture (echo break). Only populated on boss actors (PostureMax > 0).
    public double PostureBaseMax;
    public double PostureMax;
    public double Posture;
    public int PostureCycle;
    public long StaggerUntilMs;
    public double StaggerMultiplier = 1.0;
    public long PostureLastHitMs;
    public long PostureBonusReadyAtMs;

    // F-E: elemental reaction mark (any actor).
    public string ElementMark = "";
    public long ElementMarkUntilMs;

    public bool IsSummon => OwnerId != 0;
    public bool IsStaggered(long nowMs) => nowMs < StaggerUntilMs;
    public string ActiveMark(long nowMs) => nowMs < ElementMarkUntilMs ? ElementMark : "";
    public bool IsMoving(long nowMs) => nowMs < StepStartMs + StepDurMs;
    public bool IsStunned(long nowMs) => nowMs < StunUntilMs;
}

/// <summary>A DoT ticking on the player (tibia condition applied by a monster attack).</summary>
public sealed class ActiveCondition
{
    public string Type = "";
    public int DamagePerTick;
    public int TicksLeft;
    public int TickMs;
    public long NextTickAtMs;
}

/// <summary>A DoT ticking on a monster, applied by a player skill (necromancer Wither/Eternal Suffering).</summary>
public sealed class MonsterDot
{
    public string Element = "";
    public int Fx;
    public double DamagePerTick;
    public int TicksLeft;
    public int TickMs;
    public long NextTickAtMs;
}

/// <summary>
/// A stationary construct summoned by the player (necromancer Bone Construct). It pulses area
/// damage around its tile for a while, then expires. Kept off the monster list so it never
/// interferes with player targeting/auto-attack; it is pure scheduled area damage.
/// </summary>
public sealed class PlayerSummon
{
    public int Floor, X, Y;
    public string Element = "";
    public int Fx;
    public int Radius;
    public double DamagePerPulse;
    public int PulseMs;
    public long NextPulseAtMs;
    public long ExpireAtMs;
    public bool IsEchoSpectre; // G-04: Harvest specter (Velvet), counted against the cap of 5.
    // Roaming shade (Velvet): drifts one tile toward the nearest enemy each pulse and, when
    // LeavesField, seeds a spreading field of its element on the tiles it crosses (corrosion trail).
    public bool Roams;
    public bool LeavesField;
    public double FieldPower;
    public int FieldTickMs, FieldLifeMs, FieldSpreadChance, FieldSpreadGenerations;
}

/// <summary>A hazard tile painted by a player skill (shape "field"): damages/slows the monster
/// standing on it each tick until it expires. Terrain modification: fire patch, frost patch, etc.</summary>
public sealed class GroundField
{
    public int Floor, X, Y;
    public string Element = "";
    public int Fx;
    public double DamagePerTick;
    public double SlowFactor = 1;
    public int SlowMs;
    public int TickMs;
    public long NextTickAtMs;
    public long ExpireAtMs;
    // Spreading terrain: chance% per tick to ignite a free neighbor; remaining generations limit
    // how far the fire crawls from the origin. 0 = static field (no propagation).
    public int SpreadChance;
    public int SpreadGenerationsLeft;
}

/// <summary>A future area hit scheduled by a multi-time skill (shape "barrage", delayed nukes,
/// expanding rings). Resolved deterministically when NowMs reaches AtMs. Damage/DoT are
/// pre-computed at cast time so they don't drift with buffs that change mid-flight.</summary>
public sealed class ScheduledStrike
{
    public int Floor, X, Y;
    public long AtMs;
    public string Element = "";
    public int Fx;
    public double Damage;
    public int Radius;
    public int RingInner;
    public int StunMs;
    public double SlowFactor = 1;
    public int SlowMs;
    public int DotTicks, DotTickMs;
    public double DotPower;
    // Fire trail: each barrage hit may leave a spreading field where it lands.
    public bool LeavesField;
    public double FieldPower;
    public int FieldRadius, FieldTickMs, FieldLifeMs, FieldSpreadChance, FieldSpreadGenerations;
    public double FieldSlowFactor = 1;
    public int FieldSlowMs;
    // Velvet Soul Detonation: a Death Orb burst (re-seeds Decay on survivors so it can cascade).
    public bool IsDeathOrb;
    // Rin Infernal Ball: this impact stacks the room-wide burn-damage multiplier.
    public bool StacksBurnMult;
    // Rynna Storm Heart: scheduled nova waves detonate Static marks in their footprint.
    public bool DetonatesStaticMarks;
    public double SkillLifesteal;
    public double StaticChargeGain;
}

public sealed class GroundItem
{
    public int Id;
    public int Floor, X, Y;
    public int ItemId;
    public int Count;
    public string Name = "";
}

public sealed class Poi
{
    public int Id;
    public string Kind = "chest"; // chest | sanctuary | ladder
    // G-09: chest variant: "" (normal) | "cursed" (telegraphed) | "mimic" (hidden from the client).
    public string Variant = "";
    public int Floor, X, Y;
    public bool Used;
}

public enum CommandKind { SetMoveDir, SetTarget, CastSkill, ToggleStance, ToggleAutoHelper, Interact, ChooseCard, RerollCards, BanCard, Abandon, UsePotion, Dash, SetTrainingFreeCast }

public sealed record Command(CommandKind Kind, int A, int B, string? S);

/// <summary>
/// One dungeon run: server-authoritative world ticked at GameConfig.TickMs.
/// Movement, monster AI, combat, loot and run-cards all live here.
/// Deterministic for a given seed + command timeline.
/// </summary>
/// Persistence belongs to Meta and is only invoked at run boundaries; never add DB/EF access here.
public sealed class GameWorld
{
    public readonly long Seed;
    public readonly GameMode Mode;
    public readonly DungeonTier Tier;
    public readonly WaifuDef Waifu;
    public readonly ClassDef PlayerClass;
    public readonly int Ascension;
    public readonly EquipmentStats EquipmentStats;
    public readonly KaeliLoadout Loadout;
    private readonly TraitDef _trait;
    private readonly double _traitMult;        // trait amplification from mastery (Echo branch)
    private readonly double _affinityStatBonus; // +1% ATK/HP per affinity level above 1
    private readonly double _gaugeRate;        // mastery x trait overcharge
    private readonly GameData _data;
    private readonly ItemRegistry? _items;
    private readonly MonsterRegistry _monsterRegistry;
    private readonly Rng _rng;
    // LM-03: mode stitching: locates map source, population, and end condition.
    private readonly GameModeStrategy _modeRules;
    private readonly IReadOnlyDictionary<string, long> _bestiaryKills;
    // MG-05: active role-tuning table for this run (injected by the Hub from ContentStore).
    private readonly IReadOnlyDictionary<KaeliRole, RoleTuning> _roles;
    // LM-08: biome resolved ONCE at construction (injected by the Hub from ContentStore; fallback to
    // canonical defaults). Never reread in the tick: determinism preserved (same as RoleTuning).
    private readonly BiomeDef _biome;

    public long TickCount { get; private set; }
    private long _simulationMs;
    private long NowMs => _simulationMs;

    private readonly DungeonFloor[] _floors;
    private int _currentFloor;
    private int _nextActorId = 1;

    public Actor Player { get; }
    private readonly List<Actor> _monsters = [];
    private readonly List<GroundItem> _groundItems = [];
    private readonly List<Poi> _pois = [];
    private readonly List<EventDto> _events = [];
    private readonly Queue<Command> _commands = new();
    private readonly object _commandLock = new();

    // run progression
    private int _runLevel = 1;
    private long _runXp;
    private long _gold;
    private int _kills;
    private double _gauge;
    private readonly Dictionary<string, int> _cards = [];
    private readonly HashSet<string> _bannedCards = new(StringComparer.Ordinal);
    private List<CardOfferDto>? _pendingOffer;
    private long _cardOfferStartedTick;
    private int _queuedOffers;
    private int _choicesOffered; // G-06: card choices already granted by beats (cap + progress)
    private bool _offerBlessed;  // G-09: blessed offer (cursed chest): weights rare/echo higher
    private int _cardRerollsRemaining = GameConfig.CardRerollsPerRun;
    public Dictionary<string, int> KillsBySpecies { get; } = [];
    public List<RewardItemDto> ItemsLooted { get; } = [];
    private int _potionCharges = GameConfig.PotionChargesPerRun;
    private long _potionReadyAtMs;
    private int _chestsOpened;
    public int ChestsOpened => _chestsOpened;

    // player combat state
    private readonly long[] _skillReadyAtMs = new long[4];
    private string _stanceId;
    private long _autoAttackReadyAtMs;
    // Dash/Dodge (Shift): cooldown and i-frame window. Shared between manual input and helper.
    private long _dashReadyAtMs;
    private long _dashInvulnUntilMs;
    // Dash signature: which role-keyed dash the player uses (movement style + payoff). Resolved from the
    // Kaeli's role at construction; a future card/mastery may reassign it ("dash evolution"). See PerformDash.
    private enum DashSignature { Cleave, Sprint, Trail }
    private readonly DashSignature _dashSignature;
    // Archer sprint: brief move-speed haste granted by the dash (Sprint signature). Applied in PlayerSpeed.
    private long _dashHasteUntilMs;
    private double _dashHasteFactor = 1.0;
    private bool _autoHelperTargeting = true;
    private bool _autoHelperSkills = true;
    private bool _autoHelperUltimate = true;
    // Training Room only: when on, skills and the ultimate ignore cooldown/gauge so kits (and FX of the
    // never-otherwise-reachable ult) can be spammed. Guarded by Mode so it can never leak into real runs.
    private bool _trainingFreeCast;
    private bool FreeCast => _trainingFreeCast && Mode == GameMode.Training;
    private string _autoHelperTargetPreference = GameConfig.AutoHelperTargetPreferenceNearest;
    private string _autoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
    private string _savedAutoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
    private string _defaultAutoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
    // G-10: helper automations (autoplay style): enabled by default. Auto-heal uses the potion
    // when health drops below _autoHelperHealPct%; navMode ("off"/"loot") makes the helper walk
    // by itself while collecting chests/altars and heading to the exit; autoCards takes the highest-rarity card.
    private bool _autoHelperAutoHeal = true;
    private int _autoHelperHealPct = GameConfig.AutoHelperHealPctDefault;
    private string _autoHelperNavMode = GameConfig.AutoHelperNavLoot;
    private bool _autoHelperAutoCards = true;
    // marks when the current floor started: auto-loot waits AutoLootStartDelayMs before walking.
    private long _floorEnteredMs;
    // orbit-mobbing: previous tile, so it does not step back onto the same SQM (anti-stutter for kiting).
    private int _mobLastX = int.MinValue, _mobLastY = int.MinValue;
    // dynamic loot: kill counter since the last dropped chest (resets per floor).
    private int _killsSinceChest;
    // next free POI id (continues the SpawnPois sequence for runtime-created chests/teleports).
    private int _nextPoiId = 1;
    private int _helperMovementOverrideTargetId;
    private int _manualTargetId;
    private int _moveDirX, _moveDirY; // held movement direction (-1..1)
    private int _bufferedMoveDirX, _bufferedMoveDirY;
    private bool _hasBufferedMoveDir;
    private long _moveDirChangedAtMs;
    private readonly Dictionary<string, long> _buffsUntilMs = [];
    private double _regenCarry;
    private readonly List<ActiveCondition> _playerConditions = [];
    private readonly List<PlayerSummon> _summons = [];
    private readonly List<GroundField> _fields = [];
    private readonly List<ScheduledStrike> _pendingStrikes = [];
    // True while a Death Orb burst is being resolved, so its kills do not spawn more orbs (no cascade).
    private bool _resolvingDeathOrb;
    private long _playerSlowUntilMs;
    private double _playerSlowFactor = 1.0;

    // K-04 signature trait state carried per Kaeli (the player side of the passive).
    private int _comboTargetId;          // Seren: Discipline, current ramp target
    private int _comboHits;              // consecutive hits on the target
    private long _comboExpireMs;         // resets the ramp if too much time passes without hitting
    private double _staticCharge;        // Rynna: Static Charge (0..RynnaChargeMax)
    private int _preyId;                 // Gaia: Prey, marked target id
    private long _preyStartMs;           // hunt start (time ramp)
    private long _preyHuntBonusUntilMs;  // cadence window after an execution
    private long _traitHasteUntilMs;     // Lunara: Shatter haste (move speed)
    private double _traitHasteFactor = 1.0;
    private long _contagionNextJumpMs;   // Rin: Contagion, next periodic burn jump
    private int _rinBurnMultStacks;      // Rin: Infernal Ball, room-wide burn-damage multiplier stacks
    private long _rinBurnMultNextDecayMs; // when the next stack bleeds off
    private int _cardDoubleStrikeHits;   // G-04: Double Strike direct-hit counter

    // KR-00: shared auto-modifier state (empowered autoattack). Armed by a buff-shaped skill; consumed
    // per auto in TickPlayerCombat. Dormant until a Kaeli kit wires an AutoModKind (Seren/Lunara/Gaia).
    private string? _autoModKind;        // null = inactive; "cleave" | "pierce" | "lock_pierce"
    private long _autoModUntilMs;        // window expiry (also a safety cap for charge-based mods)
    private int _autoModChargesLeft;     // remaining empowered autos (charge-based only)
    private int _autoModMaxCharges;      // 0 = time-windowed; > 0 = charge-based (reset-on-kill cap)
    private bool _autoModResetOnKill;    // a kill during the window refunds a charge, up to the cap

    // G-04B: live Echo state per Kaeli (1-stack cap; presence = HasEcho).
    private double _echoShield;          // Eloa Martyr / Velvet Pact: overheal shield that absorbs damage
    private int _eloaSentenceStacks;     // Eloa Sentence: accumulated amplification for the next burst
    private int _preyId2;                // Gaia Pack: second simultaneous Prey

    public RunEndDto? Ended { get; private set; }
    public bool MapDirty { get; private set; } = true;

    /// <summary>
    /// MG-01 (tools/BalanceSim): monster rank by id: "common" | "elite" | "boss" for the
    /// Lets the simulator classify TTK without inferring from the dungeon pool. Pure read: touches no state
    /// or <c>_rng</c>, so it does not perturb determinism. Dead actors are not removed from the list
    /// (they stay with <c>Killed=true</c>), so the rank remains queryable on the death tick.
    /// </summary>
    public string? MonsterRank(int monsterId) =>
        _monsters.FirstOrDefault(m => m.Id == monsterId) is { } m
            ? m.IsBossActor ? "boss" : m.IsElite ? "elite" : "common"
            : null;

    /// <summary>
    /// MG-08 (tools/BalanceSim): true se o monstro foi conjurado por outro (OwnerId != 0). O simulador
    /// excludes summons from TTK calibration: they are transient adds (for example, a summoner creates T1 Echoids
    /// in any tier), not that cell's common/elite/boss, and would pollute the median. Pure read.
    /// </summary>
    public bool IsSummonedMonster(int monsterId) =>
        _monsters.FirstOrDefault(m => m.Id == monsterId) is { IsSummon: true };

    public GameWorld(long seed, DungeonTier tier, WaifuDef waifu, int ascension,
        GameData data, MonsterRegistry monsterRegistry, IReadOnlyDictionary<string, long> bestiaryKills,
        EquipmentStats? equipmentStats = null, KaeliLoadout? loadout = null, ItemRegistry? items = null,
        string? helperProfile = null, IReadOnlyDictionary<KaeliRole, RoleTuning>? roleTuning = null,
        GameMode mode = GameMode.Dungeon, BiomeDef? biome = null)
    {
        Seed = seed;
        Mode = mode;
        _modeRules = GameModeStrategy.For(mode);
        Tier = tier;
        Waifu = waifu;
        // MG-05: the run reads the injected active table (editable in admin); falls back to
        // GameConfig.Roles when nothing is passed (for example, simulator measurements against the stable baseline).
        _roles = roleTuning ?? GameConfig.Roles;
        PlayerClass = Classes.ById.GetValueOrDefault(waifu.ClassId)
                      ?? throw new InvalidOperationException($"classe desconhecida: {waifu.ClassId}");
        _stanceId = PlayerClass.InitialStance(waifu.Element).Id;
        Ascension = ascension;
        EquipmentStats = equipmentStats ?? Domain.EquipmentStats.Empty;
        Loadout = loadout ?? KaeliLoadout.Default(waifu);
        _trait = waifu.Trait;
        _traitMult = Loadout.Mastery.TraitMult;
        _affinityStatBonus = (Loadout.AffinityLevel - 1) * GameConfig.AffinityStatBonusPerLevel;
        _gaugeRate = Loadout.Mastery.GaugeMult
                     * (_trait.Kind is "overcharge" or "static_charge" ? 1 + _trait.Value * _traitMult : 1);
        _data = data;
        _items = items;
        _monsterRegistry = monsterRegistry;
        _bestiaryKills = bestiaryKills;
        _rng = new Rng((ulong)seed);
        // MG-02: default movement (follow vs kite) follows role range, not the weapon anymore.
        var isMelee = _roles[Waifu.Role].AutoRange <= GameConfig.MeleeRange;
        // Dash signature is innate to the role (evolution hook: a card could reassign this later).
        _dashSignature = Waifu.Role switch
        {
            KaeliRole.Knight => DashSignature.Cleave,
            KaeliRole.Archer => DashSignature.Sprint,
            _ => DashSignature.Trail, // Mage
        };
        _autoHelperTargetPreference = GameConfig.AutoHelperTargetPreferenceNearest;
        _defaultAutoHelperMovementMode = isMelee
            ? GameConfig.AutoHelperMovementModeFollow
            : GameConfig.AutoHelperMovementModeAvoid;
        _autoHelperMovementMode = _defaultAutoHelperMovementMode;
        if (waifu.ClassId == Classes.StormcallerId)
        {
            _autoHelperAutoHeal = GameConfig.RynnaHelperAutoHealDefault;
            _autoHelperHealPct = GameConfig.RynnaHelperHealPctDefault;
        }
        if (!string.IsNullOrWhiteSpace(helperProfile)) ApplyHelperProfile(helperProfile);

        // LM-08: biome comes from ContentStore (editable in admin); fallback to canonical defaults for
        // callers without a store (for example, the simulator measuring against the stable baseline). Resolved here once.
        _biome = biome ?? Biomes.ForTier(tier.Tier);
        // LM-03 (1) map source: the mode decides how the place is produced (same Rng sequence in Dungeon).
        _floors = _modeRules.BuildFloors(_rng, _biome);

        var hp = (int)(waifu.BaseHp * (1 + ascension * GameConfig.AscensionAtkBonus)
                       * (1 + _affinityStatBonus) * Loadout.Mastery.HpMult)
                 + EquipmentStats.MaxHpBonus;
        Player = new Actor
        {
            Id = _nextActorId++,
            IsPlayer = true,
            Floor = 0,
            X = _floors[0].Entry.X,
            Y = _floors[0].Entry.Y,
            FromX = _floors[0].Entry.X,
            FromY = _floors[0].Entry.Y,
            Hp = hp,
            MaxHp = hp
        };

        // LM-03 (2) population: the mode decides pre-spawn (rooms in Dungeon; waves in Arena).
        _modeRules.Populate(this);
    }

    // ================= spawn =================

    internal void SpawnFloorMonsters(int floorIndex)
    {
        var floor = _floors[floorIndex];
        foreach (var room in floor.Rooms)
        {
            switch (room.Role)
            {
                case "entry": continue;
                // G-06: the Echo Sanctuary is safe: the player claims the card without a fight.
                case "sanctuary": continue;
                case "boss":
                    SpawnBossRoom(floorIndex, room);
                    continue;
                // G-07: miniboss = detour mini-climax (1 reinforced elite + escort).
                case "miniboss":
                    SpawnMiniBossRoom(floorIndex, room);
                    continue;
            }

            // echo-spots style budget spawn: commons cost 2, elites cost 5
            // H-01: the area factor scales room.W*room.H against the base room (SpawnRoomAreaBaseline) and is
            // clamped: with the large H-01 rooms, the cap rose so the room does not read empty (the pile fits).
            var sizeFactor = room.W * room.H / GameConfig.SpawnRoomAreaBaseline;
            var budget = (int)(GameConfig.SpawnBudgetBase * (1 + (Tier.Tier - 1) * GameConfig.SpawnBudgetTierGrowth)
                * Math.Clamp(sizeFactor, GameConfig.SpawnBudgetSizeClampMin, GameConfig.SpawnBudgetSizeClampMax));
            // G-07: treasure = fewer guards; event/risk = more guards (swarm); elite = elite pack.
            if (room.Role == "treasure") budget = budget / 2 + 2;
            else if (room.Role == "hazard") budget = (int)(budget * GameConfig.HazardBudgetMult);
            var forceElite = room.Role == "elite";
            var elitesSpawned = 0;
            var guard = 0;
            while (budget > 0 && guard++ < 50)
            {
                var elite = forceElite
                    ? budget >= 5 && elitesSpawned < GameConfig.EliteRoomMaxElites
                    : budget >= 5 && _rng.Chance(0.25);
                var name = elite ? _rng.Pick(Tier.EliteMobs) : _rng.Pick(Tier.CommonMobs);
                // G-06: only common-room elites become beats (boss guards/ambushes do not count).
                // G-08B: common cost comes from the profile (swarm = 1 doubles count, numeric pressure).
                var cost = elite ? 5 : SpawnCostFor(name);
                if (SpawnMonster(floorIndex, name, room, isElite: elite) is not null)
                {
                    budget -= cost;
                    if (elite) elitesSpawned++;
                }
                else break;
            }
        }
    }

    /// <summary>G-07: miniboss room: one reinforced elite (choice beat on death) + common escort.</summary>
    private void SpawnMiniBossRoom(int floorIndex, Room room)
    {
        var mini = SpawnMonster(floorIndex, _rng.Pick(Tier.EliteMobs), room, isElite: true);
        if (mini is not null)
            mini.MaxHp = mini.Hp = (int)(mini.Hp * GameConfig.MiniBossHpScale);
        for (var i = 0; i < GameConfig.MiniBossEscort; i++)
            SpawnMonster(floorIndex, _rng.Pick(Tier.CommonMobs), room, isElite: false);
    }

    private void SpawnBossRoom(int floorIndex, Room room)
    {
        var boss = SpawnMonster(floorIndex, Tier.Boss, room, isBoss: true);
        if (boss is not null)
        {
            if (!boss.Species!.IsAuthored)
                boss.MaxHp = boss.Hp = (int)(boss.Hp * GameConfig.BossHpScale(Tier.Boss));
            boss.PostureBaseMax = GameConfig.PostureBaseMax * (1 + (Tier.Tier - 1) * GameConfig.PostureTierGrowth);
            boss.PostureMax = boss.PostureBaseMax;
            // boss at the BACK of the arena (top, opposite the lower entry): the player enters and advances toward it.
            var (bx, by) = OpenTileNear(floorIndex, room.CenterX, room.Y + 2);
            boss.X = boss.FromX = bx;
            boss.Y = boss.FromY = by;
        }
        // elite escort + common chaff around the boss: the smaller chamber keeps everything close to raise difficulty.
        for (var i = 0; i < 2 + Tier.Tier / 2; i++)
            SpawnMonster(floorIndex, _rng.Pick(Tier.EliteMobs), room, isElite: true);
        for (var i = 0; i < 2 + Tier.Tier / 2; i++)
            SpawnMonster(floorIndex, _rng.Pick(Tier.CommonMobs), room);
    }

    /// <summary>Nearest open and unoccupied tile to (x,y) on the floor (Chebyshev ring), used to anchor a spawn.</summary>
    private (int X, int Y) OpenTileNear(int floorIndex, int x, int y)
    {
        var floor = _floors[floorIndex];
        for (var r = 0; r <= 10; r++)
            for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!floor.IsBlocked(nx, ny) && OccupiedBy(floorIndex, nx, ny) is null) return (nx, ny);
                }
        return (x, y);
    }

    private Actor? SpawnMonster(int floorIndex, string speciesName, Room room, bool isBoss = false, bool isElite = false)
    {
        var species = _monsterRegistry.Get(speciesName);
        var floor = _floors[floorIndex];
        var (entryX, entryY) = floor.Entry;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var x = _rng.Range(room.X, room.X + room.W - 1);
            var y = _rng.Range(room.Y, room.Y + room.H - 1);
            if (floor.IsBlocked(x, y) || OccupiedBy(floorIndex, x, y) is not null) continue;
            // do not spawn on top of the entry (the Kaeli does not start buried); relax on the final attempts.
            if (!isBoss && attempt < 16 && Chebyshev(x, y, entryX, entryY) < GameConfig.SpawnEntrySafeRadius) continue;

            var mult = species.IsAuthored ? 1 : Tier.StatMultiplier;
            var actor = new Actor
            {
                Id = _nextActorId++,
                Species = species,
                Floor = floorIndex,
                X = x, Y = y, FromX = x, FromY = y,
                Hp = (int)(species.Health * mult),
                MaxHp = (int)(species.Health * mult),
                AttackReadyAtMs = new long[Math.Max(species.Attacks.Count, 1)],
                DefenseReadyAtMs = new long[species.Defenses.Count],
                SummonReadyAtMs = new long[species.Summons.Count],
                NextWanderAtMs = _rng.Range(0, GameConfig.MonsterWanderIntervalMs),
                IsBossActor = isBoss,
                IsElite = isElite,
                StatMult = mult,
                Facing = (Dir)_rng.Next(4)
            };
            // G-08B: posture tank: common/elite mob gains a Posture bar (Echo Break) scaled by profile.
            if (!isBoss && GameConfig.BehaviorProfile(species.BehaviorId) is { PostureScale: > 0 } postureProfile)
            {
                actor.PostureBaseMax = GameConfig.PostureBaseMax * postureProfile.PostureScale
                                       * (1 + (Tier.Tier - 1) * GameConfig.PostureTierGrowth);
                actor.PostureMax = actor.PostureBaseMax;
            }
            _monsters.Add(actor);
            return actor;
        }
        return null;
    }

    /// <summary>Training Room: one passive dummy at the arena centre — a DEDICATED practice creature
    /// (KaezanContentSeed.TrainingDummyId), not a dungeon mob or boss. Huge HP + regen (TickTrainingDummy) so it
    /// can be wailed on forever; never attacks, never chases, drops nothing. Re-callable (TrainingModeStrategy
    /// respawns it if it ever dies).</summary>
    internal void SpawnTrainingDummy()
    {
        var floor = _floors[0];
        var room = floor.Rooms[0];
        var species = _monsterRegistry.Get(KaezanContentSeed.TrainingDummyId);
        var (cx, cy) = (room.CenterX, room.CenterY);
        if (floor.IsBlocked(cx, cy) || OccupiedBy(0, cx, cy) is not null)
            (cx, cy) = OpenTileNear(0, cx, cy);
        _monsters.Add(new Actor
        {
            Id = _nextActorId++,
            Species = species,
            Floor = 0,
            X = cx, Y = cy, FromX = cx, FromY = cy,
            Hp = GameConfig.TrainingDummyHp,
            MaxHp = GameConfig.TrainingDummyHp,
            AttackReadyAtMs = new long[Math.Max(species.Attacks.Count, 1)],
            DefenseReadyAtMs = new long[species.Defenses.Count],
            SummonReadyAtMs = new long[species.Summons.Count],
            IsTrainingDummy = true,
            Facing = Dir.North,
        });
    }

    /// <summary>Per-tick behaviour of the training dummy: regenerates toward full (so the HP bar recovers
    /// between bursts) and faces the player. It never moves or attacks — the AI loop short-circuits here.</summary>
    private void TickTrainingDummy(Actor dummy)
    {
        if (dummy.Hp < dummy.MaxHp)
        {
            var regen = (int)(dummy.MaxHp * GameConfig.TrainingDummyRegenPctPerSec * (GameConfig.TickMs / 1000.0));
            dummy.Hp = Math.Min(dummy.MaxHp, dummy.Hp + Math.Max(regen, 1));
        }
        dummy.Facing = FacingFrom(Player.X - dummy.X, Player.Y - dummy.Y);
    }

    /// <summary>G-08B: common spawn budget cost (swarm costs 1, filling the room with chaff).</summary>
    private int SpawnCostFor(string speciesName) =>
        GameConfig.BehaviorProfile(_monsterRegistry.Get(speciesName).BehaviorId)?.SpawnCost ?? 2;

    internal void SpawnPois()
    {
        var nextPoi = 1;
        for (var f = 0; f < _floors.Length; f++)
        {
            foreach (var (cx, cy) in _floors[f].Chests)
            {
                // G-09: each chest rolls a deterministic variant: mimic (hidden) or cursed
                // (telegraphed), otherwise a normal altar. Deterministic via run _rng (always 2 rolls, fixed order).
                // Benefit chests (strategic arena chests) are NEVER mimics: they give rewards, at most
                // cursed (ambush + blessed offer). Mimics (pure trap) stay for the others only.
                var mimicRoll = _rng.Chance(GameConfig.ChestMimicChance);
                var cursedRoll = _rng.Chance(GameConfig.ChestCursedChance);
                var noMimic = _floors[f].BenefitChests.Contains((cx, cy));
                var variant = (!noMimic && mimicRoll) ? "mimic" : cursedRoll ? "cursed" : "";
                _pois.Add(new Poi { Id = nextPoi++, Kind = "chest", Variant = variant, Floor = f, X = cx, Y = cy });
            }
            foreach (var (sx, sy) in _floors[f].Sanctuaries)
                _pois.Add(new Poi { Id = nextPoi++, Kind = "sanctuary", Floor = f, X = sx, Y = sy });
            if (_floors[f].LadderDown is { } ladder)
                _pois.Add(new Poi { Id = nextPoi++, Kind = "ladder", Floor = f, X = ladder.X, Y = ladder.Y });
        }
        _nextPoiId = nextPoi; // runtime-created chests/teleports continue the id sequence.
    }

    /// <summary>Creates a POI on the current floor at runtime (death-dropped chest, exit teleport) and marks the
    /// map dirty so the client rerenders the sprite. Deterministic (tick state only).</summary>
    private void AddRuntimePoi(string kind, string variant, int x, int y)
    {
        _pois.Add(new Poi { Id = _nextPoiId++, Kind = kind, Variant = variant, Floor = _currentFloor, X = x, Y = y });
        MapDirty = true;
    }

    /// <summary>Is the current floor the boss floor? (the single boss arena has its room marked "boss").</summary>
    private bool IsBossFloor() => Floor.Rooms.Any(r => r.Role == "boss");

    private int CountAliveOnFloor() => _monsters.Count(m => m.Hp > 0 && m.Floor == _currentFloor);

    private bool LadderExistsOnFloor() => _pois.Any(p => p.Floor == _currentFloor && p.Kind == "ladder");

    // ================= commands =================

    public void Enqueue(Command cmd)
    {
        lock (_commandLock) _commands.Enqueue(cmd);
    }

    private void DrainCommands(bool cardPauseAtStart)
    {
        var cardPause = cardPauseAtStart;
        while (true)
        {
            Command cmd;
            lock (_commandLock)
            {
                if (_commands.Count == 0) return;
                cmd = _commands.Dequeue();
            }

            // G-10: ToggleAutoHelper is a config change (does not touch the paused simulation), so let it
            // pass even during a card offer; otherwise HELPER panel adjustments are swallowed.
            if (cardPause && cmd.Kind is not (CommandKind.SetMoveDir or CommandKind.ChooseCard or CommandKind.RerollCards or CommandKind.BanCard or CommandKind.Abandon or CommandKind.ToggleAutoHelper))
                continue;

            Apply(cmd);
            cardPause |= _pendingOffer is not null;
        }
    }

    private void Apply(Command cmd)
    {
        if (Ended is not null) return;
        switch (cmd.Kind)
        {
            case CommandKind.SetMoveDir:
                SetMoveDirection(cmd.A, cmd.B);
                break;
            case CommandKind.SetTarget:
                var target = _monsters.FirstOrDefault(m => m.Id == cmd.A && m.Hp > 0);
                Player.TargetId = target?.Id ?? 0;
                _manualTargetId = Player.TargetId;
                break;
            case CommandKind.CastSkill:
                TryCastSkill(cmd.A);
                break;
            case CommandKind.UsePotion:
                TryUsePotion();
                break;
            case CommandKind.Dash:
                TryDash(cmd.A, cmd.B);
                break;
            case CommandKind.ToggleStance:
                ToggleStance();
                break;
            case CommandKind.ToggleAutoHelper:
                _autoHelperTargeting = (cmd.A & 1) != 0;
                _autoHelperSkills = (cmd.A & 2) != 0;
                _autoHelperUltimate = (cmd.A & 4) != 0;
                _autoHelperAutoHeal = (cmd.A & GameConfig.AutoHelperAutoHealFlag) != 0;
                _autoHelperAutoCards = (cmd.A & GameConfig.AutoHelperAutoCardsFlag) != 0;
                _autoHelperMovementMode = NormalizeAutoHelperMovementMode(cmd.B);
                // S carrega "targetPreference|navMode|healPct".
                var parts = (cmd.S ?? "").Split('|');
                _autoHelperTargetPreference = NormalizeAutoHelperTargetPreference(parts.Length > 0 ? parts[0] : null);
                _autoHelperNavMode = GameConfig.NormalizeAutoHelperNav(parts.Length > 1 ? parts[1] : null);
                if (parts.Length > 2 && int.TryParse(parts[2], out var healPct))
                    _autoHelperHealPct = GameConfig.ClampHealPct(healPct);
                _helperMovementOverrideTargetId = 0;
                if (!_autoHelperTargeting && _manualTargetId == 0)
                    Player.TargetId = 0;
                break;
            case CommandKind.Interact:
                TryInteract(cmd.A, cmd.B);
                break;
            case CommandKind.ChooseCard:
                ChooseCard(cmd.S ?? "");
                break;
            case CommandKind.RerollCards:
                RerollCards();
                break;
            case CommandKind.BanCard:
                BanCard(cmd.S ?? "");
                break;
            case CommandKind.SetTrainingFreeCast:
                if (Mode == GameMode.Training) _trainingFreeCast = cmd.A != 0;
                break;
            case CommandKind.Abandon:
                EndRun(false, "abandoned");
                break;
        }
    }

    // ================= tick =================

    public (SnapshotDto Snapshot, MapDto? Map) Tick()
    {
        TickCount++;
        _events.Clear();

        if (Ended is null)
        {
            var cardPauseAtStart = _pendingOffer is not null;
            if (!cardPauseAtStart)
                _simulationMs += GameConfig.TickMs;

            DrainCommands(cardPauseAtStart);

            // G-10: auto-pick chooses the highest-rarity card by itself (after a short flash
            // on screen), so autoplay/cavebot does not stall on offers. Otherwise, wait for timeout/player.
            if (Ended is null && _pendingOffer is not null && _autoHelperAutoCards
                && (TickCount - _cardOfferStartedTick) * GameConfig.TickMs >= GameConfig.AutoCardPickDelayMs)
                ChooseCard(BestOfferCardId());
            else if (Ended is null && _pendingOffer is not null
                && (TickCount - _cardOfferStartedTick) * GameConfig.TickMs >= GameConfig.CardOfferTimeoutMs)
                ChooseCard(_pendingOffer[0].Id);

            if (Ended is null && _pendingOffer is null)
            {
                if (cardPauseAtStart)
                    _simulationMs += GameConfig.TickMs;

                TickPlayerMovement();
                TickAutoHelper();
                TickPlayerCombat();

                if (_pendingOffer is null)
                {
                    TickPlayerRegen();
                    TickPlayerConditions();
                    TickMonsters();
                    // LM-03 (2b) continuous population: mode wave scheduler (no-op in Dungeon).
                    _modeRules.OnTick(this);
                    TickPlayerSummons();
                    TickFields();
                    TickPendingStrikes();
                    TickPostureDecay();
                    TickTraitTimers();
                }
            }
        }

        MapDto? map = null;
        if (MapDirty)
        {
            map = BuildMap();
            MapDirty = false;
        }
        return (BuildSnapshot(), map);
    }

    // ---- movement ----

    private DungeonFloor Floor => _floors[_currentFloor];

    private Actor? OccupiedBy(int floor, int x, int y)
    {
        if (Player.Floor == floor && Player.X == x && Player.Y == y && Player.Hp > 0) return Player;
        return _monsters.FirstOrDefault(m => m.Floor == floor && m.X == x && m.Y == y && m.Hp > 0);
    }

    private void SetMoveDirection(int dx, int dy)
    {
        dx = Math.Clamp(dx, -1, 1);
        dy = Math.Clamp(dy, -1, 1);
        if (dx == _moveDirX && dy == _moveDirY) return;

        _moveDirX = dx;
        _moveDirY = dy;
        _moveDirChangedAtMs = NowMs;

        // When player manually moves while follow/avoid is active, pause helper movement until target dies
        if ((dx != 0 || dy != 0) && _helperMovementOverrideTargetId == 0
            && _autoHelperMovementMode != GameConfig.AutoHelperMovementModeNone)
        {
            var target = CurrentPlayerTarget();
            if (target is not null)
            {
                _savedAutoHelperMovementMode = _autoHelperMovementMode;
                _autoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
                _helperMovementOverrideTargetId = target.Id;
            }
        }

        var remaining = Player.StepStartMs + Player.StepDurMs - NowMs;
        if (Player.IsMoving(NowMs) && remaining <= GameConfig.StepGraceMs)
        {
            _bufferedMoveDirX = dx;
            _bufferedMoveDirY = dy;
            _hasBufferedMoveDir = true;
        }
        else
        {
            _hasBufferedMoveDir = false;
        }
    }

    private int StepDuration(int speed, bool diagonal)
    {
        var ms = 1000.0 * GameConfig.GroundFriction / Math.Max(speed, 30);
        if (diagonal) ms *= GameConfig.DiagonalStepFactor;
        return (int)Math.Clamp(ms, GameConfig.MinStepMs, GameConfig.MaxStepMs);
    }

    private static Dir FacingFrom(int dx, int dy, Dir? previous = null)
    {
        if (dx != 0 && dy != 0 && previous is Dir.North or Dir.South)
            return dy >= 0 ? Dir.South : Dir.North;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? Dir.East : Dir.West)
            : (dy >= 0 ? Dir.South : Dir.North);
    }

    private bool CanStep(Actor actor, int dx, int dy)
    {
        if (dx == 0 && dy == 0) return false;
        var nx = actor.X + dx;
        var ny = actor.Y + dy;
        var floor = _floors[actor.Floor];
        if (dx != 0 && dy != 0
            && (floor.IsBlocked(actor.X + dx, actor.Y)
                || floor.IsBlocked(actor.X, actor.Y + dy)))
            return false;
        return !floor.IsBlocked(nx, ny) && OccupiedBy(actor.Floor, nx, ny) is null;
    }

    private bool TryStep(Actor actor, int dx, int dy, int speed, long? stepStartMs = null)
    {
        if (!CanStep(actor, dx, dy)) return false;

        actor.FromX = actor.X;
        actor.FromY = actor.Y;
        actor.X += dx;
        actor.Y += dy;
        actor.StepStartMs = stepStartMs ?? NowMs;
        actor.StepDurMs = StepDuration(speed, dx != 0 && dy != 0);
        actor.Facing = FacingFrom(dx, dy, actor.Facing);
        return true;
    }

    private static void SettleStep(Actor actor)
    {
        actor.FromX = actor.X;
        actor.FromY = actor.Y;
        actor.StepDurMs = 0;
    }

    private int PlayerSpeed()
    {
        var speed = GameConfig.PlayerBaseSpeed
                    * (1 + EquipmentStats.MoveSpeedPercent + CardValue("moveSpeedPercent"));
        if (IsBuffActive("haste")) speed *= 1.30;
        if (NowMs < _traitHasteUntilMs) speed *= _traitHasteFactor; // Lunara: Shatter haste
        if (NowMs < _dashHasteUntilMs) speed *= _dashHasteFactor;   // Archer: Sprint dash haste
        if (NowMs < _playerSlowUntilMs) speed *= _playerSlowFactor;
        return (int)speed;
    }

    private int MonsterSpeed(Actor monster)
    {
        var speed = monster.Species!.Speed * GameConfig.MonsterSpeedMultiplier;
        if (NowMs < monster.HasteUntilMs) speed = (int)(speed * monster.HasteFactor);
        if (NowMs < monster.SlowUntilMs) speed = (int)(speed * monster.SlowFactor);
        return speed;
    }

    private void TickPlayerMovement()
    {
        if (Player.Hp <= 0 || Player.IsMoving(NowMs)) return;
        if (Player.IsStunned(NowMs))
        {
            SettleStep(Player);
            return;
        }

        var dx = _hasBufferedMoveDir ? _bufferedMoveDirX : _moveDirX;
        var dy = _hasBufferedMoveDir ? _bufferedMoveDirY : _moveDirY;
        _hasBufferedMoveDir = false;
        if (dx == 0 && dy == 0)
        {
            SettleStep(Player);
            return;
        }

        var previousStepEndMs = Player.StepStartMs + Player.StepDurMs;
        var canChain = Player.StepDurMs > 0
                       && previousStepEndMs <= NowMs
                       && (_moveDirChangedAtMs <= previousStepEndMs
                           || NowMs - previousStepEndMs <= GameConfig.StepGraceMs);
        var stepStartMs = canChain ? previousStepEndMs : NowMs;
        var speed = PlayerSpeed();

        // try the held direction; if diagonal is blocked, slide along an axis
        if (!TryStep(Player, dx, dy, speed, stepStartMs))
        {
            if (dx != 0 && TryStep(Player, dx, 0, speed, stepStartMs)) return;
            if (dy != 0 && TryStep(Player, 0, dy, speed, stepStartMs)) return;
            SettleStep(Player);
        }
    }

    // ---- player stats from cards/buffs ----

    private double CardValue(string stat)
    {
        double total = 0;
        foreach (var (cardId, stacks) in _cards)
        {
            var def = Cards.ById[cardId];
            if (def.Stat == stat) total += def.Value * stacks;
        }
        return total;
    }

    private bool IsBuffActive(string buff) => _buffsUntilMs.TryGetValue(buff, out var until) && NowMs < until;

    private ClassStanceDef CurrentStance => PlayerClass.GetStance(_stanceId);

    private SkillDef[] CurrentSkillBar() => Classes.SkillBar(CurrentStance);

    private void ToggleStance()
    {
        if (!PlayerClass.CanToggleStance || Player.Hp <= 0) return;
        var next = PlayerClass.NextStance(_stanceId);
        _stanceId = next.Id;
        Emit("text", Player.X, Player.Y, 0, 0, 0, $"STANCE: {next.Name}");
        Emit("effect", Player.X, Player.Y, 0, 0, Classes.Skills[next.Slots[0]].EffectId);
    }

    // MG-02: Kaeli role tuning (auto vs skill damage, speed, range, AOE).
    private RoleTuning RoleTuning => _roles[Waifu.Role];
    // MG-02: auto/skill separation is done AT CALL SITES, never inside PlayerAttack() (it would apply
    // twice). Auto-hit uses * RoleAutoMult(); skill damage and all trait/echo/card procs use
    // * RoleSkillMult(). PlayerAttack() returns the "pure" attack (without role multiplier).
    private double RoleAutoMult() => RoleTuning.AutoDmgMult;
    private double RoleSkillMult() => RoleTuning.SkillDmgMult;
    private int EffectiveAutoRange() =>
        RoleTuning.AutoRange + (IsBuffActive(GameConfig.LunaraLunarFocusBuff)
            ? GameConfig.LunaraLunarFocusRangeBonus : 0);

    // MG-04: AOE size scaled by role (mage > knight > archer). Radius 0 (single-tile hit)
    // single-target) is preserved; positives round by AoeScale with floor 1. Math.Round is deterministic.
    private int ScaledRadius(int raw) =>
        raw <= 0 ? 0 : Math.Max(1, (int)Math.Round(raw * RoleTuning.AoeScale, MidpointRounding.AwayFromZero));
    // Effective skill radius at cast time. Ultimates cap at UltimateRadiusCap before
    // AoeScale and never fall below 2: they still "burst" (mage 3, archer/knight ~2).
    private int SkillRadius(int raw, bool isUlt) =>
        isUlt ? Math.Max(2, ScaledRadius(Math.Min(raw, GameConfig.UltimateRadiusCap))) : ScaledRadius(raw);

    private double PlayerAttack()
    {
        var atk = (Waifu.BaseAtk + EquipmentStats.AttackBonus)
                  * (1 + GameConfig.AtkPerRunLevel * (_runLevel - 1))
                  * (1 + Ascension * GameConfig.AscensionAtkBonus)
                  * (1 + _affinityStatBonus)
                  * Loadout.Mastery.AtkMult
                  * (1 + CardValue("atkPercent"));
        if (_trait.Kind == "pack_hunter")
        {
            var packed = _monsters.Count(m => m.Hp > 0 && m.Floor == Player.Floor
                                              && Chebyshev(m, Player) <= 2);
            atk *= 1 + Math.Min(packed * _trait.Value, _trait.Param) * _traitMult;
        }
        if (IsBuffActive("atk")) atk *= 1.35;
        if (IsBuffActive(GameConfig.UltStateAutoChain)) atk *= GameConfig.GaiaRicochetAttackMultiplier;
        if (IsBuffActive(GameConfig.RynnaBloodlustBuff)) atk *= GameConfig.RynnaBloodlustAttackMultiplier;
        if (IsBuffActive("bloodrage")) atk *= GameConfig.BloodRageAttackMultiplier;
        if (IsBuffActive("aegis")) atk *= GameConfig.SentinelAegisAttackMultiplier;
        return atk * GameConfig.PlayerDamageMult;
    }

    private double CritChance()
    {
        var crit = GameConfig.CritChance + CardValue("critChance")
                   + Loadout.Mastery.CritChanceBonus + EquipmentStats.CritChance;
        if (IsBuffActive("crit")) crit += 0.20;
        return crit;
    }

    private long AutoAttackInterval()
    {
        // MG-02: base speed comes from role (archer > knight > mage); card/buff/Gaia divisors
        // and the 400ms floor continue on top.
        var interval = (double)RoleTuning.BaseAutoAttackMs / (1 + CardValue("atkSpeedPercent"));
        if (IsBuffActive("atkspeed")) interval /= 1.40;
        if (IsBuffActive(GameConfig.SerenWarCadenceBuff)) interval /= GameConfig.SerenWarCadenceAttackSpeedMultiplier;
        if (IsBuffActive(GameConfig.LunaraLunarFocusBuff)) interval /= GameConfig.LunaraLunarFocusAttackSpeedMultiplier;
        if (IsBuffActive(GameConfig.UltStateAutoChain)) interval /= GameConfig.GaiaRicochetAttackSpeedMultiplier;
        if (IsBuffActive("aegis")) interval /= GameConfig.SentinelAegisAttackSpeedMultiplier;
        if (NowMs < _preyHuntBonusUntilMs) interval /= 1 + GameConfig.GaiaHuntAtkSpeedBonus; // Gaia: hunt
        return (long)Math.Max(interval, 400);
    }

    // ---- combat: player ----

    private void TickAutoHelper()
    {
        if (Player.Hp <= 0) return;

        if (_autoHelperTargeting)
        {
            var manualTarget = LockedManualTarget();
            if (manualTarget is not null)
            {
                Player.TargetId = manualTarget.Id;
            }
            else
            {
                var target = BestAutoHelperTarget(GameConfig.AutoHelperTargetRange);
                Player.TargetId = target?.Id ?? 0;
            }
        }

        if (Player.IsStunned(NowMs)) return;

        // G-10: auto-heal: uses the potion when health drops below the threshold (potion respects charges/cooldown).
        if (_autoHelperAutoHeal && Player.Hp * 100 < Player.MaxHp * _autoHelperHealPct)
            TryUsePotion();

        // G-10: pathing: when enabled, the helper walks by itself to the objective (chest/exit),
        // replacing combat movement (stand/follow/avoid). Combat (target/skills/ult) continues.
        if (_autoHelperNavMode == GameConfig.AutoHelperNavOff)
            TickAutoHelperMovement();
        else
            TickHelperNav();

        if (_autoHelperSkills)
            for (var slot = 0; slot < 4; slot++)
                TryAutoHelperSkill(slot);

        if (_autoHelperUltimate)
            TryAutoHelperSkill(4);
    }

    // ---- G-10: pathing do helper ("cavebot") + perfil persistido ----

    // Auto-loot: walks to the nearest active collectible (chest OR altar/Sanctuary), opens it, repeats;
    // with nothing left, heads to the exit (ladder/boss). Stops to fight when an enemy closes in. Waits
    // AutoLootStartDelayMs at floor start (the screen loads before the Kaeli starts walking).
    // Deterministic (tick state only + stable order in NextNavStep).
    private void TickHelperNav()
    {
        if (NowMs - _floorEnteredMs < GameConfig.AutoLootStartDelayMs) return;
        if (Player.IsMoving(NowMs)) return;
        if (_moveDirX != 0 || _moveDirY != 0 || _hasBufferedMoveDir) return; // input manual tem prioridade

        // Helper, not bot: if there is a reachable enemy "on screen", DEFEAT it before moving to loot/exit.
        // CurrentPlayerTarget only returns the target when it is in range + line of sight (same
        // targeting criterion); outside that, "no enemy exists" and the Kaeli goes to the chest/hole, which the
        // connected map guarantees is always reachable. Previously the cavebot only paused with the mob adjacent (<=1) and
        // walked past the rest; now it positions: approaches to auto-attack range (melee
        // encosta, ranged assenta no alcance) e o combate do mesmo tick (auto/skills/ult) abate. Se a
        // direct approach gets stuck on a concave wall, falls back to BFS pathing. Mobs are never blockers; when
        // they die, CurrentPlayerTarget clears and navigation flows again.
        var goals = NavGoals();
        // Role governs helper combat style: melee closes box (plants and cleaves), ranged mobs/kites.
        var isMelee = RoleTuning.AutoRange <= GameConfig.MeleeRange;

        // PRIORITY 1: dropped chest (falls on the corpse, near the fight): claim it WHILE luring, pulling the train.
        // (The central altar waits until after the pile, so the Kaeli does not cut straight across the room for it.)
        var chest = goals.FirstOrDefault(g => g.Kind == "chest");
        if (chest.Kind == "chest")
        {
            if (Chebyshev(Player.X, Player.Y, chest.X, chest.Y) <= 1) { TryInteract(chest.X, chest.Y); return; }
            if (StepTowardGoal(chest.X, chest.Y)) return;
        }

        // PRIORITY 2: the BOSS. Ranged KITEs (keeps distance; only plants to fire during Echo Break/stun);
        // melee faces it. When far away, lets the exit pather approach (BFS). Boss is the threat, so it comes before the pile.
        var boss = _monsters.FirstOrDefault(m => m.IsBossActor && m.Hp > 0 && m.Floor == _currentFloor);
        if (boss is not null && TickHelperBoss(boss, isMelee)) return;

        // PRIORITY 3: aggroed pile. Ranged ORBITS to clump and dump AoE; melee CLOSES BOX (plants and cleaves).
        var pile = AggroedMobsNear(GameConfig.HelperGatherRange);
        if (pile.Count >= GameConfig.HelperGatherThreshold)
        {
            if (isMelee) TickHelperBox(pile); else TickHelperMobbing(pile);
            return;
        }

        // PRIORITY 4: Echo altar (central): with no pile to mob, detour to claim the beat.
        var altar = goals.FirstOrDefault(g => g.Kind == "sanctuary");
        if (altar.Kind == "sanctuary")
        {
            if (Chebyshev(Player.X, Player.Y, altar.X, altar.Y) <= 1) { TryInteract(altar.X, altar.Y); return; }
            if (StepTowardGoal(altar.X, altar.Y)) return;
        }

        // PRIORITY 5: 1-2 loose mobs: engage to auto-attack range (tick combat kills them).
        var foe = CurrentPlayerTarget();
        if (foe is not null)
        {
            var combatSpeed = PlayerSpeed();
            var autoRange = EffectiveAutoRange();
            if (Chebyshev(Player, foe) > autoRange
                && !TryStepTowardDistance(foe, autoRange, combatSpeed)
                && NextNavStep(foe.X, foe.Y) is { } chase)
                TryStep(Player, chase.Dx, chase.Dy, combatSpeed);
            return;
        }

        // PRIORITY 6: nothing to fight/collect: head to the exit (portal/boss). But if the room is still NOT
        // clear and there is no exit/collectible (the last mob is lost outside range/vision), HUNT the nearest
        // living mob to close the room; otherwise the helper stalls with a straggler in a corner.
        if (goals.Count == 0)
        {
            if (NearestMonsterOnFloor() is { } straggler) StepTowardGoal(straggler.X, straggler.Y);
            return;
        }
        if (goals[0].Interactable && Chebyshev(Player.X, Player.Y, goals[0].X, goals[0].Y) <= 1)
        {
            TryInteract(goals[0].X, goals[0].Y);
            return;
        }
        foreach (var (gx, gy, _, _) in goals)
            if (StepTowardGoal(gx, gy)) return;

        // last resort (objective surrounded by adjacent mobs): pull the nearest one so combat can clear it.
        var blocker = BestAutoHelperTarget(GameConfig.AutoHelperTargetRange);
        if (blocker is not null)
            TryStepTowardDistance(blocker, GameConfig.AutoHelperFollowDistance, PlayerSpeed());
    }

    /// <summary>Um passo rumo ao objetivo: tenta desviar dos mobs (caminho liso); se travado, rota
    /// IGNORING mobs (goes to the blocker so combat can clear it). False = unreachable for now.</summary>
    private bool StepTowardGoal(int gx, int gy)
    {
        if (NextNavStep(gx, gy, avoidMonsters: true) is { } s1) { TryStep(Player, s1.Dx, s1.Dy, PlayerSpeed()); return true; }
        if (NextNavStep(gx, gy, avoidMonsters: false) is { } s2) { TryStep(Player, s2.Dx, s2.Dy, PlayerSpeed()); return true; }
        return false;
    }

    /// <summary>Helper boss combat. Ranged KITEs: keeps auto-attack distance and only PLANTS to
    /// fire when the boss is in Echo Break (stunned/broken): standing still against a pursuing boss was
    /// the anti-pattern (2026-06-29 feedback). Melee faces it (approaches and holds). Returns false when the boss is
    /// far away (lets the BFS exit pather approach). True = the tick position was resolved here.</summary>
    private bool TickHelperBoss(Actor boss, bool isMelee)
    {
        var dist = Chebyshev(Player, boss);
        var speed = PlayerSpeed();
        if (isMelee)
        {
            if (dist <= GameConfig.MeleeRange) return true; // colado: segura e cleava (o combate do tick bate)
            if (TryStepTowardDistance(boss, GameConfig.MeleeRange, speed)) return true;
            return false; // far/stuck: exit BFS approaches
        }

        var kite = EffectiveAutoRange();
        if (dist > kite + 3) return false; // far away: approach with exit BFS (local steering gets stuck on rock masses)

        // Echo Break / stunned: the boss is stopped, so plant and dump damage (tick combat/skills unload).
        if (boss.IsStaggered(NowMs) || boss.IsStunned(NowMs)) return true;

        if (dist > kite)
        {
            // slightly far: close in to enter firing range (approach is free; no "U" is needed).
            if (!TryStepTowardDistance(boss, kite, speed) && NextNavStep(boss.X, boss.Y) is { } s)
                TryStep(Player, s.Dx, s.Dy, speed);
            return true;
        }

        // boss closed in (dist <= kite): retreat. If straight escape hits a wall, slide by tangent (the "U").
        // If CORNERED (no escape step opens), use DASH (same ability as Shift)
        // to slide out through the open axis instead of tanking against the wall.
        if (!KiteAway(boss, speed)) TryHelperDash(boss);
        return true;
    }

    /// <summary>Cornered helper: fires dash on the best cardinal, the one that travels the most free tiles
    /// (tie-breaker: the one that moves farthest from the boss). Same ability/cooldown as Shift; only dashes if there is
    /// at least 1 free tile on some axis (otherwise it stays and combat/break resolves it).</summary>
    private void TryHelperDash(Actor boss)
    {
        Span<(int dx, int dy)> dirs = [(0, -1), (0, 1), (-1, 0), (1, 0)];
        var bestLen = 0;
        var bestAway = -1;
        (int dx, int dy) best = default;
        foreach (var (dx, dy) in dirs)
        {
            var len = DashReach(dx, dy); // role-aware: Knight blink / Archer pass-through / Mage stop-at-mob
            if (len == 0) continue;
            var away = Chebyshev(Player.X + dx * len, Player.Y + dy * len, boss.X, boss.Y);
            if (len > bestLen || (len == bestLen && away > bestAway)) { bestLen = len; bestAway = away; best = (dx, dy); }
        }
        if (bestLen > 0) TryDash(best.dx, best.dy);
    }

    /// <summary>Wall-aware kite retreat: moves straight away from the boss; if a wall blocks it, slides along
    /// tangent turned toward the arena center (the "U" back to the middle) before falling back to axes. Returns false
    /// when NO escape step opens (cornered): the caller then falls back to dash.</summary>
    private bool KiteAway(Actor boss, int speed)
    {
        var (ax, ay) = CardinalFromVector(Player.X - boss.X, Player.Y - boss.Y); // escape direction (radial)
        int tx = -ay, ty = ax;                                                   // tangente perpendicular
        if (CurrentArenaRoom() is { } arena
            && tx * Math.Sign(arena.CenterX - Player.X) + ty * Math.Sign(arena.CenterY - Player.Y) < 0)
        { tx = -tx; ty = -ty; }                                                  // turn the tangent toward center

        Span<(int dx, int dy)> cands =
        [
            (ax, ay),                                  // fuga reta
            (Math.Sign(ax + tx), Math.Sign(ay + ty)),  // diagonal fuga+tangente
            (tx, ty),                                  // pure tangent (slides along the wall toward center = the "U")
            (ax, 0), (0, ay),                          // one-axis escape
        ];
        foreach (var (dx, dy) in cands)
            if ((dx != 0 || dy != 0) && TryStep(Player, dx, dy, speed)) return true;
        return false;
    }

    /// <summary>Nearest living mob on the current floor (tie-break by id): helper straggler-seek target.</summary>
    private Actor? NearestMonsterOnFloor()
    {
        Actor? best = null;
        var bestD = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor) continue;
            var d = Chebyshev(Player, m);
            if (best is null || d < bestD || (d == bestD && m.Id < best.Id)) { best = m; bestD = d; }
        }
        return best;
    }

    /// <summary>Melee helper box: does NOT orbit/lure; plants in place and lets mobs come in (cleaves around).
    /// If no mob is adjacent, steps toward the nearest one to engage; with an adjacent mob, HOLD.</summary>
    private void TickHelperBox(List<Actor> pile)
    {
        if (pile.Any(m => Chebyshev(Player, m) <= GameConfig.MeleeRange)) return; // engaged: hold position
        var nearest = pile.OrderBy(m => Chebyshev(Player, m)).ThenBy(m => m.Id).First();
        var speed = PlayerSpeed();
        if (!TryStepTowardDistance(nearest, GameConfig.MeleeRange, speed) && NextNavStep(nearest.X, nearest.Y) is { } s)
            TryStep(Player, s.Dx, s.Dy, speed);
    }

    // Navigation objectives in priority order: active collectibles (normal/cursed chest OR Echo
    // altar: the minimap "purple" targets) from nearest to farthest; then the exit (floor ladder,
    // or the boss if there is no ladder). Coordinates come directly from server POIs.
    // Deterministic: OrderBy is stable and tie-breaks by Id.
    private List<(int X, int Y, bool Interactable, string Kind)> NavGoals()
    {
        var goals = _pois
            .Where(p => !p.Used && p.Floor == _currentFloor && p.Kind is "chest" or "sanctuary")
            .OrderBy(p => Chebyshev(Player.X, Player.Y, p.X, p.Y))
            .ThenBy(p => p.Id)
            .Select(p => (p.X, p.Y, true, p.Kind))
            .ToList();

        var ladder = _pois.FirstOrDefault(p => !p.Used && p.Floor == _currentFloor && p.Kind == "ladder");
        if (ladder is not null) goals.Add((ladder.X, ladder.Y, true, "ladder"));

        // no ladder (boss floor): the exit is defeating the boss, so walk to it.
        var boss = _monsters.FirstOrDefault(m => m.IsBossActor && m.Hp > 0 && m.Floor == _currentFloor);
        if (boss is not null) goals.Add((boss.X, boss.Y, false, "boss"));

        return goals;
    }

    private (int X, int Y, bool Interactable, string Kind)? NavGoal()
    {
        var goals = NavGoals();
        return goals.Count > 0 ? goals[0] : null;
    }

    // G-10: current auto-loot target for client readability (null when pathing is off).
    private NavTargetDto? CurrentNavTargetDto()
    {
        if (_autoHelperNavMode != GameConfig.AutoHelperNavLoot || Player.Hp <= 0) return null;
        return NavGoal() is { } g ? new NavTargetDto(g.X, g.Y, g.Kind) : null;
    }

    // First step of a shortest path (BFS) from the player until adjacent to (tx,ty).
    // Real BFS contours around walls/corners; greedy step got stuck in dead ends. With avoidMonsters=true,
    // living mobs block tiles (the Kaeli detours, damage-free path); with false only terrain blocks
    // (fallback that walks toward the mob blocking the corridor so combat can clear it). Deterministic:
    // fixed neighbor order, no `_rng`/`DateTime`. Returns null if there is no terrain path.
    private (int Dx, int Dy)? NextNavStep(int tx, int ty, bool avoidMonsters = true)
    {
        var floor = Floor;
        int w = floor.W, h = floor.H, sx = Player.X, sy = Player.Y;
        if (Chebyshev(sx, sy, tx, ty) <= 1) return null;

        HashSet<int>? occupied = null;
        if (avoidMonsters)
        {
            occupied = new HashSet<int>();
            foreach (var m in _monsters)
                if (m.Hp > 0 && m.Floor == _currentFloor) occupied.Add(m.Y * w + m.X);
        }

        var firstStep = new (int Dx, int Dy)?[w * h];
        var seen = new bool[w * h];
        var q = new Queue<int>();
        seen[sy * w + sx] = true;
        q.Enqueue(sy * w + sx);

        // cardinals before diagonals: stable and deterministic path.
        ReadOnlySpan<(int dx, int dy)> dirs =
        [
            (0, -1), (0, 1), (-1, 0), (1, 0), (-1, -1), (1, -1), (-1, 1), (1, 1)
        ];

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int cx = cur % w, cy = cur / w;
            foreach (var (dx, dy) in dirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                var ni = ny * w + nx;
                if (seen[ni]) continue;

                var isTargetTile = nx == tx && ny == ty;
                if (!isTargetTile)
                {
                    if (floor.IsBlocked(nx, ny)) continue;
                    if (dx != 0 && dy != 0 && (floor.IsBlocked(cx + dx, cy) || floor.IsBlocked(cx, cy + dy))) continue;
                    if (occupied is not null && occupied.Contains(ni)) continue;
                }

                seen[ni] = true;
                var step = firstStep[cur] ?? (dx, dy);
                if (Chebyshev(nx, ny, tx, ty) <= 1) return step; // adjacent to target: enough to interact/fight
                firstStep[ni] = step;
                q.Enqueue(ni);
            }
        }
        return null;
    }

    // Perfil do helper persistido por Kaeli:
    // "targeting|skills|ult|pref|movement|autoheal|nav|healpct|autocards".
    public string EncodeHelperProfile() => string.Join('|',
        _autoHelperTargeting ? 1 : 0,
        _autoHelperSkills ? 1 : 0,
        _autoHelperUltimate ? 1 : 0,
        _autoHelperTargetPreference,
        _autoHelperMovementMode,
        _autoHelperAutoHeal ? 1 : 0,
        _autoHelperNavMode,
        _autoHelperHealPct,
        _autoHelperAutoCards ? 1 : 0);

    private void ApplyHelperProfile(string encoded)
    {
        var p = encoded.Split('|');
        if (p.Length < 7) return;
        _autoHelperTargeting = p[0] == "1";
        _autoHelperSkills = p[1] == "1";
        _autoHelperUltimate = p[2] == "1";
        _autoHelperTargetPreference = NormalizeAutoHelperTargetPreference(p[3]);
        _autoHelperMovementMode = NormalizeAutoHelperMovementModeName(p[4]);
        _autoHelperAutoHeal = p[5] == "1";
        _autoHelperNavMode = GameConfig.NormalizeAutoHelperNav(p[6]);
        if (p.Length > 7 && int.TryParse(p[7], out var healPct)) _autoHelperHealPct = GameConfig.ClampHealPct(healPct);
        if (p.Length > 8) _autoHelperAutoCards = p[8] == "1";
    }

    private static string NormalizeAutoHelperMovementModeName(string? mode) => mode switch
    {
        GameConfig.AutoHelperMovementModeFollow => GameConfig.AutoHelperMovementModeFollow,
        GameConfig.AutoHelperMovementModeAvoid => GameConfig.AutoHelperMovementModeAvoid,
        _ => GameConfig.AutoHelperMovementModeNone
    };

    private void TickAutoHelperMovement()
    {
        if (_helperMovementOverrideTargetId != 0)
        {
            var overrideDead = _monsters.All(m => m.Id != _helperMovementOverrideTargetId || m.Hp <= 0);
            var newTarget = Player.TargetId != 0 && Player.TargetId != _helperMovementOverrideTargetId;
            if (overrideDead || newTarget)
            {
                _autoHelperMovementMode = _savedAutoHelperMovementMode;
                _helperMovementOverrideTargetId = 0;
            }
        }

        if (_autoHelperMovementMode == GameConfig.AutoHelperMovementModeNone || Player.IsMoving(NowMs)) return;
        if (_moveDirX != 0 || _moveDirY != 0 || _hasBufferedMoveDir) return;

        var target = CurrentPlayerTarget();
        if (target is null) return;

        var speed = PlayerSpeed();

        if (_autoHelperMovementMode == GameConfig.AutoHelperMovementModeFollow)
        {
            // Approaches until adjacent (AutoHelperFollowDistance=1). Local steering tries all 8
            // directions (including diagonal) and moves around small obstacles; if no step approaches
            // (concave wall between the Kaeli and the enemy, which used to stall pursuit), it falls back to the
            // auto-loot BFS path (NextNavStep stops at adjacency). Deterministic: neither uses _rng/DateTime.
            if (Chebyshev(Player, target) > GameConfig.AutoHelperFollowDistance
                && !TryStepTowardDistance(target, GameConfig.AutoHelperFollowDistance, speed)
                && NextNavStep(target.X, target.Y) is { } nav)
                TryStep(Player, nav.Dx, nav.Dy, speed);
            return;
        }

        if (_autoHelperMovementMode != GameConfig.AutoHelperMovementModeAvoid) return;

        // Avoid: settles at AutoHelperAvoidDistance, approaching or retreating as needed.
        // Diagonals allowed: slides along the wall instead of freezing while taking damage.
        TryStepTowardDistance(target, GameConfig.AutoHelperAvoidDistance, speed);
    }

    // Deterministic local steering: among the 8 neighbor steps (cardinals before diagonals,
    // same order as NextNavStep), chooses the unblocked step whose Chebyshev distance to the
    // target is closest to desiredDist. Tie -> first direction in the fixed order. Does not move if
    // no step improves the current distance (prevents equilibrium jitter). CanStep already blocks
    // walls, corner cutting, and monster-occupied tiles.
    private bool TryStepTowardDistance(Actor target, int desiredDist, int speed)
    {
        var bestErr = Math.Abs(Chebyshev(Player, target) - desiredDist);
        (int dx, int dy) best = default;
        var found = false;
        ReadOnlySpan<(int dx, int dy)> dirs =
        [
            (0, -1), (0, 1), (-1, 0), (1, 0), (-1, -1), (1, -1), (-1, 1), (1, 1)
        ];
        foreach (var (dx, dy) in dirs)
        {
            if (!CanStep(Player, dx, dy)) continue;
            var nd = Math.Max(Math.Abs(target.X - (Player.X + dx)), Math.Abs(target.Y - (Player.Y + dy)));
            var err = Math.Abs(nd - desiredDist);
            if (err < bestErr)
            {
                bestErr = err;
                best = (dx, dy);
                found = true;
            }
        }
        return found && TryStep(Player, best.dx, best.dy, speed);
    }

    private Actor? LockedManualTarget()
    {
        if (_manualTargetId == 0) return null;
        var lockedId = _manualTargetId;
        var target = _monsters.FirstOrDefault(m => m.Id == lockedId);
        if (target is not null && IsTargetableByPlayer(target, GameConfig.AutoHelperTargetRange))
            return target;

        _manualTargetId = 0;
        if (Player.TargetId == lockedId) Player.TargetId = 0;
        return null;
    }

    private static string NormalizeAutoHelperTargetPreference(string? preference) =>
        preference == GameConfig.AutoHelperTargetPreferenceNearest
            ? GameConfig.AutoHelperTargetPreferenceNearest
            : GameConfig.AutoHelperTargetPreferenceLowestHp;

    private static string NormalizeAutoHelperMovementMode(int movementMode) =>
        movementMode switch
        {
            GameConfig.AutoHelperMovementModeFollowCode => GameConfig.AutoHelperMovementModeFollow,
            GameConfig.AutoHelperMovementModeAvoidCode => GameConfig.AutoHelperMovementModeAvoid,
            _ => GameConfig.AutoHelperMovementModeNone
        };

    private Actor? BestAutoHelperTarget(int maxRange, Func<Actor, bool>? extra = null)
    {
        // G-08B: shieldbearer forces focus: if there is a valid shieldbearer target, the helper prioritizes it.
        var shielder = FindTargetableShielder(maxRange, extra);
        if (shielder is not null) return shielder;

        Actor? best = null;
        var bestHp = int.MaxValue;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (!IsTargetableByPlayer(m, maxRange)) continue;
            if (extra is not null && !extra(m)) continue;

            var dist = Chebyshev(Player, m);
            var better = _autoHelperTargetPreference == GameConfig.AutoHelperTargetPreferenceNearest
                ? dist < bestDist || (dist == bestDist && m.Hp < bestHp)
                                  || (dist == bestDist && m.Hp == bestHp && (best is null || m.Id < best.Id))
                : m.Hp < bestHp || (m.Hp == bestHp && dist < bestDist)
                                  || (m.Hp == bestHp && dist == bestDist && (best is null || m.Id < best.Id));
            if (better)
            {
                best = m;
                bestHp = m.Hp;
                bestDist = dist;
            }
        }
        return best;
    }

    /// <summary>G-08B: nearest valid shieldbearer target to choose (stable tie-break by id).</summary>
    private Actor? FindTargetableShielder(int maxRange, Func<Actor, bool>? extra)
    {
        Actor? best = null;
        foreach (var m in _monsters)
        {
            if (!IsTargetableByPlayer(m, maxRange)) continue;
            if (extra is not null && !extra(m)) continue;
            if (GameConfig.BehaviorProfile(m.Species!.BehaviorId) is not { ShieldFraction: > 0 }) continue;
            if (best is null || m.Id < best.Id) best = m;
        }
        return best;
    }

    private void TryAutoHelperSkill(int slot)
    {
        if (slot < 4 && !FreeCast && NowMs < _skillReadyAtMs[slot]) return;
        if (slot == 4 && !FreeCast && _gauge < GameConfig.UltimateGaugeMax) return;

        var skill = CurrentSkillBar()[slot];
        if (!ShouldAutoHelperCast(skill, slot == 4, out var target)) return;

        if (_autoHelperTargeting && _manualTargetId == 0 && target is not null)
            Player.TargetId = target.Id;

        TryCastSkill(slot);
    }

    private bool ShouldAutoHelperCast(SkillDef skill, bool isUlt, out Actor? target)
    {
        target = null;

        if (skill.Shape == "buff")
            return ShouldAutoHelperCastBuff(skill);

        var currentTarget = _autoHelperTargeting ? LockedManualTarget() : CurrentPlayerTarget();
        if (currentTarget is not null)
        {
            target = currentTarget;
            return SkillWouldAffectMonster(skill, currentTarget, isUlt);
        }

        if (skill.Shape is "nova" or "ring" or "summon")
            return SkillWouldAffectMonster(skill, null, isUlt);

        if (!_autoHelperTargeting) return false;

        target = BestAutoHelperTarget(GameConfig.AutoHelperTargetRange, m => SkillWouldAffectMonster(skill, m, isUlt));
        return target is not null;
    }

    private bool ShouldAutoHelperCastBuff(SkillDef skill)
    {
        if (skill.Buff == "heal")
            return Player.Hp < Player.MaxHp * GameConfig.AutoHelperHealHpFraction;

        if (skill.Buff is not null && IsBuffActive(skill.Buff))
            return false;

        return BestAutoHelperTarget(GameConfig.AutoHelperTargetRange) is not null;
    }

    private Actor? CurrentPlayerTarget()
    {
        if (Player.TargetId == 0) return null;
        var target = _monsters.FirstOrDefault(m => m.Id == Player.TargetId);
        return target is not null && IsTargetableByPlayer(target, GameConfig.AutoHelperTargetRange)
            ? target
            : null;
    }

    private bool SkillWouldAffectMonster(SkillDef skill, Actor? target, bool isUlt)
    {
        switch (skill.Shape)
        {
            case "single":
                return target is not null && CanSkillReachTarget(skill, target);

            case "barrage": // multi-hit at one point (the ult the user liked): counts as 1 target
                if (target is null || !CanSkillReachTarget(skill, target)) return false;
                return AnyMonsterOnTiles(CircleTiles(target.X, target.Y, SkillRadius(skill.Radius, isUlt)));

            case "area":
                if (target is null || !CanSkillReachTarget(skill, target)) return false;
                return AoeWorthCasting(CircleTiles(target.X, target.Y, SkillRadius(skill.Radius, isUlt)));

            case "field":
                if (target is null || !CanSkillReachTarget(skill, target)) return false;
                // do not repaint fire where active fire already exists (kills "flame spam": wait for embers to fade).
                if (HasActiveFieldNear(target.X, target.Y, skill.Element, Math.Max(skill.SummonRadius, 0) + 1))
                    return false;
                return AoeWorthCasting(CircleTiles(target.X, target.Y, Math.Max(skill.SummonRadius, 0)));

            case "nova":
                return AoeWorthCasting(CircleTiles(Player.X, Player.Y, SkillRadius(skill.Radius, isUlt)),
                    skipPlayerTile: true);

            case "ring":
                return AoeWorthCasting(
                    RingTiles(Player.X, Player.Y, skill.RingInner,
                        Math.Max(SkillRadius(skill.Radius, isUlt), skill.RingInner + 1)));

            case "summon":
                return AnyMonsterOnTiles(CircleTiles(Player.X, Player.Y, Math.Max(skill.SummonRadius, 1)),
                    skipPlayerTile: true, requiredMonsterId: target?.Id ?? 0);

            case "beam":
                return BeamWouldHitMonster(skill, target);

            case "cone":
                return ConeWorthCasting(skill, target, isUlt);

            case "chain":
                return target is not null && CanSkillReachTarget(skill, target);

            default:
                return false;
        }
    }

    private bool CanSkillReachTarget(SkillDef skill, Actor target)
    {
        var range = skill.Range > 0 ? skill.Range : SkillFootprintRange(skill);
        return IsTargetableByPlayer(target, Math.Max(range, 1));
    }

    private static int SkillFootprintRange(SkillDef skill) => skill.Shape switch
    {
        "cone" => Math.Max(skill.Radius, 1),
        "nova" or "ring" => Math.Max(skill.Radius, 1),
        "summon" => Math.Max(skill.SummonRadius, 1),
        _ => GameConfig.AutoHelperTargetRange
    };

    private bool BeamWouldHitMonster(SkillDef skill, Actor? target)
    {
        var (dx, dy) = DirDelta(Player.Facing, target);
        for (var i = 1; i <= Math.Max(skill.Range, 1); i++)
        {
            var tx = Player.X + dx * i;
            var ty = Player.Y + dy * i;
            var px = Player.X + dx * (i - 1);
            var py = Player.Y + dy * (i - 1);
            if (DiagonalCornerBlocked(Floor, px, py, tx, ty)) break;
            if (Floor.IsBlocked(tx, ty)) break;
            if (MonsterAt(tx, ty) is not null) return true;
        }
        return false;
    }

    /// <summary>Cone is worthwhile for the helper when it hits >= AutoHelperAoeMinTargets OR catches a boss/elite.</summary>
    private bool ConeWorthCasting(SkillDef skill, Actor? target, bool isUlt)
    {
        var (dx, dy) = DirDelta(Player.Facing, target);
        var reach = Math.Max(skill.Radius, 1);
        var count = 0;
        foreach (var (tx, ty) in ConeTiles(Player.X, Player.Y, dx, dy, Math.Max(SkillRadius(skill.Radius, isUlt), 1)))
        {
            var victim = MonsterAt(tx, ty);
            if (victim is null || !IsTargetableByPlayer(victim, reach)) continue;
            if (victim.IsBossActor || victim.IsElite) return true;
            if (++count >= GameConfig.AutoHelperAoeMinTargets) return true;
        }
        return false;
    }

    private bool AnyMonsterOnTiles(IEnumerable<(int X, int Y)> tiles, bool skipPlayerTile = false, int requiredMonsterId = 0)
    {
        foreach (var (tx, ty) in tiles)
        {
            if (skipPlayerTile && tx == Player.X && ty == Player.Y) continue;
            var victim = MonsterAt(tx, ty);
            if (victim is null) continue;
            if (requiredMonsterId != 0 && victim.Id != requiredMonsterId) continue;
            return true;
        }
        return false;
    }

    /// <summary>AoE only "counts" for the helper if it hits >= AutoHelperAoeMinTargets mobs OR if it catches a
    /// boss/elite (do not suppress boss damage). Prevents "skill spam into nothing" on a lone target.</summary>
    private bool AoeWorthCasting(IEnumerable<(int X, int Y)> tiles, bool skipPlayerTile = false)
    {
        var count = 0;
        foreach (var (tx, ty) in tiles)
        {
            if (skipPlayerTile && tx == Player.X && ty == Player.Y) continue;
            var victim = MonsterAt(tx, ty);
            if (victim is null) continue;
            if (victim.IsBossActor || victim.IsElite) return true;
            if (++count >= GameConfig.AutoHelperAoeMinTargets) return true;
        }
        return false;
    }

    /// <summary>True if an active field of the same element is already on/near the point (do not repaint fire onto fire).</summary>
    private bool HasActiveFieldNear(int x, int y, string element, int radius)
    {
        foreach (var f in _fields)
        {
            if (f.Floor != _currentFloor || f.Element != element) continue;
            if (Math.Abs(f.X - x) <= radius && Math.Abs(f.Y - y) <= radius) return true;
        }
        return false;
    }

    /// <summary>Mobs already aggroed on the player within <paramref name="range"/> (Chebyshev): the "pack"
    /// that orbit-mobbing gathers and melts. Stable order (_monsters scan) -> deterministic.</summary>
    private List<Actor> AggroedMobsNear(int range)
    {
        var list = new List<Actor>();
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != Player.Floor || m.TargetId != Player.Id) continue;
            if (Chebyshev(Player, m) <= range) list.Add(m);
        }
        return list;
    }

    /// <summary>Orbit-mobbing: walks in an arc around the ARENA CENTER (fixed point -> no jitter from a
    /// jumping centroid), on a short target radius. Mobs chase and cut the curve, piling into a clump
    /// where AoE lands. Consistent rotation direction (always the same) and anti-backtracking (do not return to the SQM
    /// it came from) kill the "back and forth". No Rng: deterministic (center + tick geometry).</summary>
    private void TickHelperMobbing(List<Actor> pile)
    {
        var arena = CurrentArenaRoom();
        double cx, cy;
        if (arena is not null) { cx = arena.CenterX; cy = arena.CenterY; }
        else
        {
            long sx = 0, sy = 0;
            foreach (var m in pile) { sx += m.X; sy += m.Y; }
            cx = (double)sx / pile.Count; cy = (double)sy / pile.Count;
        }

        double rx = Player.X - cx;       // radial center to player
        double ry = Player.Y - cy;
        var dist = Math.Sqrt(rx * rx + ry * ry);
        double mvx, mvy;
        if (dist < 0.5)
        {
            mvx = 1; mvy = 0; // exactly at center: leave in any direction and the arc starts next tick.
        }
        else
        {
            // tangential (radial rotated 90 degrees, fixed direction) + correction to target radius (short, so the clump sticks).
            var tux = -ry / dist;
            var tuy = rx / dist;
            var target = GameConfig.HelperMobOrbitRadius;
            var bias = dist > target + 1 ? -0.9 : dist < target - 1 ? 0.9 : 0.0;
            mvx = tux + rx / dist * bias;
            mvy = tuy + ry / dist * bias;
        }

        var (dx, dy) = CardinalFromVector(mvx, mvy);
        var speed = PlayerSpeed();
        // 1st pass: avoid returning to the previous tile (anti-stutter). 2nd: allow backtracking if stuck.
        foreach (var (ax, ay) in StepCandidates(dx, dy))
        {
            if (Player.X + ax == _mobLastX && Player.Y + ay == _mobLastY) continue;
            if (TryStep(Player, ax, ay, speed)) { _mobLastX = Player.FromX; _mobLastY = Player.FromY; return; }
        }
        foreach (var (ax, ay) in StepCandidates(dx, dy))
            if (TryStep(Player, ax, ay, speed)) { _mobLastX = Player.FromX; _mobLastY = Player.FromY; return; }
        // fully walled in: stay and let combat (auto/AoE) work; the pile is already close.
    }

    /// <summary>Reference arena room for orbiting: the one containing the Kaeli, otherwise the largest on the floor.</summary>
    private Room? CurrentArenaRoom()
    {
        Room? best = null;
        var bestArea = -1;
        foreach (var r in Floor.Rooms)
        {
            if (r.Contains(Player.X, Player.Y)) return r;
            var area = r.W * r.H;
            if (area > bestArea) { bestArea = area; best = r; }
        }
        return best;
    }

    /// <summary>Preferred step followed by alternatives (wall contouring), in order.</summary>
    private static IEnumerable<(int dx, int dy)> StepCandidates(int dx, int dy)
    {
        yield return (dx, dy);
        foreach (var alt in StepAlternatives(dx, dy)) yield return alt;
    }

    /// <summary>Continuous vector -> 8-direction grid step (nearest cardinal/diagonal).</summary>
    private static (int dx, int dy) CardinalFromVector(double vx, double vy)
    {
        var mag = Math.Max(Math.Abs(vx), Math.Abs(vy));
        if (mag < 1e-6) return (1, 0);
        var dx = Math.Abs(vx) > 0.40 * mag ? Math.Sign(vx) : 0;
        var dy = Math.Abs(vy) > 0.40 * mag ? Math.Sign(vy) : 0;
        if (dx == 0 && dy == 0) dx = Math.Sign(vx) != 0 ? Math.Sign(vx) : 1;
        return (dx, dy);
    }

    /// <summary>Alternative steps to contour around a wall when the preferred step blocks (slides by one axis, then rotates).</summary>
    private static IEnumerable<(int dx, int dy)> StepAlternatives(int dx, int dy)
    {
        if (dx != 0 && dy != 0) { yield return (dx, 0); yield return (0, dy); }
        else if (dx != 0) { yield return (dx, 1); yield return (dx, -1); yield return (0, 1); yield return (0, -1); }
        else { yield return (1, dy); yield return (-1, dy); yield return (1, 0); yield return (-1, 0); }
    }

    // MG-02: auto range comes from role (archer > mage > knight), no longer from the weapon (cosmetic).
    private bool CanPlayerAutoAttack(Actor target) =>
        IsTargetableByPlayer(target, EffectiveAutoRange());

    private bool IsTargetableByPlayer(Actor target, int maxRange) =>
        target.Hp > 0
        && target.Floor == Player.Floor
        && Chebyshev(Player, target) <= maxRange
        && HasLineOfSight(Player.X, Player.Y, target.X, target.Y);

    private void TickPlayerCombat()
    {
        if (Player.Hp <= 0) return;
        if (Player.TargetId == 0 || NowMs < _autoAttackReadyAtMs) return;
        var target = _monsters.FirstOrDefault(m => m.Id == Player.TargetId && m.Hp > 0 && m.Floor == Player.Floor);
        if (target is null)
        {
            Player.TargetId = 0;
            return;
        }

        if (!CanPlayerAutoAttack(target)) return;

        // KR-00/KR-04: lock_pierce and Ricochet keep Gaia's autos committed to the Prey when that
        // marked target is still a legal auto target.
        if (((IsAutoModArmed && _autoModKind == "lock_pierce") || IsBuffActive(GameConfig.UltStateAutoChain))
            && TraitMarkedTarget() is { } marked && CanPlayerAutoAttack(marked))
            target = marked;

        _autoAttackReadyAtMs = NowMs + AutoAttackInterval();
        Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);

        var missile = Waifus.WeaponMissile(Waifu.Weapon, Waifu.Element);
        if (missile > 0)
            Emit("projectile", Player.X, Player.Y, target.X, target.Y, missile);

        var attackElement = string.IsNullOrWhiteSpace(EquipmentStats.WeaponElement)
            ? Waifu.Element
            : EquipmentStats.WeaponElement;
        var autoDamage = PlayerAttack() * RoleAutoMult();
        DealDamageToMonster(target, autoDamage, attackElement, hitEffect: missile > 0 ? 0 : 216);
        ApplyLunaraBaselineAutoSplash(target, autoDamage, attackElement, missile);
        if (IsBuffActive(GameConfig.UltStateAutoChain))
            ApplyRicochetAutoChain(target, autoDamage, attackElement, missile);
        // KR-00: while armed, the same auto splashes per its kind (cleave/pierce) and spends a charge.
        if (IsAutoModArmed) ConsumeAutoModifier(target, autoDamage, attackElement);
    }

    /// <summary>Gaia Ricochet (§4D): while auto_chain is active, the primary auto stays full-strength
    /// on the Prey and reduced follow-up shots jump through nearby foes. The jump target is stable:
    /// shortest distance from the previous hit, then lowest id. Boss-only fights simply have no jumps.</summary>
    private void ApplyRicochetAutoChain(Actor primary, double autoDamage, string element, int missile)
    {
        if (_trait.Kind != "prey") return;
        var hit = new HashSet<int> { primary.Id };
        var fromX = primary.X;
        var fromY = primary.Y;
        for (var h = 0; h < GameConfig.GaiaRicochetChainJumps; h++)
        {
            var next = NearestUnhitMonster(fromX, fromY, GameConfig.GaiaRicochetChainRange, hit);
            if (next is null) break;
            if (missile > 0) Emit("projectile", fromX, fromY, next.X, next.Y, missile);
            Emit("effect", next.X, next.Y, 0, 0, 216);
            DealDamageToMonster(next, autoDamage * GameConfig.GaiaRicochetSecondaryDamageScale, element, 0);
            hit.Add(next.Id);
            fromX = next.X;
            fromY = next.Y;
        }
    }

    /// <summary>Lunara Frostbite (§4C): her baseline auto is not pure single-target. It splashes a
    /// small moonshot into the nearest neighbor, seeding frost through the pack while keeping caster
    /// AoE modest. Stable target choice: distance from the primary, then id.</summary>
    private void ApplyLunaraBaselineAutoSplash(Actor primary, double autoDamage, string element, int missile)
    {
        if (_trait.Kind != "shatter") return;
        var neighbor = NearestUnhitMonster(primary.X, primary.Y, GameConfig.LunaraBaselineSplashRange,
            new HashSet<int> { primary.Id });
        if (neighbor is null) return;
        if (missile > 0) Emit("projectile", primary.X, primary.Y, neighbor.X, neighbor.Y, missile);
        Emit("effect", neighbor.X, neighbor.Y, 0, 0, 44);
        DealDamageToMonster(neighbor, autoDamage * GameConfig.LunaraBaselineSplashDamageScale, element, 0);
    }

    // ================= KR-00: auto-modifier seam (empowered autoattack) =================
    // Shared by the auto-Kaelis (Seren cleave, Lunara/Gaia pierce, Gaia lock_pierce). Deterministic:
    // the neighbor is chosen by shortest distance with a lowest-id tie-break; only draws _rng through
    // the normal hit roll. Dormant until a kit sets SkillDef.AutoModKind on one of its buff skills.

    /// <summary>True while an auto-modifier is active: a kind is set, the window has not lapsed, and
    /// (for charge-based mods) at least one charge remains.</summary>
    private bool IsAutoModArmed =>
        _autoModKind is not null && NowMs < _autoModUntilMs
        && (_autoModMaxCharges == 0 || _autoModChargesLeft > 0);

    /// <summary>Arms the auto-modifier from a buff-shaped skill. Charge-based when AutoModCharges > 0
    /// (reset-on-kill refunds up to that cap); otherwise time-windowed by BuffMs (default window as a fallback).</summary>
    private void ActivateAutoModifier(SkillDef skill, double scale)
    {
        _autoModKind = skill.AutoModKind;
        _autoModMaxCharges = Math.Max(skill.AutoModCharges, 0);
        _autoModChargesLeft = _autoModMaxCharges;
        _autoModResetOnKill = skill.AutoModResetOnKill;
        var windowMs = skill.BuffMs > 0 ? skill.BuffMs : GameConfig.AutoModDefaultWindowMs;
        _autoModUntilMs = NowMs + (long)(windowMs * scale);
    }

    private void DisarmAutoModifier()
    {
        _autoModKind = null;
        _autoModChargesLeft = 0;
        _autoModMaxCharges = 0;
        _autoModResetOnKill = false;
        _autoModUntilMs = 0;
    }

    /// <summary>Applies the armed auto-modifier's secondary effect on the same auto and spends a charge.
    /// cleave hits the small diamond around the primary; pierce/lock_pierce splash to the nearest
    /// neighbor with falloff. Called only while <see cref="IsAutoModArmed"/> (so charges &gt; 0 here).</summary>
    private void ConsumeAutoModifier(Actor primary, double autoDamage, string element)
    {
        // Spend this auto's charge first; a kill from the splash below can then refund it (net-0 on kill).
        if (_autoModMaxCharges > 0) _autoModChargesLeft--;

        switch (_autoModKind)
        {
            case "cleave":
                foreach (var (tx, ty) in CircleTiles(primary.X, primary.Y, GameConfig.AutoModCleaveRadius))
                {
                    if (tx == primary.X && ty == primary.Y) continue;
                    var victim = MonsterAt(tx, ty);
                    if (victim is null || victim.Id == primary.Id) continue;
                    Emit("effect", tx, ty, 0, 0, 216);
                    DealDamageToMonster(victim, autoDamage * GameConfig.AutoModCleaveDamageScale, element, 0);
                }
                break;

            case "pierce":
            case "lock_pierce":
                if (NearestAutoModNeighbor(primary) is { } neighbor)
                {
                    Emit("effect", neighbor.X, neighbor.Y, 0, 0, 216);
                    DealDamageToMonster(neighbor, autoDamage * GameConfig.AutoModPierceDamageScale, element, 0);
                    if (_trait.Kind == "shatter" && neighbor.Hp > 0)
                    {
                        AddFrostStacks(neighbor, GameConfig.LunaraMoonlightVolleyExtraFrostStacks);
                        TryShatterFrost(neighbor);
                    }
                }
                break;
        }

        if (_autoModMaxCharges > 0 && _autoModChargesLeft <= 0) DisarmAutoModifier();
    }

    /// <summary>KR-00 reset-on-kill hook: a kill during an armed window refunds one charge (capped).</summary>
    private void OnMonsterKilledAutoMod()
    {
        if (_autoModKind is null || !_autoModResetOnKill || _autoModMaxCharges <= 0) return;
        if (NowMs >= _autoModUntilMs) return;
        _autoModChargesLeft = Math.Min(_autoModChargesLeft + 1, _autoModMaxCharges);
    }

    /// <summary>Nearest living neighbor (other than the primary) within the pierce reach, on the current
    /// floor. Deterministic: shortest Chebyshev distance, ties broken by lowest id.</summary>
    private Actor? NearestAutoModNeighbor(Actor primary)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || m.Id == primary.Id) continue;
            var d = Chebyshev(primary.X, primary.Y, m.X, m.Y);
            if (d > GameConfig.AutoModPierceRange) continue;
            if (d < bestDist || (d == bestDist && (best is null || m.Id < best.Id)))
            {
                bestDist = d;
                best = m;
            }
        }
        return best;
    }

    /// <summary>The current trait mark used by lock_pierce (Gaia's Prey today); null for traits without a
    /// single marked target. Reads existing state only — no kit is wired to lock_pierce yet.</summary>
    private Actor? TraitMarkedTarget() =>
        _trait.Kind == "prey" && _preyId != 0
            ? _monsters.FirstOrDefault(m => m.Id == _preyId && m.Hp > 0 && m.Floor == _currentFloor)
            : null;

    private void TryCastSkill(int slot)
    {
        if (slot is < 0 or > 4 || Player.Hp <= 0 || Player.IsStunned(NowMs)) return;
        var skill = CurrentSkillBar()[slot];
        var isUlt = slot == 4;

        if (isUlt)
        {
            if (!FreeCast && _gauge < GameConfig.UltimateGaugeMax) return;
        }
        else if (!FreeCast && NowMs < _skillReadyAtMs[slot]) return;

        var target = _monsters.FirstOrDefault(m => m.Id == Player.TargetId && m.Hp > 0 && m.Floor == Player.Floor)
                     ?? NearestMonster(skill.Range > 0 ? skill.Range : GameConfig.AutoHelperTargetRange);

        // target-required skills only require THAT a locked/nearby target EXISTS, not that it
        // is in range. If it is far away, the spell fires along the Kaeli line up to its limit/wall.
        if (skill.Shape is "single" or "area" or "chain" or "barrage" && target is null) return;
        if (skill.Shape == "chain" && !CanSkillReachTarget(skill, target!)) return;

        // aim point: locked target, but limited to skill range and stopping before the wall
        var aimX = target?.X ?? Player.X;
        var aimY = target?.Y ?? Player.Y;
        if (skill.Shape is "single" or "area" or "barrage" or "field" && target is not null)
        {
            (aimX, aimY) = AimAlongLine(target.X, target.Y, skill.Range);
            if (aimX == Player.X && aimY == Player.Y) return; // wall-adjacent: does not launch (no CD spent)
        }

        if (isUlt)
        {
            // Free-cast (Training) keeps the gauge full so the ult stays spammable.
            if (!FreeCast) _gauge = 0;
            // Echo Thunder Core: using the ultimate refills Charge.
            if (HasEcho("thunder_core")) _staticCharge = GameConfig.RynnaChargeMax;
        }
        else if (!FreeCast) _skillReadyAtMs[slot] = NowMs + (long)(
            skill.CooldownMs
            * Loadout.Mastery.CooldownMult
            * (1 - EquipmentStats.CooldownReduction));

        if (target is not null)
            Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);
        // CUT-05: visual-only cast cue. Carries the skill id + aim point (+ isUlt via Crit) so the
        // client can stamp shape-keyed FX from its own skill-footprint catalog. Pure cosmetics; this
        // event is only appended to the outgoing stream; it never feeds the simulation.
        Emit("skill_cast", Player.X, Player.Y, aimX, aimY, 0, skill.Id, Player.Id, isUlt);

        // mastery: slots 1-4 multiply Power; ultimate amplifies duration/heal (ultmod)
        var ultScale = isUlt ? Loadout.Mastery.UltimatePowerMult : 1.0;
        var damage = PlayerAttack() * RoleSkillMult() * skill.Power * EquipmentStats.SkillPowerMultiplier
                     * (isUlt ? 1.0 : Loadout.Mastery.SlotPowerMult(slot));
        switch (skill.Shape)
        {
            case "buff":
                // KR-00: a buff-shaped skill may arm the shared auto-modifier (empowered autos), toggle
                // a named state (reused by ult-states like ramp_unlocked/auto_chain), and/or heal.
                if (skill.AutoModKind is not null)
                    ActivateAutoModifier(skill, ultScale);
                if (skill.Buff == "heal")
                {
                    HealPlayer((int)Math.Ceiling(Player.MaxHp * GameConfig.NaturesEmbraceHealFraction * ultScale));
                }
                else if (skill.Buff is not null)
                {
                    _buffsUntilMs[skill.Buff] = NowMs + (long)(skill.BuffMs * ultScale);
                }
                Emit("effect", Player.X, Player.Y, 0, 0, skill.EffectId);
                break;

            case "single":
            {
                if (skill.MissileId > 0) Emit("projectile", Player.X, Player.Y, aimX, aimY, skill.MissileId);
                Emit("effect", aimX, aimY, 0, 0, skill.EffectId);
                // hits the locked target if the line reached it; otherwise hits whatever is where the spell stopped
                var victim = aimX == target!.X && aimY == target.Y ? target : MonsterAt(aimX, aimY);
                if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                break;
            }

            case "area":
            {
                if (skill.MissileId > 0) Emit("projectile", Player.X, Player.Y, aimX, aimY, skill.MissileId);
                foreach (var (tx, ty) in CircleTiles(aimX, aimY, SkillRadius(skill.Radius, isUlt)))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "nova":
            {
                var radius = SkillRadius(skill.Radius, isUlt);
                if (skill.Strikes > 0)
                {
                    var strikes = Math.Max(skill.Strikes, 1);
                    var interval = Math.Max(skill.StrikeIntervalMs, GameConfig.TickMs);
                    for (var k = 0; k < strikes; k++)
                        _pendingStrikes.Add(new ScheduledStrike
                        {
                            Floor = _currentFloor, X = Player.X, Y = Player.Y,
                            AtMs = NowMs + (long)k * interval,
                            Element = skill.Element, Fx = skill.EffectId, Damage = damage,
                            Radius = radius, StunMs = skill.StunMs,
                            DetonatesStaticMarks = skill.DetonateStaticMarks,
                            SkillLifesteal = skill.SkillLifesteal,
                            StaticChargeGain = skill.DetonateStaticMarks ? GameConfig.RynnaStormHeartChargePerWave : 0,
                        });
                    Emit("effect", Player.X, Player.Y, 0, 0, skill.EffectId);
                    break;
                }

                var victims = new List<Actor>();
                foreach (var (tx, ty) in CircleTiles(Player.X, Player.Y, radius))
                {
                    if (tx == Player.X && ty == Player.Y) continue;
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null && !victims.Contains(victim)) victims.Add(victim);
                }
                foreach (var victim in victims)
                {
                    if (skill.PullTiles > 0) PullMonsterTowardPlayer(victim, skill.PullTiles);
                    if (skill.EchoShieldOnHit > 0)
                        GainEchoShield(Player.MaxHp * skill.EchoShieldOnHit * ultScale);
                    HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "beam":
            {
                var (dx, dy) = DirDelta(Player.Facing, target);
                for (var i = 1; i <= skill.Range; i++)
                {
                    var tx = Player.X + dx * i;
                    var ty = Player.Y + dy * i;
                    var px = Player.X + dx * (i - 1);
                    var py = Player.Y + dy * (i - 1);
                    if (DiagonalCornerBlocked(Floor, px, py, tx, ty)) break;
                    if (Floor.IsBlocked(tx, ty)) break;
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "cone":
            {
                var (dx, dy) = DirDelta(Player.Facing, target);
                var reach = Math.Max(SkillRadius(skill.Radius, isUlt), 1);
                foreach (var (tx, ty) in ConeTiles(Player.X, Player.Y, dx, dy, reach))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                if (skill.Strikes > 0 && skill.SummonPower > 0)
                {
                    var pulseDamage = PlayerAttack() * RoleSkillMult() * skill.SummonPower
                        * EquipmentStats.SkillPowerMultiplier
                        * (isUlt ? ultScale : Loadout.Mastery.SlotPowerMult(slot));
                    var interval = Math.Max(skill.StrikeIntervalMs, GameConfig.TickMs);
                    var radius = SkillRadius(Math.Max(skill.SummonRadius, 1), isUlt);
                    for (var k = 0; k < skill.Strikes; k++)
                        _pendingStrikes.Add(new ScheduledStrike
                        {
                            Floor = _currentFloor,
                            X = Player.X,
                            Y = Player.Y,
                            AtMs = NowMs + skill.StrikeDelayMs + (long)k * interval,
                            Element = skill.Element,
                            Fx = skill.EffectId,
                            Damage = pulseDamage,
                            Radius = radius,
                        });
                }
                break;
            }

            case "summon":
            {
                // construct dropped on the caster's tile; pulses area damage for SummonMs.
                var pulseMs = Math.Max(skill.SummonPulseMs, GameConfig.TickMs);
                var slotMult = isUlt ? ultScale : Loadout.Mastery.SlotPowerMult(slot);
                var summonPulse = PlayerAttack() * RoleSkillMult() * skill.SummonPower
                    * EquipmentStats.SkillPowerMultiplier * slotMult;
                _summons.Add(new PlayerSummon
                {
                    Floor = _currentFloor, X = Player.X, Y = Player.Y,
                    Element = skill.Element, Fx = skill.EffectId,
                    Radius = Math.Max(skill.SummonRadius, 1),
                    DamagePerPulse = summonPulse,
                    PulseMs = pulseMs,
                    NextPulseAtMs = NowMs + pulseMs,
                    ExpireAtMs = NowMs + Math.Max(skill.SummonMs, pulseMs),
                    Roams = skill.SummonRoams,
                    LeavesField = skill.SummonLeavesField,
                    FieldPower = summonPulse * GameConfig.VelvetShadeCorrosionFraction,
                    FieldTickMs = pulseMs,
                    FieldLifeMs = GameConfig.FieldSpreadChildLifeMs,
                    FieldSpreadChance = skill.FieldSpreadChance,
                    FieldSpreadGenerations = skill.FieldSpreadGenerations,
                });
                Emit("effect", Player.X, Player.Y, 0, 0, skill.EffectId);
                break;
            }

            case "chain":
            {
                if (target is null) break;
                var hit = new HashSet<int> { target.Id };
                var current = target;
                var hops = Math.Max(skill.ChainJumps, 1);
                var falloff = Math.Clamp(skill.ChainFalloff, 0, 0.9);
                var jumpRange = Math.Max(skill.ChainRange, 1);
                int fromX = Player.X, fromY = Player.Y;
                var hopDamage = damage;
                for (var h = 0; h < hops && current is not null; h++)
                {
                    if (skill.MissileId > 0) Emit("projectile", fromX, fromY, current.X, current.Y, skill.MissileId);
                    Emit("effect", current.X, current.Y, 0, 0, skill.EffectId);
                    HitMonster(current, hopDamage, skill, ultScale);
                    fromX = current.X; fromY = current.Y;
                    hopDamage *= 1 - falloff;
                    current = NearestUnhitMonster(fromX, fromY, jumpRange, hit);
                    if (current is not null) hit.Add(current.Id);
                }
                break;
            }

            case "ring":
            {
                // scaled outer radius, but never collapses over the central hole (keeps a valid ring).
                var outer = Math.Max(SkillRadius(skill.Radius, isUlt), skill.RingInner + 1);
                if (skill.Strikes > 0)
                {
                    var availableBands = Math.Max(1, outer - skill.RingInner);
                    var bands = Math.Min(Math.Max(skill.Strikes, 1), availableBands);
                    var interval = Math.Max(skill.StrikeIntervalMs, GameConfig.TickMs);
                    var previousOuter = skill.RingInner;
                    for (var k = 0; k < bands && previousOuter < outer; k++)
                    {
                        var bandOuter = k == bands - 1
                            ? outer
                            : skill.RingInner + (int)Math.Round(
                                (outer - skill.RingInner) * (k + 1) / (double)bands,
                                MidpointRounding.AwayFromZero);
                        bandOuter = Math.Clamp(bandOuter, previousOuter + 1, outer);
                        _pendingStrikes.Add(new ScheduledStrike
                        {
                            Floor = _currentFloor,
                            X = Player.X,
                            Y = Player.Y,
                            AtMs = NowMs + skill.StrikeDelayMs + (long)k * interval,
                            Element = skill.Element,
                            Fx = skill.EffectId,
                            Damage = damage,
                            Radius = bandOuter,
                            RingInner = previousOuter,
                            StunMs = skill.StunMs,
                            SlowFactor = skill.SlowFactor,
                            SlowMs = skill.SlowMs,
                        });
                        previousOuter = bandOuter;
                    }
                    Emit("effect", Player.X, Player.Y, 0, 0, skill.EffectId);
                    break;
                }
                foreach (var (tx, ty) in RingTiles(Player.X, Player.Y, skill.RingInner, outer))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "field":
            {
                // paints danger tiles on the ground (around the target, or itself if no target). With
                // FieldSpreadChance > 0 turns the seed into a self-spreading fire (Contagion).
                var dmg = PlayerAttack() * RoleSkillMult() * skill.SummonPower * EquipmentStats.SkillPowerMultiplier
                    * (isUlt ? ultScale : Loadout.Mastery.SlotPowerMult(slot));
                var tickMs = Math.Max(skill.SummonPulseMs, GameConfig.TickMs);
                var lifeMs = Math.Max(skill.SummonMs, tickMs);
                foreach (var (tx, ty) in CircleTiles(aimX, aimY, Math.Max(skill.SummonRadius, 0)))
                    SpawnField(tx, ty, skill.Element, skill.EffectId, dmg, skill.SlowFactor, skill.SlowMs,
                        tickMs, lifeMs, skill.FieldSpreadChance, skill.FieldSpreadGenerations, telegraph: true);
                break;
            }

            case "barrage":
            {
                // hits at multiple timings on the target point (rain/barrage landing in sequence).
                var strikes = Math.Max(skill.Strikes, 1);
                var interval = Math.Max(skill.StrikeIntervalMs, GameConfig.TickMs);
                var dotPerTick = PlayerAttack() * RoleSkillMult() * skill.DotPower * EquipmentStats.SkillPowerMultiplier;
                var fieldTickMs = Math.Max(skill.SummonPulseMs, GameConfig.TickMs);
                var fieldPower = PlayerAttack() * RoleSkillMult() * skill.SummonPower
                    * EquipmentStats.SkillPowerMultiplier * (isUlt ? ultScale : Loadout.Mastery.SlotPowerMult(slot));
                for (var k = 0; k < strikes; k++)
                    _pendingStrikes.Add(new ScheduledStrike
                    {
                        Floor = _currentFloor, X = aimX, Y = aimY,
                        AtMs = NowMs + skill.StrikeDelayMs + (long)k * interval,
                        Element = skill.Element, Fx = skill.EffectId, Damage = damage,
                        Radius = SkillRadius(skill.Radius, isUlt), RingInner = skill.RingInner,
                        StunMs = skill.StunMs, SlowFactor = skill.SlowFactor, SlowMs = skill.SlowMs,
                        DotTicks = skill.DotTicks, DotTickMs = skill.DotTickMs, DotPower = dotPerTick,
                        LeavesField = skill.StrikeLeavesField, FieldPower = fieldPower,
                        FieldRadius = Math.Max(skill.SummonRadius, 0), FieldTickMs = fieldTickMs,
                        FieldLifeMs = Math.Max(skill.SummonMs, fieldTickMs),
                        FieldSpreadChance = skill.FieldSpreadChance, FieldSpreadGenerations = skill.FieldSpreadGenerations,
                        FieldSlowFactor = skill.SlowFactor, FieldSlowMs = skill.SlowMs,
                        StacksBurnMult = skill.StackBurnMult,
                    });
                Emit("effect", aimX, aimY, 0, 0, skill.EffectId); // telegrafo inicial
                break;
            }
        }

        // Reign of Shadows (§4G): the ult cashes in the whole run's harvest — every Death Orb still
        // waiting out its delay bursts now, alongside the shadow rain above.
        if (skill.DetonateDeathOrbs) DetonatePendingDeathOrbs();
    }

    private void HitMonster(Actor monster, double damage, SkillDef skill, double buffScale = 1.0)
    {
        // Wildfire Reckoning (§4F): read the pending burn BEFORE the hit re-ignites it via Contagion, so the
        // reap scales with what the target had accumulated (amplified by the ult's burn multiplier), not a
        // fresh burn. Reaped after the hit's riders resolve.
        double burnToReap = 0;
        if (skill.ConsumeBurnBonus > 0)
        {
            foreach (var d in monster.Dots)
                if (d.Element == "fire") burnToReap += d.DamagePerTick * d.TicksLeft;
            burnToReap *= 1 + RinBurnMult();
        }

        if (skill.Power > 0)
        {
            // Soul Rend (§4G): a finisher — extra damage against an already-wounded target.
            if (skill.LowHpBonus > 0 && monster.Hp < monster.MaxHp * skill.LowHpThreshold)
                damage *= 1 + skill.LowHpBonus;
                DealDamageToMonster(monster, damage, skill.Element, 0, fromSkill: true,
                traitChargeBonus: skill.TraitChargeBonus,
                consumePreyRampBonus: skill.ConsumePreyRampBonus,
                skillLifesteal: skill.SkillLifesteal);
        }
        if (monster.Hp <= 0) return;

        switch (skill.Buff)
        {
            case "taunt":
                AcquirePlayer(monster);
                // Taunt: the enemy (even ranged/kiting) is forced into melee by BuffMs.
                monster.TauntedUntilMs = NowMs + (long)(skill.BuffMs * buffScale);
                Emit("text", monster.X, monster.Y, 0, 0, 0, "TAUNTED!");
                break;
            case "exposed":
                monster.ExposedUntilMs = NowMs + (long)(skill.BuffMs * buffScale);
                break;
            case "sapped":
                monster.SappedUntilMs = NowMs + (long)(skill.BuffMs * buffScale);
                break;
        }

        if (monster.Hp > 0 && skill.StunMs > 0)
        {
            monster.StunUntilMs = NowMs + skill.StunMs;
            Emit("effect", monster.X, monster.Y, 0, 0, 32); // stun stars
        }

        if (monster.Hp > 0 && skill.DotTicks > 0 && skill.DotPower > 0)
            ApplyDotToMonster(monster, skill.Element, skill.EffectId,
                PlayerAttack() * RoleSkillMult() * skill.DotPower * EquipmentStats.SkillPowerMultiplier * buffScale,
                skill.DotTicks, skill.DotTickMs > 0 ? skill.DotTickMs : GameConfig.ConditionDefaultTickMs);

        if (monster.Hp > 0 && skill.SlowMs > 0 && skill.SlowFactor < 1)
            ApplyMonsterSlow(monster, skill.SlowFactor, skill.SlowMs);

        if (monster.Hp > 0 && skill.MassShatterFrost)
            MassShatterFrost(monster);

        // Wildfire Reckoning (§4F): cash in the pending burn as a burst, then leave a light ember.
        if (skill.ConsumeBurnBonus > 0 && burnToReap > 0 && monster.Hp > 0)
            ReapBurn(monster, burnToReap, skill.ConsumeBurnBonus);
    }

    /// <summary>Wildfire Reckoning (§4F): cash in a target's pending burn. burnToReap is the pre-hit fire
    /// damage (already amplified by any Infernal Ball multiplier). Consumes the fire DoT — the deliberate
    /// contrast with the ult, which never consumes — dealing bonusMult × of it as an instant burst, then
    /// re-seeds a short faint ember so the DoT engine keeps ticking. The burst is fromTrait so it never
    /// re-ignites via Contagion. Deterministic: no Rng.</summary>
    private void ReapBurn(Actor monster, double burnToReap, double bonusMult)
    {
        monster.Dots.RemoveAll(d => d.Element == "fire"); // consume the accumulated burn (incl. this hit's re-ignite)
        var burst = burnToReap * bonusMult;
        if (burst >= 1)
        {
            Emit("text", monster.X, monster.Y, 0, 0, 0, "REAP");
            DealDamageToMonster(monster, burst, "fire", GameConfig.ConditionTickFx["fire"],
                fromSkill: false, canCrit: false, canLifeSteal: false, fromTrait: true);
        }
        if (monster.Hp <= 0) return;
        // light ember: a faint burn left behind so Contagion has something to keep spreading from.
        ApplyDotToMonster(monster, "fire", GameConfig.ConditionTickFx["fire"],
            PlayerAttack() * RoleSkillMult() * GameConfig.RinContagionBurnPower * GameConfig.RinReckoningEmberPowerFraction,
            GameConfig.RinReckoningEmberTicks, GameConfig.RinContagionBurnTickMs);
    }

    /// <summary>Slows a monster's movement (reuses the chiller/reaction slow fields).</summary>
    private void ApplyMonsterSlow(Actor monster, double factor, int ms)
    {
        monster.SlowUntilMs = NowMs + ms;
        monster.SlowFactor = Math.Clamp(factor, GameConfig.SlowFactorFloor, 1.0);
    }

    /// <summary>Adds (or refreshes) a damage-over-time on a monster. Same element never stacks:
    /// reapplying keeps the stronger per-tick and the longer duration (no infinite ramp).</summary>
    private void ApplyDotToMonster(Actor monster, string element, int fx, double dmgPerTick, int ticks, int tickMs)
    {
        var perTick = Math.Max(dmgPerTick, 1);
        tickMs = Math.Max(tickMs, GameConfig.TickMs);
        foreach (var existing in monster.Dots)
        {
            if (existing.Element != element) continue;
            existing.DamagePerTick = Math.Max(existing.DamagePerTick, perTick);
            existing.TicksLeft = Math.Max(existing.TicksLeft, ticks);
            existing.TickMs = tickMs;
            existing.NextTickAtMs = Math.Min(existing.NextTickAtMs, NowMs + tickMs);
            return;
        }
        monster.Dots.Add(new MonsterDot
        {
            Element = element, Fx = fx, DamagePerTick = perTick,
            TicksLeft = ticks, TickMs = tickMs, NextTickAtMs = NowMs + tickMs,
        });
    }

    /// <summary>Ticks the DoTs on one monster. Reuses DealDamageToMonster so death/xp/posture/
    /// reactions all behave identically to a normal hit (no crit, no lifesteal for ticks).</summary>
    private void TickMonsterDots(Actor monster)
    {
        for (var i = monster.Dots.Count - 1; i >= 0; i--)
        {
            var dot = monster.Dots[i];
            if (NowMs < dot.NextTickAtMs) continue;
            dot.NextTickAtMs = NowMs + dot.TickMs;
            dot.TicksLeft--;
            // Infernal Ball (§4F): the room-wide multiplier stokes each fire burn tick hotter.
            var tickDmg = dot.Element == "fire" && _trait.Kind == "contagion"
                ? dot.DamagePerTick * (1 + RinBurnMult())
                : dot.DamagePerTick;
            DealDamageToMonster(monster, tickDmg, dot.Element, dot.Fx,
                fromSkill: false, canCrit: false, canLifeSteal: false);
            if (monster.Hp <= 0) { monster.Dots.Clear(); return; }
            if (dot.TicksLeft <= 0)
            {
                monster.Dots.RemoveAt(i);
                OnConditionExpiredCard(monster, dot); // G-04 Detonation: expires -> area burst
                if (monster.Hp <= 0) return;
            }
        }
    }

    /// <summary>Ticks player-summoned constructs: each pulses area damage on its tile, then expires.</summary>
    private void TickPlayerSummons()
    {
        if (_summons.Count == 0) return;
        for (var i = _summons.Count - 1; i >= 0; i--)
        {
            var summon = _summons[i];
            if (NowMs >= summon.ExpireAtMs) { _summons.RemoveAt(i); continue; }
            if (summon.Floor != _currentFloor || NowMs < summon.NextPulseAtMs) continue;
            summon.NextPulseAtMs = NowMs + summon.PulseMs;
            // Roaming shade: drift one cardinal step toward the nearest living enemy before pulsing,
            // and leave a spreading corrosion field on the tile it now occupies.
            if (summon.Roams)
            {
                var prey = NearestLivingMonster(summon.X, summon.Y, _ => true);
                if (prey is not null)
                {
                    var stepX = summon.X + Math.Sign(prey.X - summon.X);
                    var stepY = summon.Y;
                    if (prey.X == summon.X || Math.Abs(prey.Y - summon.Y) > Math.Abs(prey.X - summon.X))
                    {
                        stepX = summon.X;
                        stepY = summon.Y + Math.Sign(prey.Y - summon.Y);
                    }
                    if (!Floor.IsBlocked(stepX, stepY)) { summon.X = stepX; summon.Y = stepY; }
                }
            }
            if (summon.LeavesField && summon.FieldPower > 0)
                SpawnField(summon.X, summon.Y, summon.Element, summon.Fx, summon.FieldPower, 1, 0,
                    summon.FieldTickMs, summon.FieldLifeMs, summon.FieldSpreadChance,
                    summon.FieldSpreadGenerations, telegraph: false);
            Emit("effect", summon.X, summon.Y, 0, 0, summon.Fx);
            foreach (var (tx, ty) in CircleTiles(summon.X, summon.Y, summon.Radius))
            {
                var victim = MonsterAt(tx, ty);
                if (victim is null) continue;
                if (tx != summon.X || ty != summon.Y) Emit("effect", tx, ty, 0, 0, summon.Fx);
                DealDamageToMonster(victim, summon.DamagePerPulse, summon.Element, 0,
                    fromSkill: false, canCrit: false, canLifeSteal: false);
            }
        }
    }

    /// <summary>Nearest living monster on the current floor within range, excluding ids in <paramref name="exclude"/>. For chain jumps.</summary>
    private Actor? NearestUnhitMonster(int x, int y, int range, HashSet<int> exclude)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || exclude.Contains(m.Id)) continue;
            var d = Chebyshev(x, y, m.X, m.Y);
            if (d <= range && (d < bestDist || (d == bestDist && (best is null || m.Id < best.Id))))
            {
                bestDist = d;
                best = m;
            }
        }
        return best;
    }

    /// <summary>Hollow diamond: tiles whose Manhattan distance is in (inner, outer]. inner 0 = solid (= CircleTiles).</summary>
    private IEnumerable<(int X, int Y)> RingTiles(int cx, int cy, int inner, int outer)
    {
        for (var dy = -outer; dy <= outer; dy++)
            for (var dx = -outer; dx <= outer; dx++)
            {
                var dist = Math.Abs(dx) + Math.Abs(dy);
                if (dist > outer * GameConfig.AoeRoundingFactor || dist <= inner) continue;
                var x = cx + dx;
                var y = cy + dy;
                if (!Floor.IsBlocked(x, y)) yield return (x, y);
            }
    }

    /// <summary>Resolves one scheduled multi-time strike on its footprint (circle or hollow ring).</summary>
    private void ResolveStrike(ScheduledStrike s)
    {
        if (s.Floor != _currentFloor) return;
        // Soul Detonation: while an orb burst resolves, suppress new orbs from its kills (no cascade).
        var prevOrb = _resolvingDeathOrb;
        if (s.IsDeathOrb) _resolvingDeathOrb = true;
        try
        {
            ResolveStrikeBody(s);
        }
        finally
        {
            _resolvingDeathOrb = prevOrb;
        }
    }

    private void ResolveStrikeBody(ScheduledStrike s)
    {
        // Infernal Ball (§4F): every meteor impact stokes the room's burn hotter (no consume).
        if (s.StacksBurnMult) AddBurnMultStack();
        if (s.StaticChargeGain > 0) AddRynnaCharge(s.StaticChargeGain);
        // fire trail: the meteor ignites the ground where it lands, and the fire spreads (Contagion).
        if (s.LeavesField && s.FieldPower > 0)
            SpawnField(s.X, s.Y, s.Element, s.Fx, s.FieldPower, s.FieldSlowFactor, s.FieldSlowMs,
                s.FieldTickMs, s.FieldLifeMs, s.FieldSpreadChance, s.FieldSpreadGenerations, telegraph: false);
        var tiles = s.RingInner > 0 ? RingTiles(s.X, s.Y, s.RingInner, s.Radius) : CircleTiles(s.X, s.Y, s.Radius);
        foreach (var (tx, ty) in tiles)
        {
            Emit("effect", tx, ty, 0, 0, s.Fx);
            var victim = MonsterAt(tx, ty);
            if (victim is null) continue;
            DealDamageToMonster(victim, s.Damage, s.Element, 0, fromSkill: true, canCrit: false,
                canLifeSteal: !s.DetonatesStaticMarks, fromTrait: s.DetonatesStaticMarks,
                skillLifesteal: s.SkillLifesteal);
            if (victim.Hp <= 0) continue;
            if (s.DetonatesStaticMarks && ActiveStaticMark(victim))
                RynnaDetonateStaticMark(victim, massDetonation: true);
            if (victim.Hp <= 0) continue;
            if (s.StunMs > 0) victim.StunUntilMs = NowMs + s.StunMs;
            if (s.SlowMs > 0 && s.SlowFactor < 1) ApplyMonsterSlow(victim, s.SlowFactor, s.SlowMs);
            if (s.DotTicks > 0 && s.DotPower > 0)
                ApplyDotToMonster(victim, s.Element, s.Fx, s.DotPower, s.DotTicks,
                    s.DotTickMs > 0 ? s.DotTickMs : GameConfig.ConditionDefaultTickMs);
        }
    }

    /// <summary>Creates one ground-field tile (terrain hazard). With spreadChance > 0 it will crawl to
    /// free neighbours over time (Contagion). No-op on blocked tiles. telegraph=true emits the ignition FX.</summary>
    private void SpawnField(int x, int y, string element, int fx, double dmg, double slowFactor, int slowMs,
        int tickMs, int lifeMs, int spreadChance, int spreadGenerations, bool telegraph)
    {
        if (Floor.IsBlocked(x, y)) return;
        tickMs = Math.Max(tickMs, GameConfig.TickMs);
        if (telegraph) Emit("effect", x, y, 0, 0, fx);
        _fields.Add(new GroundField
        {
            Floor = _currentFloor, X = x, Y = y, Element = element, Fx = fx,
            DamagePerTick = dmg, SlowFactor = slowFactor, SlowMs = slowMs,
            TickMs = tickMs, NextTickAtMs = NowMs + tickMs,
            ExpireAtMs = NowMs + Math.Max(lifeMs, tickMs),
            SpreadChance = spreadChance, SpreadGenerationsLeft = spreadGenerations,
        });
    }

    /// <summary>Ticks player-painted ground fields: each damages/slows the monster on its tile, may
    /// ignite a free neighbour (spreading fire), then expires. Children are queued and only added after
    /// the pass, so a tile never re-spreads within the same tick (bounded, deterministic crawl).</summary>
    private void TickFields()
    {
        if (_fields.Count == 0) return;
        var floorFields = 0;
        foreach (var f in _fields) if (f.Floor == _currentFloor) floorFields++;
        List<GroundField>? newborns = null;
        var initialCount = _fields.Count;
        for (var i = initialCount - 1; i >= 0; i--)
        {
            var field = _fields[i];
            if (NowMs >= field.ExpireAtMs)
            {
                if (field.Floor == _currentFloor) floorFields--;
                _fields.RemoveAt(i);
                continue;
            }
            if (field.Floor != _currentFloor || NowMs < field.NextTickAtMs) continue;
            field.NextTickAtMs = NowMs + field.TickMs;
            Emit("effect", field.X, field.Y, 0, 0, field.Fx);
            var victim = MonsterAt(field.X, field.Y);
            if (victim is not null)
            {
                if (field.DamagePerTick > 0)
                    DealDamageToMonster(victim, field.DamagePerTick, field.Element, 0,
                        fromSkill: false, canCrit: false, canLifeSteal: false);
                if (victim.Hp > 0 && field.SlowMs > 0 && field.SlowFactor < 1)
                    ApplyMonsterSlow(victim, field.SlowFactor, field.SlowMs);
            }

            // Contagion: the lit tile tries to ignite a free neighbor while generation budget remains
            // and the floor has not hit the tile cap. The fire front advances; the tail fades by itself.
            if (field.SpreadGenerationsLeft > 0 && field.SpreadChance > 0
                && floorFields < GameConfig.FieldMaxTilesPerFloor && _rng.Chance(field.SpreadChance / 100.0))
            {
                var child = MakeSpreadChild(field, newborns);
                if (child is not null) { (newborns ??= []).Add(child); floorFields++; }
            }
        }
        if (newborns is not null) _fields.AddRange(newborns);
    }

    /// <summary>Picks a free cardinal neighbour of a burning tile (not blocked, not already on fire) and
    /// returns a fresh short-lived child field with one less spread generation. Null if boxed in.</summary>
    private GroundField? MakeSpreadChild(GroundField parent, List<GroundField>? pending)
    {
        Span<int> dx = [1, -1, 0, 0];
        Span<int> dy = [0, 0, 1, -1];
        // collect candidates in a stable order and roll one (deterministic on the run Rng).
        Span<int> okX = stackalloc int[4];
        Span<int> okY = stackalloc int[4];
        var n = 0;
        for (var k = 0; k < 4; k++)
        {
            var nx = parent.X + dx[k];
            var ny = parent.Y + dy[k];
            if (Floor.IsBlocked(nx, ny) || FieldAlreadyAt(nx, ny, pending)) continue;
            okX[n] = nx; okY[n] = ny; n++;
        }
        if (n == 0) return null;
        var pick = _rng.Next(n);
        var cx = okX[pick];
        var cy = okY[pick];
        Emit("effect", cx, cy, 0, 0, parent.Fx);
        return new GroundField
        {
            Floor = _currentFloor, X = cx, Y = cy, Element = parent.Element, Fx = parent.Fx,
            DamagePerTick = parent.DamagePerTick, SlowFactor = parent.SlowFactor, SlowMs = parent.SlowMs,
            TickMs = parent.TickMs, NextTickAtMs = NowMs + parent.TickMs,
            ExpireAtMs = NowMs + GameConfig.FieldSpreadChildLifeMs,
            SpreadChance = parent.SpreadChance, SpreadGenerationsLeft = parent.SpreadGenerationsLeft - 1,
        };
    }

    /// <summary>True if a live field already occupies the tile on the current floor (incl. this tick's newborns).</summary>
    private bool FieldAlreadyAt(int x, int y, List<GroundField>? pending)
    {
        foreach (var f in _fields)
            if (f.Floor == _currentFloor && f.X == x && f.Y == y) return true;
        if (pending is not null)
            foreach (var f in pending)
                if (f.X == x && f.Y == y) return true;
        return false;
    }

    /// <summary>Resolves multi-time strikes whose scheduled moment has arrived.</summary>
    private void TickPendingStrikes()
    {
        if (_pendingStrikes.Count == 0) return;
        for (var i = _pendingStrikes.Count - 1; i >= 0; i--)
        {
            var strike = _pendingStrikes[i];
            if (NowMs < strike.AtMs) continue;
            _pendingStrikes.RemoveAt(i);
            ResolveStrike(strike);
        }
    }

    private void DealDamageToMonster(Actor monster, double raw, string element, int hitEffect,
        bool fromSkill = false, bool canCrit = true, bool canLifeSteal = true, bool fromTrait = false,
        int traitChargeBonus = 0, double consumePreyRampBonus = 0, double skillLifesteal = 0)
    {
        // "direct hit" = auto-attack or skill hit (not DoT/field/summon/trait burst).
        // This drives passive state (combo, charge, mark, prey, shatter).
        var directHit = (fromSkill || canCrit) && !fromTrait;

        var roll = raw * (GameConfig.DamageRollMin + _rng.NextDouble() * (GameConfig.DamageRollMax - GameConfig.DamageRollMin));
        if (NowMs < monster.ExposedUntilMs)
            roll *= GameConfig.ExposedWeaknessDamageMultiplier;

        // trait: deadeye adds crit by distance; executioner/slayer multiply the roll
        var critChance = CritChance();
        if (_trait.Kind == "deadeye" && Chebyshev(Player, monster) >= (int)_trait.Param)
            critChance += _trait.Value * _traitMult;

        // K-04: pre-damage signature passives (ramp/execution/bonus + guaranteed crit)
        var forceCrit = false;
        if (!fromTrait)
            ApplyTraitPreDamage(monster, element, directHit, ref roll, ref forceCrit, consumePreyRampBonus);

        var crit = canCrit && (forceCrit || _rng.Chance(critChance));
        if (crit) roll *= GameConfig.CritMultiplier + EquipmentStats.CritDamage;

        if (_trait.Kind == "executioner" && monster.Hp < monster.MaxHp * _trait.Param)
            roll *= 1 + _trait.Value * _traitMult;
        if (_trait.Kind == "slayer" && monster.Species!.BestiaryClass == _trait.Tag)
            roll *= 1 + _trait.Value * _traitMult;

        // element bonus card
        if (element == Waifu.Element) roll *= 1 + CardValue("elementPercent");
        if (element == CurrentStance.Element)
        {
            if (element == EquipmentStats.WeaponElement)
                roll *= 1 + GameConfig.EquipmentWeaponElementMatchDamageBonus;
            if (element == EquipmentStats.Element && EquipmentStats.ElementDamageBonus > 0)
                roll *= 1 + EquipmentStats.ElementDamageBonus;
        }

        // bestiary mastery bonus
        var rank = BestiaryRank(monster.Species!.StableId);
        roll *= 1 + rank * GameConfig.BestiaryDamageBonusPerRank;

        // tibia armor + elemental resistance
        var afterArmor = roll - _rng.Range(0, Math.Max(monster.Species.Armor / 2, 0)) / Math.Max(monster.StatMult, 1);
        var resist = monster.Species.Elements.GetValueOrDefault(element, 0);
        afterArmor *= (100 - resist) / 100.0;

        // F-E: echo break amplifies every hit landed during the stagger window
        var staggered = monster.IsStaggered(NowMs);
        if (staggered)
        {
            afterArmor *= monster.StaggerMultiplier;
            if (NowMs >= monster.PostureBonusReadyAtMs)
            {
                afterArmor += monster.MaxHp * GameConfig.PostureMaxHpBonusPct;
                monster.PostureBonusReadyAtMs = NowMs + GameConfig.PostureMaxHpBonusCooldownMs;
            }
        }

        var final = Math.Max((int)afterArmor, resist >= 100 ? 0 : 1);
        if (final <= 0)
        {
            Emit("text", monster.X, monster.Y, 0, 0, 0, "IMMUNE");
            return;
        }

        // G-08B: shieldbearer barrier absorbs before health (removes the hit if it covers everything).
        if (monster.MonsterShield > 0)
        {
            var absorbed = Math.Min(monster.MonsterShield, final);
            monster.MonsterShield -= absorbed;
            final -= (int)absorbed;
            if (final <= 0)
            {
                Emit("text", monster.X, monster.Y, 0, 0, 0, "BLOCKED");
                return;
            }
        }

        monster.Hp -= final;
        Emit("damage", monster.X, monster.Y, 0, 0, final, "", monster.Id, crit);
        if (hitEffect > 0) Emit("effect", monster.X, monster.Y, 0, 0, hitEffect);
        if (crit) Emit("effect", monster.X, monster.Y, 0, 0, 173);

        var lifesteal = canLifeSteal ? CardValue("lifesteal") + GameConfig.BaselineLifesteal : 0;
        if (canLifeSteal && IsBuffActive(GameConfig.SerenWarCadenceBuff))
            lifesteal += GameConfig.SerenWarCadenceLifesteal;
        if (canLifeSteal && IsBuffActive(GameConfig.RynnaBloodlustBuff))
            lifesteal += GameConfig.RynnaBloodlustLifesteal;
        if (canLifeSteal && skillLifesteal > 0)
            lifesteal += skillLifesteal;
        if (canLifeSteal && EquipmentStats.LifeStealAmount > 0
            && _rng.Chance(EquipmentStats.LifeStealChance))
            lifesteal += EquipmentStats.LifeStealAmount;
        if (lifesteal > 0) HealPlayer((int)Math.Max(final * lifesteal, 0));

        // reserve traits after damage: vital sap (skill lifesteal) and northern bite (ice slow)
        if (fromSkill && _trait.Kind == "skill_lifesteal")
            HealPlayer((int)Math.Max(final * _trait.Value * _traitMult, 0));
        if (_trait.Kind == "chiller" && element == "ice" && monster.Hp > 0)
        {
            monster.SlowUntilMs = NowMs + (long)_trait.Param;
            monster.SlowFactor = Math.Max(1 - _trait.Value * _traitMult, GameConfig.SlowFactorFloor);
        }

        // K-04: post-damage signature passives (marks, stacks, charge, shatter, contagion)
        if (!fromTrait)
            ApplyTraitPostDamage(monster, final, element, directHit, fromSkill, traitChargeBonus);
        // G-04: post-damage mechanic cards (fill ult, extra strike): same seam, no new dispatch.
        if (!fromTrait)
            ApplyCardPostDamage(monster, directHit);

        // F-E: posture build (boss only) and elemental reactions (any target)
        if (monster.Hp > 0 && monster.PostureMax > 0 && !staggered)
            AddPosture(monster, element, fromSkill);
        if (monster.Hp > 0)
            ApplyElementMarkAndReactions(monster, element, final);

        // aggro: damaged monsters retaliate
        if (monster.TargetId == 0) AcquirePlayer(monster);

        if (monster.Hp <= 0) KillMonster(monster);
    }

    // ================= G-08B: keyword interaction (mob x G-04 tags) =================
    // A mob may resist (0-100%) or amplify (negative) a card keyword. Multiplies the
    // magnitude of that tag effect applied TO the mob. Deterministic: the multiplier is pure; the
    // whole-stack rounding uses _rng ONLY when resistance is configured (does not perturb
    // the RNG stream for mobs without keyword resist, preserving determinism of existing runs).

    /// <summary>Fraction of the keyword affecting the mob: 1 = normal, 0 = immune (100), >1 = amplified (negative).</summary>
    private static double KeywordResistMult(Actor m, string tag)
    {
        var dict = m.Species?.KeywordResistances;
        if (dict is null || dict.Count == 0 || !dict.TryGetValue(tag, out var pct)) return 1.0;
        return Math.Max(1 - pct / 100.0, 0);
    }

    private static bool HasKeywordResist(Actor m, string tag) =>
        m.Species?.KeywordResistances is { Count: > 0 } d && d.ContainsKey(tag);

    /// <summary>Scales an integer stack gain by keyword resistance. No entry -> returns the raw
    /// value (does not touch _rng). With entry -> integer part + 1 probabilistic extra by fraction.</summary>
    private int KeywordScaledStacks(Actor m, string tag, int baseStacks)
    {
        if (baseStacks <= 0 || !HasKeywordResist(m, tag)) return baseStacks;
        var scaled = baseStacks * KeywordResistMult(m, tag);
        var whole = (int)Math.Floor(scaled);
        var frac = scaled - whole;
        if (frac > 0 && _rng.NextDouble() < frac) whole++;
        return whole;
    }

    // ================= K-04: passivas assinatura =================
    // One mechanical family per Kaeli. Deterministic: only NowMs/_rng from the run, stable counters, and
    // target selection by shortest distance with lowest-id tie-break. _traitMult (mastery) always
    // amplifies the main effect. Internal bursts call DealDamageToMonster with fromTrait:true
    // so it does not retrigger its own passive (Killed guard prevents double death counting).

    /// <summary>Pre-damage: ramp/execution/bonus that multiply the roll, and guaranteed crit (Seren).</summary>
    private void ApplyTraitPreDamage(Actor monster, string element, bool directHit, ref double roll,
        ref bool forceCrit, double consumePreyRampBonus = 0)
    {
        switch (_trait.Kind)
        {
            case "discipline": // Seren: combo on the same target, 3rd hit = Perfect Cut
            {
                if (!directHit) break;
                var rampUnlocked = IsBuffActive(GameConfig.UltStateRampUnlocked);
                if (!rampUnlocked && (_comboTargetId != monster.Id || NowMs > _comboExpireMs))
                {
                    _comboTargetId = monster.Id;
                    _comboHits = 0;
                }
                if (rampUnlocked)
                    _comboTargetId = monster.Id;
                _comboHits++;
                // Echo Endless Cadence: harsher reset, uncapped ramp.
                _comboExpireMs = NowMs + (HasEcho("endless_cadence")
                    ? GameConfig.EchoEndlessCadenceResetMs : GameConfig.SerenDisciplineResetMs);
                var ramp = rampUnlocked ? _trait.Param : HasEcho("endless_cadence")
                    ? _comboHits * _trait.Value
                    : Math.Min(_comboHits * _trait.Value, _trait.Param);
                // G-08B: keyword "combo": resists/amplifies Discipline ramp against this target.
                roll *= 1 + ramp * _traitMult * KeywordResistMult(monster, "combo");
                // Echo Perfect Execution: Cut every 2nd hit, and guaranteed crit executes weak targets.
                var cutEvery = HasEcho("perfect_execution")
                    ? GameConfig.EchoPerfectCutEvery : GameConfig.SerenPerfectCutEvery;
                if (rampUnlocked || _comboHits % cutEvery == 0)
                {
                    forceCrit = true;
                    if (HasEcho("perfect_execution")
                        && monster.Hp < monster.MaxHp * GameConfig.EchoPerfectExecuteHpFraction)
                    {
                        Emit("text", monster.X, monster.Y, 0, 0, 0, "EXECUTION");
                        roll = Math.Max(roll, monster.MaxHp * 10); // lethal burst through armor
                    }
                }
                break;
            }

            case "decay": // Velvet: execution threshold rises with Decay stacks
            {
                var threshold = Math.Min(
                    _trait.Param + ActiveDecayStacks(monster) * GameConfig.VelvetThresholdPerStack,
                    GameConfig.VelvetThresholdCap);
                if (monster.Hp < monster.MaxHp * threshold)
                    roll *= 1 + _trait.Value * _traitMult;
                break;
            }

            case "shatter": // Lunara: bonus damage against an already frosted target
                if (directHit && ActiveFrostStacks(monster) > 0)
                    roll *= 1 + _trait.Value * _traitMult;
                break;

            case "contagion": // Rin: Echo Pyre, damage grows with the number of burning enemies
                if (HasEcho("pyre"))
                {
                    var burning = _monsters.Count(m => m.Hp > 0 && m.Floor == _currentFloor && IsBurning(m));
                    roll *= 1 + Math.Min(burning * GameConfig.EchoPyreDamagePerBurning, GameConfig.EchoPyreMaxBonus);
                }
                break;

            case "prey": // Gaia: hunt-time ramp against the Prey
            {
                if (!directHit) break;
                if (_preyId == 0 || !IsMonsterAlive(_preyId)) SetPrey(monster);
                // Echo Pack: marks a second Prey when there are more targets.
                if (HasEcho("pack") && (_preyId2 == 0 || !IsMonsterAlive(_preyId2)))
                    SetSecondPrey(monster);
                if (monster.Id == _preyId || monster.Id == _preyId2)
                {
                    // Echo Eternal Hunt: doubled ramp and cap.
                    var rampPerSec = _trait.Value * (HasEcho("eternal_hunt") ? GameConfig.EchoEternalHuntRampMult : 1);
                    var cap = _trait.Param * (HasEcho("eternal_hunt") ? GameConfig.EchoEternalHuntCapMult : 1);
                    var huntSec = (NowMs - _preyStartMs) / 1000.0;
                    var huntRamp = Math.Min(huntSec * rampPerSec, cap) * _traitMult
                                   * KeywordResistMult(monster, "prey");
                    // G-08B: keyword "prey": resists/amplifies the hunt ramp against this target.
                    roll *= 1 + huntRamp;
                    if (consumePreyRampBonus > 0 && monster.Id == _preyId)
                    {
                        roll *= 1 + huntRamp * consumePreyRampBonus;
                        _preyStartMs = NowMs;
                        Emit("text", monster.X, monster.Y, 0, 0, 0, "EXECUTE");
                    }
                    // Echo Deep Roots: each hit on the Prey roots it and plants earth poison.
                    if (HasEcho("deep_roots") && monster.Hp > 0)
                    {
                        monster.SlowUntilMs = NowMs + GameConfig.EchoDeepRootsSlowMs;
                        monster.SlowFactor = GameConfig.EchoDeepRootsSlowFactor;
                        ApplyDotToMonster(monster, "earth", GameConfig.ConditionTickFx["poison"],
                            PlayerAttack() * RoleSkillMult() * GameConfig.EchoDeepRootsDotPower,
                            GameConfig.EchoDeepRootsDotTicks, GameConfig.EchoDeepRootsDotTickMs);
                    }
                }
                break;
            }
        }
    }

    /// <summary>Post-damage: marks, stacks, charge, contagion, and shatter (target still alive).</summary>
    private void ApplyTraitPostDamage(Actor monster, int final, string element, bool directHit, bool fromSkill,
        int traitChargeBonus = 0)
    {
        switch (_trait.Kind)
        {
            case "judgment": // Eloa: Sin accumulates; when Judged, the next hit detonates
                if (!directHit || monster.Hp <= 0) break;
                // Echo Sentence: Judges with fewer Sins.
                var sinToJudge = HasEcho("sentence")
                    ? GameConfig.EchoSentenceStacksToJudge : GameConfig.EloaSinStacksToJudge;
                if (ActiveSinStacks(monster) >= sinToJudge)
                {
                    monster.SinStacks = 0;
                    monster.SinUntilMs = 0;
                    EloaDetonate(monster, final);
                }
                else
                {
                    // G-08B: keyword "sin": resistant target accumulates Sin more slowly (immune = never).
                    // Judging Lance (§4E) seeds traitChargeBonus extra Sin on top of the base 1.
                    var sinGain = KeywordScaledStacks(monster, "sin", 1 + traitChargeBonus);
                    if (sinGain > 0)
                    {
                        monster.SinStacks = ActiveSinStacks(monster) + sinGain;
                        monster.SinUntilMs = NowMs + GameConfig.EloaSinDurationMs;
                    }
                }
                break;

            case "decay": // Velvet: each hit stacks Decay (DoT) and raises the threshold
                if (!directHit || monster.Hp <= 0) break;
                // G-08B: keyword "curse": resistant target receives fewer Curse stacks (immune = none).
                var decayGain = KeywordScaledStacks(monster, "curse", 1);
                if (decayGain <= 0) break;
                monster.DecayStacks = Math.Min(ActiveDecayStacks(monster) + decayGain, GameConfig.VelvetDecayMaxStacks);
                monster.DecayUntilMs = NowMs + GameConfig.VelvetDecayDurationMs;
                var decayPower = PlayerAttack() * RoleSkillMult() * GameConfig.VelvetDecayDamagePerStack * monster.DecayStacks;
                ApplyDotToMonster(monster, "death", GameConfig.ConditionTickFx["curse"],
                    decayPower, GameConfig.VelvetDecayTicks, GameConfig.VelvetDecayTickMs);
                // Echo Blood Pact: Curse charge raises shield instead of healing Velvet.
                if (HasEcho("blood_pact"))
                    GainEchoShield(decayPower * GameConfig.VelvetDecayTicks * GameConfig.EchoBloodPactShieldFraction);
                break;

            case "contagion": // Rin: hit ignites; burn tick heals (pact)
                // Echo Wildfire: any element ignites, not only fire.
                if (directHit && monster.Hp > 0 && (element == "fire" || HasEcho("wildfire")))
                    ApplyContagionBurn(monster);
                else if (!directHit && element == "fire")
                    HealPlayer((int)Math.Max(final * _trait.Value * _traitMult, 0));
                break;

            case "static_charge": // Rynna: direct hits mark; full Charge detonates one marked target
                if (!directHit || monster.Hp <= 0) break;
                ApplyStaticMark(monster);
                if (_staticCharge >= GameConfig.RynnaChargeMax && ActiveStaticMark(monster))
                {
                    _staticCharge = HasEcho("perpetual_storm")
                        ? GameConfig.RynnaChargeMax * GameConfig.EchoPerpetualDischargeRetain : 0;
                    RynnaDetonateStaticMark(monster, massDetonation: false);
                    break;
                }

                // Echo Perpetual Storm: Charge fills twice as fast.
                // G-08B: keyword "charge": resistant target fills Charge more slowly.
                AddRynnaCharge(GameConfig.RynnaChargePerHit
                    * (1 + traitChargeBonus)
                    * (HasEcho("perpetual_storm") ? GameConfig.EchoPerpetualChargeMult : 1)
                    * KeywordResistMult(monster, "charge"));
                break;

            case "shatter": // Lunara: autos and ice hits build Frostbite stacks.
                if (directHit && monster.Hp > 0 && (element == "ice" || !fromSkill)) ApplyShatter(monster);
                break;
        }
    }

    /// <summary>Gaia/Rin: on kill, the hunt jumps Prey and the fire spreads.</summary>
    private void OnMonsterKilledTrait(Actor monster)
    {
        if (_trait.Kind == "prey" && (monster.Id == _preyId || monster.Id == _preyId2))
        {
            monster.IsPrey = false;
            // Echo Pack: bigger hunt bonus on execution.
            _preyHuntBonusUntilMs = NowMs + (HasEcho("pack")
                ? GameConfig.EchoPackHuntBonusMs : GameConfig.GaiaHuntBonusMs);
            if (monster.Id == _preyId2) _preyId2 = 0; // the Pack re-marks the 2nd on the next hit
            if (monster.Id == _preyId)
            {
                _preyId = 0;
                var next = NearestLivingMonster(monster.X, monster.Y, m => m.Id != monster.Id && m.Id != _preyId2);
                if (next is not null) SetPrey(next);
            }
        }
        else if (_trait.Kind == "contagion" && IsBurning(monster))
        {
            SpreadBurnFrom(monster);
        }
        else if (_trait.Kind == "decay" && !_resolvingDeathOrb)
        {
            // Velvet Soul Detonation: a corpse that dies under Decay drops a Death Orb. Orb-burst kills
            // are excluded (_resolvingDeathOrb) so the effect is a single generation, never a chain.
            var stacks = ActiveDecayStacks(monster);
            if (stacks > 0) SpawnDeathOrb(monster.X, monster.Y, stacks);
        }
    }

    /// <summary>Velvet Soul Detonation: schedules a delayed death burst at a slain decayed enemy. The
    /// burst re-seeds Decay on survivors so it can cascade through a decayed pack; capped per floor so
    /// the chain stays bounded. Deterministic: timing off NowMs, no Rng.</summary>
    private void SpawnDeathOrb(int x, int y, int decayStacks)
    {
        var orbs = 0;
        foreach (var s in _pendingStrikes)
            if (s.IsDeathOrb && s.Floor == _currentFloor) orbs++;
        if (orbs >= GameConfig.VelvetDeathOrbMaxPerFloor) return;

        var dmg = PlayerAttack() * RoleSkillMult() * EquipmentStats.SkillPowerMultiplier
            * (GameConfig.VelvetDeathOrbDamageMult + GameConfig.VelvetDeathOrbDamagePerStack * decayStacks);
        _pendingStrikes.Add(new ScheduledStrike
        {
            Floor = _currentFloor, X = x, Y = y,
            AtMs = NowMs + GameConfig.VelvetDeathOrbDelayMs,
            Element = "death", Fx = GameConfig.VelvetDeathOrbFx, Damage = dmg,
            Radius = decayStacks >= GameConfig.VelvetDecayMaxStacks
                ? GameConfig.VelvetDeathOrbRadius + 1 : GameConfig.VelvetDeathOrbRadius,
            IsDeathOrb = true,
        });
        Emit("effect", x, y, 0, 0, GameConfig.VelvetDeathOrbFx);
        Emit("text", x, y, 0, 0, 0, "SOUL");
    }

    /// <summary>Reign of Shadows (§4G): force every pending Death Orb on the current floor to burst now
    /// instead of waiting out its delay. Reuses ResolveStrike, so the orb-burst anti-cascade guard
    /// (_resolvingDeathOrb) is set and kills during the burst spawn no new orbs. Iterates back-to-front
    /// over stable insertion order — deterministic, no Rng.</summary>
    private void DetonatePendingDeathOrbs()
    {
        for (var i = _pendingStrikes.Count - 1; i >= 0; i--)
        {
            var s = _pendingStrikes[i];
            if (!s.IsDeathOrb || s.Floor != _currentFloor) continue;
            _pendingStrikes.RemoveAt(i);
            ResolveStrike(s);
        }
    }

    /// <summary>Rin: periodic fire jump independent of deaths, plus the Infernal Ball burn-multiplier decay.</summary>
    private void TickTraitTimers()
    {
        if (_trait.Kind != "contagion") return;
        // Infernal Ball (§4F): the room-wide burn multiplier bleeds off one stack at a time.
        if (_rinBurnMultStacks > 0 && NowMs >= _rinBurnMultNextDecayMs)
        {
            _rinBurnMultStacks--;
            _rinBurnMultNextDecayMs = NowMs + GameConfig.RinInfernalBurnMultDecayMs;
        }
        if (NowMs < _contagionNextJumpMs) return;
        _contagionNextJumpMs = NowMs + GameConfig.RinContagionIntervalMs;
        var src = FirstBurningMonster();
        if (src is null) return;
        SpreadBurnFrom(src);
        // Echo Wildfire: burn does not expire while any target is burning.
        if (HasEcho("wildfire")) RefreshAllBurns();
    }

    /// <summary>Rin Infernal Ball: current burn-damage bonus fraction from active multiplier stacks
    /// (0 when none). Amplifies fire DoT ticks and the Wildfire Reckoning reap.</summary>
    private double RinBurnMult() => _rinBurnMultStacks * GameConfig.RinInfernalBurnMultPerStack;

    /// <summary>Rin Infernal Ball: adds one burn-multiplier stack (capped) and refreshes the decay clock.</summary>
    private void AddBurnMultStack()
    {
        _rinBurnMultStacks = Math.Min(_rinBurnMultStacks + 1, GameConfig.RinInfernalBurnMultMaxStacks);
        _rinBurnMultNextDecayMs = NowMs + GameConfig.RinInfernalBurnMultDecayMs;
    }

    /// <summary>Rin: Wildfire renews active burn duration (does not stack potency).</summary>
    private void RefreshAllBurns()
    {
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor) continue;
            foreach (var d in m.Dots)
                if (d.Element == "fire")
                {
                    d.TicksLeft = GameConfig.RinContagionBurnTicks;
                    d.NextTickAtMs = Math.Min(d.NextTickAtMs, NowMs + GameConfig.RinContagionBurnTickMs);
                }
        }
    }

    // ================= G-04: card mechanics (rare/echo) =================
    // Sibling hooks to Kaeli passives: read _cards (stacks) + tags, no new engine dispatch.
    // Deterministic (only NowMs/_rng from the run, stable counters, id tie-break, stack sums
    // independent of dictionary order). Bursts call DealDamageToMonster with fromTrait:true to
    // avoid retriggering their own hooks (the !fromTrait guard closes trait and card together).

    /// <summary>Summed stacks from cards with a mechanic Kind (0 if none equipped).</summary>
    private int CardKindStacks(string kind)
    {
        var total = 0;
        foreach (var (cardId, stacks) in _cards)
            if (Cards.ById[cardId].Kind == kind) total += stacks;
        return total;
    }

    private int CountEchoSpectres() => _summons.Count(s => s.IsEchoSpectre);

    /// <summary>G-04B: an Echo is equipped (1-stack cap -> Kind presence among cards).</summary>
    private bool HasEcho(string kind) => CardKindStacks(kind) > 0;

    /// <summary>Raises Echo shield (overhealth), limited to a fraction of max health.</summary>
    private void GainEchoShield(double amount)
    {
        if (amount <= 0) return;
        var cap = Player.MaxHp * GameConfig.EchoShieldCapFraction;
        _echoShield = Math.Min(_echoShield + amount, cap);
    }

    /// <summary>Absorbs Echo shield damage before health; returns what remains for health to take.</summary>
    private int AbsorbWithEchoShield(int damage)
    {
        if (_echoShield <= 0 || damage <= 0) return damage;
        var absorbed = Math.Min(_echoShield, damage);
        _echoShield -= absorbed;
        return damage - (int)absorbed;
    }

    /// <summary>Card post-damage: fills ultimate (Echo Overloaded) and extra strike (Double Strike).
    /// Only direct hits count (auto/skill), same as passives.</summary>
    private void ApplyCardPostDamage(Actor monster, bool directHit)
    {
        if (!directHit) return;

        var surge = CardKindStacks("echo_surge");
        if (surge > 0)
            _gauge = Math.Min(
                _gauge + GameConfig.CardEchoSurgeGaugePerHit * surge * _gaugeRate,
                GameConfig.UltimateGaugeMax);

        var doubleStrike = CardKindStacks("double_strike");
        if (doubleStrike > 0 && monster.Hp > 0)
        {
            _cardDoubleStrikeHits++;
            if (_cardDoubleStrikeHits >= GameConfig.CardDoubleStrikeEvery)
            {
                _cardDoubleStrikeHits = 0;
                Emit("text", monster.X, monster.Y, 0, 0, 0, "DOUBLE STRIKE");
                DealDamageToMonster(monster,
                    PlayerAttack() * RoleSkillMult() * GameConfig.CardDoubleStrikeDamageMult * doubleStrike,
                    CurrentStance.Element, 0,
                    fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
            }
        }
    }

    /// <summary>Card hooks on kill (Velvet Harvest/Plague, Rin Holocaust). Each Echo is
    /// checked independently; reads target state (Decay/burn) before cleanup.</summary>
    private void OnMonsterKilledCard(Actor monster)
    {
        // Velvet: Harvest raises a specter that pulses damage when killing under Decay (max N).
        if (HasEcho("harvest") && ActiveDecayStacks(monster) > 0
            && CountEchoSpectres() < GameConfig.CardHarvestMaxSpectres)
        {
            var pulseMs = GameConfig.CardHarvestSpectrePulseMs;
            _summons.Add(new PlayerSummon
            {
                Floor = monster.Floor, X = monster.X, Y = monster.Y,
                Element = "death", Fx = GameConfig.CardHarvestSpectreFx,
                Radius = GameConfig.CardHarvestSpectreRadius,
                DamagePerPulse = PlayerAttack() * RoleSkillMult() * GameConfig.CardHarvestSpectreDamageMult,
                PulseMs = pulseMs, NextPulseAtMs = NowMs + pulseMs,
                ExpireAtMs = NowMs + GameConfig.CardHarvestSpectreDurationMs,
                IsEchoSpectre = true,
            });
            Emit("text", monster.X, monster.Y, 0, 0, 0, "HARVEST");
            Emit("effect", monster.X, monster.Y, 0, 0, GameConfig.CardHarvestSpectreFx);
        }

        // Velvet: Viral Plague makes Decay jump with its stacks to the nearest living target on death.
        if (HasEcho("viral_plague"))
        {
            var stacks = ActiveDecayStacks(monster);
            var next = stacks > 0 ? NearestLivingMonster(monster.X, monster.Y, m => m.Id != monster.Id) : null;
            if (next is not null)
            {
                next.DecayStacks = Math.Min(stacks, GameConfig.VelvetDecayMaxStacks);
                next.DecayUntilMs = NowMs + GameConfig.VelvetDecayDurationMs;
                ApplyDotToMonster(next, "death", GameConfig.ConditionTickFx["curse"],
                    PlayerAttack() * RoleSkillMult() * GameConfig.VelvetDecayDamagePerStack * next.DecayStacks,
                    GameConfig.VelvetDecayTicks, GameConfig.VelvetDecayTickMs);
                Emit("projectile", monster.X, monster.Y, next.X, next.Y, 11); // death missile
                Emit("text", next.X, next.Y, 0, 0, 0, "PLAGUE");
            }
        }

        // Rin: Holocaust makes a burning target explode in a fire burst on death.
        if (HasEcho("holocaust") && IsBurning(monster))
        {
            var burst = PlayerAttack() * RoleSkillMult() * GameConfig.EchoHolocaustDamageMult;
            Emit("text", monster.X, monster.Y, 0, 0, 0, "HOLOCAUST");
            foreach (var (tx, ty) in CircleTiles(monster.X, monster.Y, GameConfig.EchoHolocaustRadius))
            {
                Emit("effect", tx, ty, 0, 0, GameConfig.ConditionTickFx["fire"]);
                var victim = MonsterAt(tx, ty);
                if (victim is not null && victim.Id != monster.Id)
                    DealDamageToMonster(victim, burst, "fire", 0,
                        fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
            }
        }
    }

    /// <summary>Detonation: a condition that expires naturally (not by death) explodes in an area.</summary>
    private void OnConditionExpiredCard(Actor monster, MonsterDot dot)
    {
        if (monster.Hp <= 0) return;
        var stacks = CardKindStacks("detonate");
        if (stacks <= 0) return;
        var burst = PlayerAttack() * RoleSkillMult() * GameConfig.CardDetonateDamageMult * stacks;
        Emit("text", monster.X, monster.Y, 0, 0, 0, "DETONATION");
        foreach (var (tx, ty) in CircleTiles(monster.X, monster.Y, GameConfig.CardDetonateRadius))
        {
            Emit("effect", tx, ty, 0, 0, dot.Fx);
            var victim = MonsterAt(tx, ty);
            if (victim is not null)
                DealDamageToMonster(victim, burst, dot.Element, 0,
                    fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
        }
    }

    // Eloa: detonates the Seal in a small area and heals the Seraph by a fraction of the burst.
    private void EloaDetonate(Actor center, int triggerDamage)
    {
        var burst = triggerDamage * _trait.Value * _traitMult;
        // Echo Sentence: each Judgment amplifies the next burst (stacks up to a cap).
        if (HasEcho("sentence"))
        {
            burst *= 1 + _eloaSentenceStacks * GameConfig.EchoSentenceBurstPerStack;
            _eloaSentenceStacks = Math.Min(_eloaSentenceStacks + 1, GameConfig.EchoSentenceMaxStacks);
        }
        // The sentence spreads: a base seed always marks nearby enemies; chain_judgment adds more.
        var chainSeed = GameConfig.EloaBaseChainSinSeed
            + (HasEcho("chain_judgment") ? GameConfig.EchoEloaChainSinSeed : 0);
        Emit("text", center.X, center.Y, 0, 0, 0, "JUDGED");
        foreach (var (tx, ty) in CircleTiles(center.X, center.Y, GameConfig.EloaJudgmentRadius))
        {
            Emit("effect", tx, ty, 0, 0, 40); // holy area fx
            var victim = MonsterAt(tx, ty);
            if (victim is null) continue;
            DealDamageToMonster(victim, burst, "holy", 0,
                fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
            if (chainSeed > 0 && victim.Id != center.Id && victim.Hp > 0)
            {
                victim.SinStacks = Math.Min(
                    ActiveSinStacks(victim) + chainSeed, GameConfig.EloaSinStacksToJudge);
                victim.SinUntilMs = NowMs + GameConfig.EloaSinDurationMs;
            }
        }
        // Echo Martyr: Judgment healing becomes shield above health instead of healing.
        var grace = Math.Max(burst * _trait.Param, 0);
        if (HasEcho("martyr")) GainEchoShield(grace);
        else HealPlayer((int)grace);
    }

    private void AddRynnaCharge(double amount)
    {
        if (_trait.Kind != "static_charge" || amount <= 0) return;
        _staticCharge = Math.Min(_staticCharge + amount, GameConfig.RynnaChargeMax);
    }

    private bool ActiveStaticMark(Actor monster)
    {
        if (NowMs >= monster.StaticMarkUntilMs)
        {
            monster.HasStaticMark = false;
            return false;
        }
        return monster.HasStaticMark;
    }

    private void ApplyStaticMark(Actor monster)
    {
        monster.HasStaticMark = true;
        monster.StaticMarkUntilMs = NowMs + GameConfig.RynnaStaticMarkDurationMs;
    }

    private void RynnaDetonateStaticMark(Actor target, bool massDetonation)
    {
        target.HasStaticMark = false;
        target.StaticMarkUntilMs = 0;
        Emit("text", target.X, target.Y, 0, 0, 0, massDetonation ? "STORM" : "DISCHARGE");
        target.StunUntilMs = Math.Max(target.StunUntilMs, NowMs + GameConfig.RynnaStaticDetonateStunMs);
        Emit("effect", target.X, target.Y, 0, 0, 32);
        _gauge = Math.Min(_gauge + GameConfig.RynnaParalyzeGaugeBonus
            * (HasEcho("thunder_core") ? GameConfig.EchoThunderCoreGaugeMult : 1), GameConfig.UltimateGaugeMax);
        if (HasEcho("overload"))
            ApplyDotToMonster(target, "energy", GameConfig.ConditionTickFx["energy"],
                PlayerAttack() * RoleSkillMult() * GameConfig.EchoOverloadDotPower,
                GameConfig.EchoOverloadDotTicks, GameConfig.EchoOverloadDotTickMs);
        var dmg = PlayerAttack() * RoleSkillMult() * EquipmentStats.SkillPowerMultiplier
            * GameConfig.RynnaStaticDetonateDamageMult;
        DealDamageToMonster(target, dmg, "energy", 12,
            fromSkill: true, canCrit: false, canLifeSteal: true, fromTrait: true,
            skillLifesteal: GameConfig.RynnaStaticDetonateLifesteal);
    }

    private void PullMonsterTowardPlayer(Actor monster, int tiles)
    {
        if (monster.Hp <= 0 || monster.Floor != Player.Floor || tiles <= 0) return;
        for (var i = 0; i < tiles && Chebyshev(monster, Player) > 1; i++)
        {
            var dx = Math.Sign(Player.X - monster.X);
            var dy = Math.Sign(Player.Y - monster.Y);
            var moved = false;
            foreach (var (sx, sy) in StepAlternatives(dx, dy))
            {
                if (!CanStep(monster, sx, sy)) continue;
                var fromX = monster.X;
                var fromY = monster.Y;
                monster.X += sx;
                monster.Y += sy;
                monster.FromX = fromX;
                monster.FromY = fromY;
                monster.StepStartMs = NowMs;
                monster.StepDurMs = Math.Min(StepDuration(MonsterSpeed(monster), sx != 0 && sy != 0), GameConfig.TickMs);
                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y, monster.Facing);
                Emit("effect", monster.X, monster.Y, 0, 0, 12);
                AcquirePlayer(monster);
                moved = true;
                break;
            }
            if (!moved) break;
        }
    }

    private int ActiveFrostStacks(Actor monster) => NowMs < monster.FrostUntilMs ? monster.FrostHits : 0;

    private void AddFrostStacks(Actor monster, int stacks)
    {
        var gain = KeywordScaledStacks(monster, "frost", stacks);
        if (gain <= 0) return;
        monster.FrostHits = Math.Min(ActiveFrostStacks(monster) + gain, GameConfig.LunaraShatterHits * 3);
        monster.FrostUntilMs = NowMs + GameConfig.LunaraFrostDurationMs;
    }

    private void ClearFrost(Actor monster)
    {
        monster.FrostHits = 0;
        monster.FrostUntilMs = 0;
    }

    private double FrostShatterDamage(Actor monster, int stacks, double perStack) =>
        PlayerAttack() * RoleSkillMult() * EquipmentStats.SkillPowerMultiplier
        * perStack * Math.Max(stacks, 1) * KeywordResistMult(monster, "frost") * _traitMult;

    // Lunara: autos and ice hits build frost. Hitting an already-frosted target grants haste; the
    // threshold shatters it and cascades through nearby frosted enemies.
    private void ApplyShatter(Actor monster)
    {
        var hadFrost = ActiveFrostStacks(monster) > 0;
        if (hadFrost)
        {
            _traitHasteUntilMs = NowMs + (HasEcho("moon_dance") ? GameConfig.LunaraHasteMs * 15 : GameConfig.LunaraHasteMs);
            _traitHasteFactor = GameConfig.LunaraHasteFactor;
        }

        AddFrostStacks(monster, 1);
        TryShatterFrost(monster);
    }

    private bool TryShatterFrost(Actor monster)
    {
        var shatterHits = HasEcho("moon_dance") ? GameConfig.EchoMoonDanceShatterHits : GameConfig.LunaraShatterHits;
        var stacks = ActiveFrostStacks(monster);
        if (stacks >= shatterHits)
        {
            ShatterFrost(monster, stacks, GameConfig.LunaraShatterDamagePerStack, cascade: true);
            return true;
        }
        return false;
    }

    private void ShatterFrost(Actor monster, int stacks, double perStack, bool cascade)
    {
        ClearFrost(monster);
        Emit("text", monster.X, monster.Y, 0, 0, 0, "SHATTER");
        Emit("effect", monster.X, monster.Y, 0, 0, 44);
        DealDamageToMonster(monster, FrostShatterDamage(monster, stacks, perStack), "ice", 0,
            fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
        if (cascade) CascadeShatterFrom(monster);
    }

    private void MassShatterFrost(Actor monster)
    {
        var stacks = ActiveFrostStacks(monster);
        if (stacks <= 0) return;
        ShatterFrost(monster, stacks, GameConfig.LunaraAbsoluteZeroDamagePerStack, cascade: false);
    }

    /// <summary>Lunara Frostbite cascade (§4C): after a shatter, nearby frosted enemies may shatter too.
    /// The chain is bounded by jumps/range/falloff and picks the next target deterministically.</summary>
    private void CascadeShatterFrom(Actor source)
    {
        var range = HasEcho("chain_shatter")
            ? Math.Max(GameConfig.LunaraShatterCascadeRange, GameConfig.EchoChainShatterRange)
            : GameConfig.LunaraShatterCascadeRange;
        var scale = HasEcho("chain_shatter") ? GameConfig.EchoChainShatterDamageMult : 1.0;
        var hit = new HashSet<int> { source.Id };
        var from = source;
        var perStack = GameConfig.LunaraShatterDamagePerStack
            * GameConfig.LunaraShatterCascadeFalloff
            * scale;

        for (var jump = 0; jump < GameConfig.LunaraShatterCascadeJumps; jump++)
        {
            var next = NearestFrostedMonster(from.X, from.Y, range, hit);
            if (next is null) break;
            hit.Add(next.Id);
            var stacks = ActiveFrostStacks(next);
            Emit("projectile", from.X, from.Y, next.X, next.Y, 29);
            ShatterFrost(next, stacks, perStack, cascade: false);
            from = next;
            perStack *= GameConfig.LunaraShatterCascadeFalloff;
        }
    }

    private Actor? NearestFrostedMonster(int x, int y, int range, HashSet<int> excluded)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || excluded.Contains(m.Id)) continue;
            if (ActiveFrostStacks(m) <= 0) continue;
            var d = Chebyshev(x, y, m.X, m.Y);
            if (d > range) continue;
            if (d < bestDist || (d == bestDist && (best is null || m.Id < best.Id)))
            {
                bestDist = d;
                best = m;
            }
        }
        return best;
    }

    private void ApplyContagionBurn(Actor monster)
    {
        // G-08B: keyword "burn": resistant target burns less; immune (100) does not ignite.
        var mult = KeywordResistMult(monster, "burn");
        if (mult <= 0) return;
        ApplyDotToMonster(monster, "fire", GameConfig.ConditionTickFx["fire"],
            PlayerAttack() * RoleSkillMult() * GameConfig.RinContagionBurnPower * mult,
            GameConfig.RinContagionBurnTicks, GameConfig.RinContagionBurnTickMs);
    }

    private void SpreadBurnFrom(Actor source)
    {
        var dst = NearestLivingMonster(source.X, source.Y,
            m => m.Id != source.Id && !IsBurning(m)
                 && Chebyshev(source.X, source.Y, m.X, m.Y) <= (int)_trait.Param);
        if (dst is null) return;
        ApplyContagionBurn(dst);
        Emit("projectile", source.X, source.Y, dst.X, dst.Y, 4); // fire arc
    }

    private void SetPrey(Actor m)
    {
        foreach (var mon in _monsters)
            if (mon.IsPrey && mon.Id != m.Id && mon.Id != _preyId2) mon.IsPrey = false; // Pack preserves the 2nd
        _preyId = m.Id;
        _preyStartMs = NowMs;
        m.IsPrey = true;
        Emit("text", m.X, m.Y, 0, 0, 0, "PREY");
    }

    /// <summary>Gaia: Pack marks a second simultaneous Prey (shares the hunt ramp).</summary>
    private void SetSecondPrey(Actor primary)
    {
        var second = NearestLivingMonster(primary.X, primary.Y, m => m.Id != _preyId && m.Id != primary.Id);
        if (second is null) return;
        _preyId2 = second.Id;
        second.IsPrey = true;
        Emit("text", second.X, second.Y, 0, 0, 0, "PREY");
    }

    private int ActiveSinStacks(Actor m)
    {
        if (NowMs >= m.SinUntilMs) m.SinStacks = 0;
        return m.SinStacks;
    }

    private int ActiveDecayStacks(Actor m)
    {
        if (NowMs >= m.DecayUntilMs) m.DecayStacks = 0;
        return m.DecayStacks;
    }

    private bool IsMonsterAlive(int id) => _monsters.Any(m => m.Id == id && m.Hp > 0);

    private static bool IsBurning(Actor m) => m.Dots.Any(d => d.Element == "fire");

    private Actor? FirstBurningMonster()
    {
        Actor? best = null;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || !IsBurning(m)) continue;
            if (best is null || m.Id < best.Id) best = m;
        }
        return best;
    }

    private Actor? NearestLivingMonster(int x, int y, Func<Actor, bool> predicate)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || !predicate(m)) continue;
            var d = Chebyshev(x, y, m.X, m.Y);
            if (d < bestDist || (d == bestDist && (best is null || m.Id < best.Id)))
            {
                bestDist = d;
                best = m;
            }
        }
        return best;
    }

    // ---- F-E: boss posture (echo break) ----

    private void AddPosture(Actor boss, string element, bool fromSkill)
    {
        var gain = fromSkill ? GameConfig.PostureGainPerSkill : GameConfig.PostureGainPerAuto;
        if (boss.Species!.Elements.GetValueOrDefault(element, 0) < 0)
            gain *= GameConfig.PostureWeaknessMult; // hitting a weakness breaks faster
        // G-08B: keyword "posture": resistant target is harder to break (negative = breaks faster).
        gain *= KeywordResistMult(boss, "posture");
        boss.Posture += gain;
        boss.PostureLastHitMs = NowMs;
        if (boss.Posture >= boss.PostureMax) TriggerEchoBreak(boss);
    }

    private void TriggerEchoBreak(Actor boss)
    {
        var idx = Math.Min(boss.PostureCycle, GameConfig.PostureDamageMultipliers.Length - 1);
        boss.StaggerMultiplier = GameConfig.PostureDamageMultipliers[idx];
        boss.StaggerUntilMs = NowMs + GameConfig.PostureStaggerMs;
        boss.StunUntilMs = Math.Max(boss.StunUntilMs, boss.StaggerUntilMs);
        boss.PostureBonusReadyAtMs = 0; // first stagger hit may grant the maxHP bonus immediately
        boss.PostureCycle++;
        boss.PostureMax = boss.PostureBaseMax * (1 + boss.PostureCycle * GameConfig.PostureMaxGrowthPerCycle);
        boss.Posture = 0;
        Emit("effect", boss.X, boss.Y, 0, 0, 35); // big explosion
        Emit("text", boss.X, boss.Y, 0, 0, 0, "BROKEN!");
    }

    /// <summary>Posture bleeds back down once the boss stops taking pressure (no free fill).</summary>
    private void TickPostureDecay()
    {
        foreach (var m in _monsters)
        {
            if (m.PostureMax <= 0 || m.Hp <= 0 || m.Posture <= 0) continue;
            if (m.IsStaggered(NowMs) || NowMs - m.PostureLastHitMs < GameConfig.PostureDecayDelayMs) continue;
            var decay = m.PostureMax * GameConfig.PostureDecayFractionPerSec * GameConfig.TickMs / 1000.0;
            m.Posture = Math.Max(0, m.Posture - decay);
        }
    }

    // ---- F-E: elemental reactions ----

    private void ApplyElementMarkAndReactions(Actor monster, string element, int hitDamage)
    {
        if (!ElementReactions.IsReactive(element)) return;

        var mark = monster.ActiveMark(NowMs);
        if (mark.Length > 0 && mark != element
            && ElementReactions.Lookup(mark, element) is { } reaction)
        {
            monster.ElementMark = "";
            monster.ElementMarkUntilMs = 0;
            TriggerReaction(monster, reaction, hitDamage);
            return;
        }

        monster.ElementMark = element;
        monster.ElementMarkUntilMs = NowMs + GameConfig.ElementMarkDurationMs;
    }

    private void TriggerReaction(Actor monster, ReactionDef reaction, int hitDamage)
    {
        var damage = Math.Max((int)(hitDamage * reaction.DamageFraction), 1);
        Emit("effect", monster.X, monster.Y, 0, 0, reaction.Fx);
        Emit("text", monster.X, monster.Y, 0, 0, 0, reaction.Name);

        if (reaction.Radius > 0)
        {
            foreach (var (tx, ty) in CircleTiles(monster.X, monster.Y, reaction.Radius))
            {
                var victim = MonsterAt(tx, ty);
                if (victim is null) continue;
                if (tx != monster.X || ty != monster.Y) Emit("effect", tx, ty, 0, 0, reaction.Fx);
                DealReactionDamage(victim, damage, killIfDead: victim.Id != monster.Id);
            }
        }
        else
        {
            DealReactionDamage(monster, damage, killIfDead: false);
        }

        if (monster.Hp <= 0) return;
        if (reaction.StunMs > 0)
        {
            monster.StunUntilMs = Math.Max(monster.StunUntilMs, NowMs + reaction.StunMs);
            Emit("effect", monster.X, monster.Y, 0, 0, 32); // stun stars
        }
        if (reaction.SlowMs > 0)
        {
            monster.SlowUntilMs = NowMs + reaction.SlowMs;
            monster.SlowFactor = reaction.SlowFactor;
        }
    }

    /// <summary>
    /// Flat reaction damage: never re-marks or re-triggers (no recursion). The primary target
    /// is left to <see cref="DealDamageToMonster"/>'s trailing kill check; splash victims kill here.
    /// </summary>
    private void DealReactionDamage(Actor victim, int amount, bool killIfDead)
    {
        if (victim.Hp <= 0) return;
        victim.Hp -= amount;
        Emit("damage", victim.X, victim.Y, 0, 0, amount, "", victim.Id, true);
        if (victim.TargetId == 0) AcquirePlayer(victim);
        if (killIfDead && victim.Hp <= 0) KillMonster(victim);
    }

    private int BestiaryRank(string species)
    {
        var kills = _bestiaryKills.GetValueOrDefault(species, 0);
        var rank = 0;
        foreach (var threshold in GameConfig.BestiaryRankKills)
            if (kills >= threshold) rank++;
        return rank;
    }

    private void KillMonster(Actor monster, bool overkill = false)
    {
        // guard against double counting: trait bursts (judgment, shatter, discharge) can
        // kill the same target already marked for death by the hit that triggered them.
        if (monster.Killed) return;
        // Training dummy: it is built to survive, but if a burst ever drops it, emit the death FX and let the
        // mode respawn a fresh one. No xp/loot/gauge/chest/portal — the Training Room is a reward-free sandbox.
        if (monster.IsTrainingDummy)
        {
            monster.Killed = true;
            monster.Hp = 0;
            Emit("death", monster.X, monster.Y, 0, 0, monster.Species!.Corpse, monster.Species.Name, monster.Id);
            _modeRules.OnMonsterKilled(this, monster);
            return;
        }
        monster.Killed = true;
        monster.Hp = 0;
        _kills++;
        var speciesId = monster.Species!.StableId;
        KillsBySpecies[speciesId] = KillsBySpecies.GetValueOrDefault(speciesId) + 1;

        // K-04: Gaia hunt jumps Prey; Rin fire spreads when a burning target dies
        OnMonsterKilledTrait(monster);
        // G-04: Velvet Harvest: specter on killing under Decay (reads state before cleanup).
        OnMonsterKilledCard(monster);
        // KR-00: reset-on-kill auto-modifiers (Seren's cleave) refund a charge on any kill in the window.
        OnMonsterKilledAutoMod();

        Emit("death", monster.X, monster.Y, 0, 0, monster.Species.Corpse, monster.Species.Name, monster.Id);

        // G-08B: bomber/suicider bursts in an area when dying near the player.
        if (GameConfig.BehaviorProfile(monster.Species.BehaviorId) is { ExplodeRadius: > 0 } bomber)
            BomberExplode(monster, bomber);

        // xp + gauge
        var xpScale = monster.Species.IsAuthored ? 1 : Tier.StatMultiplier;
        var xp = (long)(monster.Species.Experience * xpScale * (1 + CardValue("xpPercent")));
        GainXp(Math.Max(xp, 1));
        _gauge = Math.Min(_gauge + GameConfig.GaugeFillPerKill * (1 + CardValue("gaugePercent")) * _gaugeRate, GameConfig.UltimateGaugeMax);

        if (!monster.IsSummon) DropLoot(monster); // summons give xp but no loot (anti-farm)

        // G-09: the mimic (corrupted Echo chest) guarantees gear material on death.
        if (monster.IsMimic)
            for (var i = 0; i < GameConfig.CursedChestMaterialDrops; i++) GrantGearMaterial(monster.X, monster.Y);

        // G-06: defeating a common-room elite is a beat: grants a heavy card choice.
        if (monster.IsElite && !monster.IsSummon) OfferCardBeat();

        // Horde-floor dynamic loot (2026-06-29 feedback, 8th pass): the chest DROPS on the corpse every N
        // deaths (the Kaeli detours to claim it while luring), and the exit appears only as a TELEPORT on the
        // last mob corpse when clearing the room. Summons do not count for the counter (anti-farm). Boss floor has none
        // of this (exit = defeat the boss). monster.Hp is already 0 here, so CountAliveOnFloor excludes this one.
        if (!monster.IsSummon && !IsBossFloor())
        {
            _killsSinceChest++;
            var aliveLeft = CountAliveOnFloor();
            if (_killsSinceChest >= GameConfig.ChestDropEveryKills && aliveLeft > 0)
            {
                _killsSinceChest = 0;
                var (dcx, dcy) = OpenTileNear(_currentFloor, monster.X, monster.Y);
                // dropped chest is never a mimic (always a benefit); it may be cursed (ambush + blessed offer).
                var variant = _rng.Chance(GameConfig.ChestCursedChance) ? "cursed" : "";
                AddRuntimePoi("chest", variant, dcx, dcy);
                Emit("effect", dcx, dcy, 0, 0, 29); // fireworks: chest appeared
            }
            // room cleared -> open the exit portal on the last mob corpse.
            if (aliveLeft == 0 && !LadderExistsOnFloor())
            {
                var (lx, ly) = OpenTileNear(_currentFloor, monster.X, monster.Y);
                AddRuntimePoi("ladder", "", lx, ly);
                Emit("effect", lx, ly, 0, 0, 11); // teleport burst
                Emit("text", lx, ly, 0, 0, 0, "PORTAL OPEN");
            }
        }

        // LM-03 (3) end condition: the mode decides what a death means (Dungeon: boss = victory).
        _modeRules.OnMonsterKilled(this, monster);
    }

    /// <summary>G-08B: bomber burst on death: paints the area and hurts the player if in radius.
    /// Deterministic: damage derived from kit (largest MaxDamage) * scale, no _rng.</summary>
    private void BomberExplode(Actor bomber, MonsterBehaviorProfile profile)
    {
        Emit("effect", bomber.X, bomber.Y, 0, 0, GameConfig.BomberExplodeFx);
        foreach (var (tx, ty) in CircleTiles(bomber.X, bomber.Y, profile.ExplodeRadius))
            if (tx != bomber.X || ty != bomber.Y)
                Emit("effect", tx, ty, 0, 0, GameConfig.BomberExplodeFx);

        if (Player.Hp <= 0) return;
        if (Chebyshev(bomber.X, bomber.Y, Player.X, Player.Y) > profile.ExplodeRadius) return;

        var baseDamage = bomber.Species!.Attacks.Count > 0
            ? bomber.Species.Attacks.Max(a => a.MaxDamage)
            : Math.Max(bomber.MaxHp / 10, 1);
        var element = bomber.Species.Attacks.FirstOrDefault()?.DamageType ?? "physical";
        var damage = Math.Max((int)(baseDamage * profile.ExplodeDamageScale * bomber.StatMult), 1);
        DamagePlayer(damage, element, bomber);
    }

    private void DropLoot(Actor monster)
    {
        if (monster.Species!.IsAuthored)
        {
            DropKaezanLoot(monster);
            return;
        }

        var junkGold = 0L;
        foreach (var entry in monster.Species!.Loot)
        {
            if (!_rng.Chance(entry.Chance / 100000.0)) continue;
            var count = entry.MaxCount > 1 ? _rng.Range(1, entry.MaxCount) : 1;
            if (entry.Name.Contains("gold coin", StringComparison.OrdinalIgnoreCase))
            {
                var gold = (long)(count * (1 + CardValue("goldPercent")) * Tier.StatMultiplier);
                _gold += gold;
                EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", monster.X, monster.Y, isGold: true);
                continue;
            }
            // food/potion/equipment is collected immediately (flies to the player); the rest (junk) becomes gold
            if (_data.IsFood(entry.ItemId) || _data.PotionHealFraction(entry.ItemId) > 0
                || _data.IsEquippableLoot(entry.ItemId))
            {
                CollectLoot(entry.ItemId, entry.Name, count, monster.X, monster.Y);
                continue;
            }
            junkGold += (long)(_items?.Value(entry.ItemId) ?? _data.ItemValue(entry.ItemId)) * count;
        }

        if (junkGold > 0)
        {
            var gold = (long)(junkGold * (1 + CardValue("goldPercent")));
            _gold += gold;
            EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", monster.X, monster.Y, isGold: true);
        }

        if (monster.IsBossActor
            && GameConfig.TierMountLookTypes.TryGetValue(Tier.Tier, out var mountLookType)
            && _rng.Chance(GameConfig.BossMountDropChance))
        {
            var itemId = GameConfig.MountItemId(mountLookType);
            if (_data.Items.TryGetValue(itemId, out var mount))
                CollectLoot(mount.ItemId, mount.Name, 1, monster.X, monster.Y);
        }
    }

    private void DropKaezanLoot(Actor monster)
    {
        var rank = monster.IsBossActor ? "boss" : monster.Species!.Rank;
        var (minGold, maxGold) = GameConfig.KaezanDropGoldRange(Tier.Tier, rank);
        var gold = (long)(_rng.Range(minGold, maxGold) * (1 + CardValue("goldPercent")));
        _gold += gold;
        EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", monster.X, monster.Y, isGold: true);

        var itemChance = monster.IsBossActor
            ? 1.0
            : rank == "elite"
                ? GameConfig.KaezanEliteItemDropChance
                : GameConfig.KaezanCommonItemDropChance;
        if (!_rng.Chance(itemChance)) return;

        if (rank == "boss"
            && _rng.Chance(GameConfig.KaezanBossRelicDropChance)
            && TryPickKaezanDropItem(rank, relicOnly: true, out var relic))
        {
            CollectLoot(relic.ItemId, relic.Name, 1, monster.X, monster.Y);
            return;
        }

        if (TryPickKaezanDropItem(rank, relicOnly: false, out var item))
            CollectLoot(item.ItemId, item.Name, 1, monster.X, monster.Y);
    }

    private bool TryPickKaezanDropItem(string rank, bool relicOnly, out ItemType item)
    {
        item = default!;
        if (_items is null) return false;

        var pool = _items.All.Values
            .Where(candidate =>
                candidate.IsAuthored
                && candidate.Tier == Tier.Tier
                && candidate.ItemId >= GameConfig.AuthoredItemIdBase
                && candidate.Slot is not null
                && (relicOnly
                    ? candidate.Tag == GameConfig.AuthoredItemTagRelic
                    : candidate.Tag != GameConfig.AuthoredItemTagRelic))
            .OrderBy(candidate => candidate.ItemId)
            .ToList();
        if (pool.Count == 0) return false;

        var classItems = pool
            .Where(candidate => candidate.AllowedClassIds?.Contains(Waifu.ClassId, StringComparer.OrdinalIgnoreCase) == true)
            .ToList();
        var genericItems = pool
            .Where(candidate => candidate.AllowedClassIds is null || candidate.AllowedClassIds.Count == 0)
            .ToList();

        var classWeight = rank switch
        {
            "boss" => GameConfig.KaezanBossClassDropWeight,
            "elite" => GameConfig.KaezanEliteClassDropWeight,
            "chest" => GameConfig.KaezanChestClassDropWeight,
            _ => GameConfig.KaezanCommonClassDropWeight
        };
        var preferred = _rng.Chance(classWeight) ? classItems : genericItems;
        var fallback = ReferenceEquals(preferred, classItems) ? genericItems : classItems;
        var source = preferred.Count > 0 ? preferred : fallback.Count > 0 ? fallback : pool;
        item = _rng.Pick(source);
        return true;
    }

    /// <summary>
    /// Credits a loot item immediately (without dropping on the ground): consumables heal immediately,
    /// the rest goes to the run backpack. Always emits the item flight effect to the player.
    /// </summary>
    private void CollectLoot(int itemId, string name, int count, int fromX, int fromY)
    {
        var potionFraction = _data.PotionHealFraction(itemId);
        if (potionFraction > 0)
            HealPlayer((int)Math.Ceiling(Player.MaxHp * potionFraction) * count);
        else if (_data.IsFood(itemId))
            HealPlayer((int)Math.Ceiling(Player.MaxHp * GameConfig.FoodHealPct) * count);
        else
            AddLootedItem(itemId, name, count);

        EmitLootFly(itemId, name, fromX, fromY, isGold: false);
    }

    /// <summary>Visual event: the item/coin flies in an arc from origin to player. crit=true marks gold (gold color).</summary>
    private void EmitLootFly(int spriteItemId, string label, int fromX, int fromY, bool isGold) =>
        Emit("loot", fromX, fromY, 0, 0, spriteItemId, label, 0, isGold);

    private void TryUsePotion()
    {
        if (Player.Hp <= 0 || _potionCharges <= 0 || NowMs < _potionReadyAtMs) return;
        if (Player.Hp >= Player.MaxHp) return; // do not waste a charge at full health
        var heal = (int)Math.Ceiling(Player.MaxHp * GameConfig.PotionSlotHealFraction(Tier.Tier));
        HealPlayer(heal);
        _potionCharges--;
        _potionReadyAtMs = NowMs + GameConfig.PotionCooldownMs;
        Emit("effect", Player.X, Player.Y, 0, 0, 12); // sparkles
        Emit("heal", Player.X, Player.Y, 0, 0, heal);
    }

    /// <summary>Dash/Dodge (Space, or triggered by helper). Repositions the player in the requested direction
    /// (otherwise current movement direction, otherwise facing direction), with short i-frames. The exact
    /// movement is role-keyed (see PerformDash): Mage slide-and-trail, Archer pass-through sprint, Knight blink.
    /// Cardinal-only. Shared cooldown (input and helper use the SAME ability).</summary>
    private void TryDash(int dirX, int dirY)
    {
        if (Player.Hp <= 0 || Player.IsStunned(NowMs) || NowMs < _dashReadyAtMs) return;
        int dx = Math.Sign(dirX), dy = Math.Sign(dirY);
        if (dx == 0 && dy == 0) { dx = Math.Sign(_moveDirX); dy = Math.Sign(_moveDirY); }
        if (dx == 0 && dy == 0) (dx, dy) = FacingDelta(Player.Facing);
        // CARDINAL-ONLY: there is no diagonal dash. If the direction is diagonal, align it by the facing axis.
        if (dx != 0 && dy != 0) (dx, dy) = FacingDelta(Player.Facing);
        if (dx == 0 && dy == 0) return;
        PerformDash(dx, dy);
    }

    /// <summary>Executes the dash. The MOVEMENT and payoff differ by role ("Dash Signature"):
    /// Mage slides and seeds a scorch trail; Archer slides through mobs and gains haste; Knight blinks and cleaves.
    /// Blocked with no valid landing: does not fire or spend cooldown. Shared cooldown/i-frames for all roles.</summary>
    private void PerformDash(int dx, int dy)
    {
        switch (_dashSignature)
        {
            case DashSignature.Cleave: PerformKnightBlink(dx, dy); break;
            case DashSignature.Sprint: PerformArcherSprint(dx, dy); break;
            default: PerformMageDash(dx, dy); break;
        }
    }

    /// <summary>Mage: slides up to DashTiles, STOPPING before the first wall/mob, and seeds a weak spreading
    /// scorch trail (Contagion) on the tiles crossed. The slide reads as motion, not a teleport.</summary>
    private void PerformMageDash(int dx, int dy)
    {
        var len = DashClearLen(dx, dy);
        if (len == 0) return; // blocked immediately: do not spend cooldown
        int ox = Player.X, oy = Player.Y;
        // Slide poof + scorch field both use the Kaeli's own element (fire/death/holy/…), not always fire.
        SlideDash(ox, oy, dx, dy, len, GameConfig.ElementFieldFx(Waifu.Element));
        DashScorchTrail(ox, oy, dx, dy, len);
        SpendDash();
    }

    /// <summary>Archer: slides up to DashTiles, PASSING THROUGH mobs (stops only at walls) and landing on the
    /// farthest free tile, then gains a brief move-speed haste (kite identity). No damage.</summary>
    private void PerformArcherSprint(int dx, int dy)
    {
        var len = PassThroughLanding(dx, dy, GameConfig.DashTiles);
        if (len == 0) return; // no free tile to land on: do not spend cooldown
        int ox = Player.X, oy = Player.Y;
        // Cyan haste streak (no element, no damage) — reads as pure speed, distinct from the mage trail.
        SlideDash(ox, oy, dx, dy, len, GameConfig.DashArcherTrailFx);
        _dashHasteUntilMs = NowMs + GameConfig.DashArcherHasteMs;
        _dashHasteFactor = GameConfig.DashArcherHasteFactor;
        SpendDash();
    }

    /// <summary>Knight: a short BLINK (instant, not a slide) of DashKnightBlinkTiles. It may pass OVER a mob in
    /// front, but the landing tile must be FREE; falls back to a shorter free tile. On landing it cleaves an
    /// Exori-style nova around itself. Damage only on landing — never on the terrain crossed.</summary>
    private void PerformKnightBlink(int dx, int dy)
    {
        var len = BlinkLanding(dx, dy, GameConfig.DashKnightBlinkTiles);
        if (len == 0) return; // no free landing tile: do not spend cooldown
        int ox = Player.X, oy = Player.Y;
        int landX = ox + dx * len, landY = oy + dy * len;
        // BLINK: the sprite appears at the destination instantly (From == land, so the client does not
        // interpolate any slide). A grey smoke "poff" marks where it vanished; the cleave nova marks the landing.
        Emit("effect", ox, oy, 0, 0, GameConfig.DashKnightVanishFx);
        Player.FromX = landX; Player.FromY = landY;
        Player.X = landX; Player.Y = landY;
        Player.StepStartMs = NowMs;
        Player.StepDurMs = 0;
        Player.Facing = FacingFrom(dx, dy);
        DashCleave(landX, landY);
        SpendDash();
    }

    /// <summary>Slides the player from origin to the landing tile (the client interpolates From→X over StepDurMs,
    /// like the monster charge) leaving a <paramref name="trailFx"/> poof trail — reads as fast motion, not a
    /// blink. The trail FX is role-keyed (Archer haste streak, Mage element scorch) so each dash looks distinct.</summary>
    private void SlideDash(int ox, int oy, int dx, int dy, int len, int trailFx)
    {
        Player.FromX = ox; Player.FromY = oy;
        Player.X = ox + dx * len; Player.Y = oy + dy * len;
        Player.StepStartMs = NowMs;
        Player.StepDurMs = Math.Max(len * GameConfig.DashStepMs, 1);
        Player.Facing = FacingFrom(dx, dy);
        for (var i = 0; i <= len; i++)
            Emit("effect", ox + dx * i, oy + dy * i, 0, 0, trailFx);
    }

    private void SpendDash()
    {
        _dashReadyAtMs = NowMs + GameConfig.DashCooldownMs;
        _dashInvulnUntilMs = NowMs + GameConfig.DashIFramesMs;
    }

    /// <summary>Knight cleave: an Exori-style nova hitting every enemy within DashCleaveRadius of the landing.</summary>
    private void DashCleave(int cx, int cy)
    {
        var dmg = PlayerAttack() * RoleSkillMult() * GameConfig.DashCleaveAtkScale;
        var hit = false;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != Player.Floor) continue;
            if (Chebyshev(m.X, m.Y, cx, cy) > GameConfig.DashCleaveRadius) continue;
            DealDamageToMonster(m, dmg, GameConfig.DashStrikeElement, 0, fromSkill: true);
            hit = true;
        }
        if (hit) Emit("effect", cx, cy, 0, 0, GameConfig.DashCleaveFx);
    }

    /// <summary>Mage scorch trail: each dashed tile seeds a weak, short field of the Kaeli's element. It does
    /// NOT spread — the trail stays contained to the tiles the mage slid across (spread is a cast-field
    /// identity). Deliberately weaker than a cast field (low power, short life) so dash never replaces casting.</summary>
    private void DashScorchTrail(int ox, int oy, int dx, int dy, int len)
    {
        var dmg = PlayerAttack() * RoleSkillMult() * GameConfig.DashTrailFieldAtkScale;
        var fx = GameConfig.ElementFieldFx(Waifu.Element); // own element: Rin=fire, Velvet=death, Eloa=holy…
        for (var i = 0; i <= len; i++)
            // telegraph:false — SlideDash already drew the ignition poof on each tile (same element FX).
            SpawnField(ox + dx * i, oy + dy * i, Waifu.Element, fx, dmg,
                1.0, 0, GameConfig.DashTrailFieldTickMs, GameConfig.DashTrailFieldLifeMs,
                GameConfig.DashTrailFieldSpreadChance, GameConfig.DashTrailFieldGenerations, telegraph: false);
    }

    /// <summary>Role-aware dash reach for direction (dx,dy): used to execute the dash and for the helper to pick
    /// the best escape cardinal. Mage stops at wall/mob; Archer passes through mobs (stops at walls); Knight blinks.</summary>
    private int DashReach(int dx, int dy) => _dashSignature switch
    {
        DashSignature.Cleave => BlinkLanding(dx, dy, GameConfig.DashKnightBlinkTiles),
        DashSignature.Sprint => PassThroughLanding(dx, dy, GameConfig.DashTiles),
        _ => DashClearLen(dx, dy),
    };

    /// <summary>Mage dash reach: up to DashTiles, stopping before the first wall OR mob (no pass-through).</summary>
    private int DashClearLen(int dx, int dy)
    {
        int x = Player.X, y = Player.Y, n = 0;
        for (var i = 0; i < GameConfig.DashTiles; i++)
        {
            int nx = x + dx, ny = y + dy;
            if (Floor.IsBlocked(nx, ny) || OccupiedBy(_currentFloor, nx, ny) is not null) break;
            x = nx; y = ny; n++;
        }
        return n;
    }

    /// <summary>Archer sprint reach: up to maxTiles, PASSING THROUGH mobs but never through walls. Returns the
    /// distance of the farthest FREE (unblocked &amp; unoccupied) tile before the first wall, or 0 if none is free.</summary>
    private int PassThroughLanding(int dx, int dy, int maxTiles)
    {
        int best = 0;
        for (var i = 1; i <= maxTiles; i++)
        {
            int nx = Player.X + dx * i, ny = Player.Y + dy * i;
            if (Floor.IsBlocked(nx, ny)) break; // cannot slide through a wall
            if (OccupiedBy(_currentFloor, nx, ny) is null) best = i; // landable (passes over occupied tiles)
        }
        return best;
    }

    /// <summary>Knight blink reach: tries to land EXACTLY `tiles` ahead (passing over a mob in front), but the
    /// landing tile must be FREE (unblocked &amp; unoccupied). Falls back to a nearer free tile; 0 if none.</summary>
    private int BlinkLanding(int dx, int dy, int tiles)
    {
        for (var i = tiles; i >= 1; i--)
        {
            int nx = Player.X + dx * i, ny = Player.Y + dy * i;
            if (!Floor.IsBlocked(nx, ny) && OccupiedBy(_currentFloor, nx, ny) is null) return i;
        }
        return 0;
    }

    private static (int dx, int dy) FacingDelta(Dir facing) => facing switch
    {
        Dir.North => (0, -1),
        Dir.South => (0, 1),
        Dir.East => (1, 0),
        _ => (-1, 0),
    };

    private void GainXp(long xp)
    {
        _runXp += xp;
        while (_runLevel < GameConfig.MaxRunLevel && _runXp >= GameConfig.XpForRunLevel(_runLevel))
        {
            _runXp -= GameConfig.XpForRunLevel(_runLevel);
            _runLevel++;
            Emit("levelup", Player.X, Player.Y, 0, 0, _runLevel);
            Emit("effect", Player.X, Player.Y, 0, 0, 182); // magic powder
            // G-06: level-up = small automatic stat (no screen). Heavy choices come from
            // beats (elite/floor/sanctuary), not every level.
            GrantAutoStatus();
        }
    }

    /// <summary>
    /// G-06: run progress in [0,1] measured by the fraction of choices already granted: used to
    /// scale offer rarity (early builds the engine, late defines it). Deterministic.
    /// </summary>
    private double RunChoiceProgress => GameConfig.MaxCardChoicesPerRun <= 1
        ? 1.0
        : Math.Clamp((_choicesOffered - 1) / (double)(GameConfig.MaxCardChoicesPerRun - 1), 0, 1);

    /// <summary>
    /// G-06: grants a heavy card choice on a fixed beat (defeated elite, cleared floor, sanctuary room).
    /// Sanctuary). Respects the choices-per-run cap and reuses the queue when an offer is already open.
    /// </summary>
    private void OfferCardBeat(bool blessed = false)
    {
        if (Ended is not null || _choicesOffered >= GameConfig.MaxCardChoicesPerRun) return;
        if (AvailableCardPool(null).Count == 0) return;
        _choicesOffered++;
        // G-09: blessed offer (cursed chest) only applies to the offer opened now; queued offers
        // use normal weighting (keeps cap/cadence without stacking blessings).
        if (_pendingOffer is null) OfferCards(blessed);
        else _queuedOffers++;
    }

    /// <summary>
    /// G-06: small automatic stat on level-up: rolls a common card (respecting bans/caps)
    /// and applies one stack immediately, without opening the choice screen. Deterministic via run _rng.
    /// </summary>
    private void GrantAutoStatus()
    {
        var pool = Cards.All
            .Where(c => c.Rarity == Cards.Common && !_bannedCards.Contains(c.Id))
            .Where(c => _cards.GetValueOrDefault(c.Id) < GameConfig.MaxStacksForRarity(c.Rarity))
            .ToList();
        if (pool.Count == 0) return;
        var pick = _rng.Pick(pool);
        ApplyCardStack(pick.Id);
        Emit("text", Player.X, Player.Y, 0, 0, 0, pick.Name);
    }

    /// <summary>Applies a card stack and its immediate effect (for example, maxhp heals the gained bonus).</summary>
    private void ApplyCardStack(string cardId)
    {
        _cards[cardId] = _cards.GetValueOrDefault(cardId) + 1;
        if (cardId == "card:maxhp")
        {
            var def = Cards.ById[cardId];
            var bonus = (int)(Waifu.BaseHp * def.Value);
            Player.MaxHp += bonus;
            HealPlayer(bonus);
        }
    }

    private void OfferCards(bool blessed = false)
    {
        // G-04: rarity-aware pool. Echo filters by active Kaeli; each rarity has its stack cap.
        // stacks. Sampling is rarity-weighted (without replacement), deterministic via run _rng.
        // G-09: blessed (cursed chest) weights like the end of the run: favors rare/echo.
        _offerBlessed = blessed;
        var offer = BuildCardOffer();
        if (offer.Count == 0) { _offerBlessed = false; return; }
        _pendingOffer = offer;
        _cardOfferStartedTick = TickCount;
    }

    /// <summary>G-09: current offer weighting progress: blessed jumps to the end of the curve.</summary>
    private double OfferProgress => _offerBlessed
        ? Math.Max(RunChoiceProgress, GameConfig.BlessedOfferProgress)
        : RunChoiceProgress;

    /// <summary>Builds an offer using the card pool available for the run.</summary>
    private List<CardOfferDto> BuildCardOffer(IReadOnlySet<string>? temporaryExcluded = null)
    {
        var offer = DrawCardOffer(AvailableCardPool(temporaryExcluded));

        if (temporaryExcluded is not null)
        {
            var desiredCount = Math.Min(GameConfig.CardChoicesPerOffer, AvailableCardPool(null).Count);
            if (offer.Count < desiredCount)
            {
                var picked = offer.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
                var fallback = AvailableCardPool(null)
                    .Where(c => temporaryExcluded.Contains(c.Id) && !picked.Contains(c.Id))
                    .ToList();

                while (offer.Count < desiredCount && fallback.Count > 0)
                {
                    var pick = WeightedPickByRarity(fallback);
                    fallback.Remove(pick);
                    offer.Add(ToOfferDto(pick));
                }
            }
        }

        return offer;
    }

    private List<CardDef> AvailableCardPool(IReadOnlySet<string>? temporaryExcluded) =>
        Cards.All
            .Where(c => !_bannedCards.Contains(c.Id))
            .Where(c => temporaryExcluded is null || !temporaryExcluded.Contains(c.Id))
            .Where(c => _cards.GetValueOrDefault(c.Id) < GameConfig.MaxStacksForRarity(c.Rarity))
            .Where(c => c.WaifuId is null || c.WaifuId == Waifu.Id)
            .ToList();

    private List<CardOfferDto> DrawCardOffer(List<CardDef> pool)
    {
        var offer = new List<CardOfferDto>();
        for (var n = 0; n < GameConfig.CardChoicesPerOffer && pool.Count > 0; n++)
        {
            var pick = WeightedPickByRarity(pool);
            pool.Remove(pick);
            offer.Add(ToOfferDto(pick));
        }
        return offer;
    }

    /// <summary>Samples a card from the pool with rarity weight. Stable pool order + _rng -> deterministic.</summary>
    private CardDef WeightedPickByRarity(List<CardDef> pool)
    {
        // G-06: weights scaled by run progress (rare/echo gain weight near the end).
        // G-09: blessed offers (cursed chest) use the jumped progress from OfferProgress.
        var progress = OfferProgress;
        var total = 0.0;
        foreach (var c in pool) total += GameConfig.CardRarityWeight(c.Rarity, progress);
        var roll = _rng.NextDouble() * total;
        foreach (var c in pool)
        {
            roll -= GameConfig.CardRarityWeight(c.Rarity, progress);
            if (roll <= 0) return c;
        }
        return pool[^1]; // guard against floating-point error
    }

    private CardOfferDto ToOfferDto(CardDef c) => new(
        c.Id, c.Name, c.Description, _cards.GetValueOrDefault(c.Id),
        c.Rarity, c.TagList, GameConfig.MaxStacksForRarity(c.Rarity));

    // G-10: best card from the current offer for auto-pick: highest rarity, tie-break by offer order
    // stable (offer index). Deterministic.
    private string BestOfferCardId()
    {
        var offer = _pendingOffer!;
        var bestId = offer[0].Id;
        var bestRank = RarityRank(offer[0].Rarity);
        for (var i = 1; i < offer.Count; i++)
        {
            var rank = RarityRank(offer[i].Rarity);
            if (rank > bestRank)
            {
                bestRank = rank;
                bestId = offer[i].Id;
            }
        }
        return bestId;
    }

    private static int RarityRank(string rarity) => rarity switch
    {
        "echo" => 3,
        "rare" => 2,
        _ => 1
    };

    private void ChooseCard(string cardId)
    {
        if (_pendingOffer is null || !_pendingOffer.Any(o => o.Id == cardId)) return;
        _pendingOffer = null;
        ApplyCardStack(cardId);

        if (_queuedOffers > 0)
        {
            _queuedOffers--;
            OfferCards();
        }
        else _offerBlessed = false;
    }

    private void RerollCards()
    {
        if (_pendingOffer is null) return;
        // G-09: free rerolls first; once exhausted, reroll becomes paid (the run altar "shop").
        var paid = _cardRerollsRemaining <= 0;
        if (paid && _gold < GameConfig.CardRerollGoldCost) return;

        var currentIds = _pendingOffer.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        if (AvailableCardPool(currentIds).Count == 0) return;

        var offer = BuildCardOffer(currentIds);
        if (offer.Count == 0) return;

        if (paid)
        {
            _gold -= GameConfig.CardRerollGoldCost;
            EmitLootFly(GameConfig.GoldCoinItemId, $"-{GameConfig.CardRerollGoldCost} gold", Player.X, Player.Y, isGold: true);
        }
        else _cardRerollsRemaining--;
        _pendingOffer = offer;
        _cardOfferStartedTick = TickCount;
        Emit("text", Player.X, Player.Y, 0, 0, 0, "REROLL");
    }

    private void BanCard(string cardId)
    {
        if (_pendingOffer is null || !_pendingOffer.Any(o => o.Id == cardId)) return;
        if (!_bannedCards.Add(cardId)) return;

        var kept = _pendingOffer.Where(o => o.Id != cardId).ToList();
        var keptIds = kept.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        var pool = AvailableCardPool(keptIds);
        while (kept.Count < GameConfig.CardChoicesPerOffer && pool.Count > 0)
        {
            var pick = WeightedPickByRarity(pool);
            pool.Remove(pick);
            kept.Add(ToOfferDto(pick));
        }

        _pendingOffer = kept.Count > 0 ? kept : null;
        _cardOfferStartedTick = TickCount;
        Emit("text", Player.X, Player.Y, 0, 0, 0, "BANNED");

        if (_pendingOffer is null && _queuedOffers > 0)
        {
            _queuedOffers--;
            OfferCards();
        }
    }

    private void HealPlayer(int amount)
    {
        if (amount <= 0 || Player.Hp <= 0) return;
        Player.Hp = Math.Min(Player.Hp + amount, Player.MaxHp);
    }

    private void TickPlayerRegen()
    {
        if (Player.Hp <= 0) return;
        // baseline regen (independent of card) + regen card bonus
        var baselinePct = GameConfig.BaselineRegenPctPerSec
                          + GameConfig.BaselineRegenPctPerRunLevel * (_runLevel - 1);
        var regen = CardValue("regenPerSec") + Player.MaxHp * baselinePct;
        if (regen <= 0) return;
        _regenCarry += regen * GameConfig.TickMs / 1000.0;
        if (_regenCarry >= 1)
        {
            var whole = (int)_regenCarry;
            _regenCarry -= whole;
            HealPlayer(whole);
        }
    }

    // ---- conditions on the player (T-53/T-14: tibia DoT + slow) ----

    private void ApplyConditionToPlayer(MonsterCondition cond, Actor source)
    {
        var tickMs = cond.TickMs > 0 ? cond.TickMs : GameConfig.ConditionDefaultTickMs;
        var ticks = cond.DurationMs > 0 ? Math.Max(cond.DurationMs / tickMs, 1) : GameConfig.ConditionMaxTicks;
        ticks = Math.Min(ticks, GameConfig.ConditionMaxTicks);
        var perTick = Math.Max(
            (int)(cond.TotalDamage / (double)ticks * source.StatMult
                  * (source.Species?.IsAuthored == true ? 1 : GameConfig.MonsterDamageTuning)), 1);

        // reapplying never stacks: it replaces only if the new condition is at least as strong
        var existing = _playerConditions.FirstOrDefault(c => c.Type == cond.Type);
        if (existing is not null)
        {
            if ((long)perTick * ticks < (long)existing.DamagePerTick * existing.TicksLeft) return;
            _playerConditions.Remove(existing);
        }

        _playerConditions.Add(new ActiveCondition
        {
            Type = cond.Type, DamagePerTick = perTick, TicksLeft = ticks,
            TickMs = tickMs, NextTickAtMs = NowMs + tickMs,
        });
        if (GameConfig.ConditionTickFx.TryGetValue(cond.Type, out var fx))
            Emit("effect", Player.X, Player.Y, 0, 0, fx);
    }

    private void TickPlayerConditions()
    {
        if (Player.Hp <= 0 || _playerConditions.Count == 0) return;
        var resist = Math.Min(CardValue("conditionResist"), GameConfig.ConditionResistCap);
        for (var i = _playerConditions.Count - 1; i >= 0; i--)
        {
            var cond = _playerConditions[i];
            if (NowMs < cond.NextTickAtMs) continue;
            cond.NextTickAtMs += cond.TickMs;
            if (--cond.TicksLeft <= 0) _playerConditions.RemoveAt(i);

            var damage = Math.Max((int)(cond.DamagePerTick * (1 - resist)), 1);
            Player.Hp -= damage;
            _gauge = Math.Min(_gauge + damage * GameConfig.GaugeFillPerDamageTaken * (1 + CardValue("gaugePercent")) * _gaugeRate, GameConfig.UltimateGaugeMax);
            AddRynnaCharge(damage * GameConfig.RynnaChargePerDamageTaken
                * (IsBuffActive(GameConfig.RynnaBloodlustBuff) ? GameConfig.RynnaBloodlustChargeTakenMultiplier : 1));
            Emit("damage", Player.X, Player.Y, 0, 0, damage, cond.Type, Player.Id);
            if (GameConfig.ConditionTickFx.TryGetValue(cond.Type, out var fx))
                Emit("effect", Player.X, Player.Y, 0, 0, fx);

            if (Player.Hp <= 0)
            {
                Player.Hp = 0;
                Emit("effect", Player.X, Player.Y, 0, 0, 18); // mort area
                EndRun(false, $"killed by {GameConfig.ConditionLabel.GetValueOrDefault(cond.Type, cond.Type)}");
                return;
            }
        }
    }

    /// <summary>Condition/slow payloads that ride on an attack that connected with the player.</summary>
    private void ApplyAttackSideEffects(Actor monster, MonsterAttack attack)
    {
        if (Player.Hp <= 0) return;
        if (attack.Condition is { } cond) ApplyConditionToPlayer(cond, monster);
        if (attack.SpeedChange < 0)
        {
            var duration = attack.DurationMs > 0 ? Math.Min(attack.DurationMs, GameConfig.SlowDurationCapMs) : GameConfig.SlowDurationCapMs;
            _playerSlowUntilMs = NowMs + duration;
            _playerSlowFactor = Math.Clamp(1 + attack.SpeedChange / GameConfig.SpeedChangeReference, GameConfig.SlowFactorFloor, 1.0);
            Emit("text", Player.X, Player.Y, 0, 0, 0, "SLOWED");
        }
    }

    // ---- combat: monsters ----

    private void TickMonsters()
    {
        // indexed loop with a fixed upper bound: summons spawned this tick are appended
        // at the end of the list and only start acting next tick (stable, deterministic order)
        var count = _monsters.Count;
        for (var idx = 0; idx < count; idx++)
        {
            var monster = _monsters[idx];
            if (monster.Hp <= 0 || monster.Floor != _currentFloor) continue;
            if (monster.Dots.Count > 0) { TickMonsterDots(monster); if (monster.Hp <= 0) continue; }
            // Training dummy: DoTs still apply (above) so curses/burns read, but it never chases or attacks.
            if (monster.IsTrainingDummy) { TickTrainingDummy(monster); continue; }
            var species = monster.Species!;
            var dist = Chebyshev(monster, Player);
            var hasLos = HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y);

            // voices (tibia flavor)
            if (species.Voices.Count > 0 && NowMs >= monster.NextVoiceAtMs)
            {
                monster.NextVoiceAtMs = NowMs + GameConfig.VoiceIntervalMs + _rng.Next(6000);
                if (dist <= 9 && _rng.Chance(GameConfig.VoiceChancePercent / 100.0))
                    Emit("voice", monster.X, monster.Y, 0, 0, 0, _rng.Pick(species.Voices), monster.Id);
            }

            // acquire target (requires line of sight; no aggro through cave walls)
            if (monster.TargetId == 0 && Player.Hp > 0
                && dist <= GameConfig.MonsterAggroRange
                && hasLos)
                AcquirePlayer(monster);

            if (monster.TargetId != 0)
            {
                if (hasLos)
                    monster.LastSawPlayerAtMs = NowMs;

                if (dist > GameConfig.AggroDropRange)
                {
                    if (monster.AggroOutOfRangeSinceMs == 0)
                        monster.AggroOutOfRangeSinceMs = NowMs;
                }
                else
                {
                    monster.AggroOutOfRangeSinceMs = 0;
                }

                var tooFarTooLong = monster.AggroOutOfRangeSinceMs > 0
                                     && NowMs - monster.AggroOutOfRangeSinceMs >= GameConfig.AggroDropOutOfRangeMs;
                var unseenTooLong = !hasLos
                                    && NowMs - monster.LastSawPlayerAtMs >= GameConfig.AggroDropNoLosMs;
                if (tooFarTooLong || unseenTooLong)
                    DropAggro(monster);
            }

            if (monster.IsStunned(NowMs)) continue;

            if (monster.TargetId == 0 || Player.Hp <= 0)
            {
                Wander(monster);
                continue;
            }

            TryMonsterAttacks(monster);
            TickMonsterDefenses(monster);
            TickMonsterSummons(monster);
            TickMonsterShield(monster);

            if (monster.IsMoving(NowMs)) continue;

            // Taunted (melee rider "taunt"): drops kiting and flight, marches into melee.
            // BOSS always pursues: runs after the player instead of planting as a ranged turret.
            var taunted = NowMs < monster.TauntedUntilMs;
            var chaser = taunted || monster.IsBossActor;

            // low-health flight (tibia runHealth: dragons & co. retreat while still attacking): boss does not flee.
            if (!chaser && species.RunOnHealth > 0 && monster.Hp <= species.RunOnHealth * monster.StatMult)
            {
                StepAway(monster, Player.X, Player.Y);
                continue;
            }

            // chase: move toward player keeping targetDistance for ranged species.
            // Pursuer (taunted/boss) only stops to attack when already adjacent; otherwise closes distance.
            if ((!chaser || dist <= GameConfig.MeleeRange)
                && CanAttackPlayer(monster, dist, hasLos)
                && _rng.Chance(Math.Clamp(species.StaticAttackChance, 0, 100) / 100.0))
            {
                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);
                continue;
            }

            var desired = chaser ? 1 : Math.Max(species.TargetDistance, 1);
            if (dist > desired)
                StepToward(monster, Player.X, Player.Y);
            else if (dist < desired && !chaser && species.TargetDistance > 1 && _rng.Chance(0.5))
                StepAway(monster, Player.X, Player.Y);
            else
                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);
        }
    }

    private void AcquirePlayer(Actor monster)
    {
        monster.TargetId = Player.Id;
        monster.LastSawPlayerAtMs = NowMs;
        monster.AggroOutOfRangeSinceMs = 0;
        // Lunara: Eternal Winter makes the enemy start slowed when seeing Lunara.
        if (HasEcho("eternal_winter") && monster.Hp > 0 && NowMs >= monster.SlowUntilMs)
        {
            monster.SlowUntilMs = NowMs + GameConfig.EchoEternalWinterAggroSlowMs;
            monster.SlowFactor = GameConfig.EchoEternalWinterSlowFactor;
        }
    }

    private static void DropAggro(Actor monster)
    {
        monster.TargetId = 0;
        monster.LastSawPlayerAtMs = 0;
        monster.AggroOutOfRangeSinceMs = 0;
    }

    private bool CanAttackPlayer(Actor monster, int dist, bool hasLos)
    {
        foreach (var attack in monster.Species!.Attacks)
        {
            if (attack.Kind == "melee")
            {
                if (dist <= GameConfig.MeleeRange) return true;
                continue;
            }

            var range = attack.Range > 0 ? attack.Range : (attack.Radius > 0 || attack.Length > 0 ? 7 : 1);
            if (dist <= range && hasLos) return true;
        }
        return false;
    }

    private void TryMonsterAttacks(Actor monster)
    {
        var species = monster.Species!;
        for (var i = 0; i < species.Attacks.Count; i++)
        {
            var attack = species.Attacks[i];
            if (NowMs < monster.AttackReadyAtMs[i]) continue;
            var dist = Chebyshev(monster, Player);

            if (attack.IsHealing)
            {
                // support kit: heal the most wounded ally in range (the "Echo Doc" pattern)
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;
                var range = attack.Range > 0 ? attack.Range : 7;
                var ally = MostWoundedAlly(monster, range);
                if (ally is null) continue;
                var amount = (int)(_rng.Range(Math.Min(attack.MinDamage, attack.MaxDamage),
                    Math.Max(attack.MinDamage, attack.MaxDamage)) * monster.StatMult);
                HealMonster(ally, amount);
                if (attack.AreaEffect > 0) Emit("effect", ally.X, ally.Y, 0, 0, attack.AreaEffect);
                continue;
            }

            if (attack.Kind == "charge")
            {
                // G-08B: charge needs room to run (not already adjacent) and line of sight.
                if (monster.IsMoving(NowMs)) continue;
                var chargeRange = attack.Range > 0 ? attack.Range : GameConfig.ChargeMaxTiles + 1;
                if (dist < 2 || dist > chargeRange) continue;
                if (!HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y)) continue;
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;
                ChargeAt(monster, attack);
                continue;
            }

            if (attack.Kind == "melee")
            {
                if (dist > 1) continue;
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;
                HitPlayerWithAttack(monster, attack);
                Emit("effect", Player.X, Player.Y, 0, 0, 1); // blood
            }
            else
            {
                var range = attack.Range > 0 ? attack.Range : (attack.Radius > 0 || attack.Length > 0 ? 7 : 1);
                if (dist > range) continue;
                if (!HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y)) continue;
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;

                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);

                if (attack.ShootEffect > 0)
                    Emit("projectile", monster.X, monster.Y, Player.X, Player.Y, attack.ShootEffect);

                if (attack.Length > 0)
                {
                    // wave (e.g. dragon fire): cone toward player
                    var (dx, dy) = DirDelta(monster.Facing, Player);
                    var hitPlayer = false;
                    foreach (var (tx, ty) in ConeTiles(monster.X, monster.Y, dx, dy, Math.Min(attack.Length, 5)))
                    {
                        if (attack.AreaEffect > 0) Emit("effect", tx, ty, 0, 0, attack.AreaEffect);
                        if (tx == Player.X && ty == Player.Y) hitPlayer = true;
                    }
                    if (hitPlayer) HitPlayerWithAttack(monster, attack);
                }
                else if (attack.Radius > 0)
                {
                    var cx = attack.Target ? Player.X : monster.X;
                    var cy = attack.Target ? Player.Y : monster.Y;
                    var hitPlayer = false;
                    foreach (var (tx, ty) in CircleTiles(cx, cy, attack.Radius))
                    {
                        if (attack.AreaEffect > 0) Emit("effect", tx, ty, 0, 0, attack.AreaEffect);
                        if (tx == Player.X && ty == Player.Y) hitPlayer = true;
                    }
                    if (hitPlayer) HitPlayerWithAttack(monster, attack);
                }
                else
                {
                    if (attack.AreaEffect > 0) Emit("effect", Player.X, Player.Y, 0, 0, attack.AreaEffect);
                    HitPlayerWithAttack(monster, attack);
                }
            }
        }
    }

    /// <summary>An attack connected with the player: damage (if any) + condition/slow payloads.</summary>
    private void HitPlayerWithAttack(Actor monster, MonsterAttack attack)
    {
        if (attack.MaxDamage > 0 || attack.Kind == "melee" || attack.Kind == "charge")
            DamagePlayer(RollMonsterDamage(monster, attack), attack.DamageType, monster);
        ApplyAttackSideEffects(monster, attack);
    }

    /// <summary>G-08B: charger advances in a straight line to the target (stops 1 tile away), then strikes if adjacent.
    /// The displacement is a single "long" step that the client interpolates as a dash. Deterministic:
    /// the chance was already rolled by the caller; here it only walks free tiles.</summary>
    private void ChargeAt(Actor monster, MonsterAttack attack)
    {
        var startX = monster.X;
        var startY = monster.Y;
        var cx = startX;
        var cy = startY;
        var floor = _floors[monster.Floor];
        for (var step = 0; step < GameConfig.ChargeMaxTiles; step++)
        {
            if (Chebyshev(cx, cy, Player.X, Player.Y) <= 1) break; // stop 1 tile from the target
            var dx = Math.Sign(Player.X - cx);
            var dy = Math.Sign(Player.Y - cy);
            if (dx == 0 && dy == 0) break;
            if (dx != 0 && dy != 0 && (floor.IsBlocked(cx + dx, cy) || floor.IsBlocked(cx, cy + dy))) break;
            var nx = cx + dx;
            var ny = cy + dy;
            if (floor.IsBlocked(nx, ny)) break;
            var occ = OccupiedBy(monster.Floor, nx, ny);
            if (occ is not null && occ.Id != monster.Id) break;
            cx = nx;
            cy = ny;
        }

        if (cx != startX || cy != startY)
        {
            monster.FromX = startX;
            monster.FromY = startY;
            monster.X = cx;
            monster.Y = cy;
            monster.StepStartMs = NowMs;
            monster.StepDurMs = GameConfig.ChargeDashMs;
            monster.Facing = FacingFrom(cx - startX, cy - startY, monster.Facing);
            Emit("effect", startX, startY, 0, 0, GameConfig.ChargeFx);
            Emit("effect", cx, cy, 0, 0, GameConfig.ChargeFx);
        }

        if (Chebyshev(monster, Player) <= 1)
        {
            HitPlayerWithAttack(monster, attack);
            Emit("effect", Player.X, Player.Y, 0, 0, 1); // blood
        }
    }

    private Actor? MostWoundedAlly(Actor healer, int range)
    {
        Actor? best = null;
        var bestMissing = 0;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != healer.Floor) continue;
            if (Chebyshev(m, healer) > range) continue;
            var missing = m.MaxHp - m.Hp;
            if (missing > bestMissing) { bestMissing = missing; best = m; }
        }
        return best;
    }

    private void HealMonster(Actor monster, int amount)
    {
        var cap = Math.Max((int)(monster.MaxHp * GameConfig.MonsterHealCapFraction), 1);
        var healed = Math.Min(Math.Clamp(amount, 1, cap), monster.MaxHp - monster.Hp);
        if (healed <= 0) return;
        monster.Hp += healed;
        Emit("heal", monster.X, monster.Y, 0, 0, healed, "", monster.Id);
    }

    private void TickMonsterDefenses(Actor monster)
    {
        var defenses = monster.Species!.Defenses;
        for (var i = 0; i < defenses.Count; i++)
        {
            var defense = defenses[i];
            if (NowMs < monster.DefenseReadyAtMs[i]) continue;
            monster.DefenseReadyAtMs[i] = NowMs + defense.IntervalMs;
            if (!_rng.Chance(Math.Min(defense.Chance, 100) / 100.0)) continue;

            if (defense.Kind == "healing")
            {
                if (monster.Hp >= monster.MaxHp) continue;
                var amount = (int)(_rng.Range(Math.Min(defense.MinValue, defense.MaxValue),
                    Math.Max(defense.MinValue, defense.MaxValue)) * monster.StatMult);
                HealMonster(monster, amount);
                if (defense.AreaEffect > 0) Emit("effect", monster.X, monster.Y, 0, 0, defense.AreaEffect);
            }
            else if (defense.SpeedChange > 0)
            {
                monster.HasteUntilMs = NowMs + (defense.DurationMs > 0 ? defense.DurationMs : GameConfig.DefaultHasteDurationMs);
                monster.HasteFactor = Math.Clamp(1 + defense.SpeedChange / GameConfig.SpeedChangeReference, 1.0, GameConfig.HasteFactorCap);
                if (defense.AreaEffect > 0) Emit("effect", monster.X, monster.Y, 0, 0, defense.AreaEffect);
            }
        }
    }

    private void TickMonsterSummons(Actor monster)
    {
        var species = monster.Species!;
        if (species.Summons.Count == 0) return;
        for (var i = 0; i < species.Summons.Count; i++)
        {
            var summon = species.Summons[i];
            if (NowMs < monster.SummonReadyAtMs[i]) continue;
            monster.SummonReadyAtMs[i] = NowMs + Math.Max(summon.IntervalMs, GameConfig.SummonMinIntervalMs);
            if (!_monsterRegistry.Contains(summon.Name)) continue;
            if (CountOwnedSummons(monster.Id) >= Math.Max(species.MaxSummons, 1)) continue;
            if (CountAliveSummons() >= GameConfig.MaxAliveSummons) continue;
            if (!_rng.Chance(Math.Min(summon.Chance, 100) / 100.0)) continue;

            for (var c = 0; c < Math.Max(summon.Count, 1); c++)
            {
                if (CountOwnedSummons(monster.Id) >= Math.Max(species.MaxSummons, 1)) break;
                if (CountAliveSummons() >= GameConfig.MaxAliveSummons) break;
                if (SpawnSummon(monster, summon.Name) is null) break;
            }
        }
    }

    /// <summary>G-08B: shieldbearer raises an echo barrier on the nearby wounded ally without a shield.
    /// Deterministic: scans _monsters in stable order, tie-breaks by lowest id; no _rng.</summary>
    private void TickMonsterShield(Actor shielder)
    {
        if (GameConfig.BehaviorProfile(shielder.Species!.BehaviorId) is not { ShieldFraction: > 0 } profile) return;
        if (NowMs < shielder.ShieldCastReadyAtMs) return;
        shielder.ShieldCastReadyAtMs = NowMs + Math.Max(profile.ShieldIntervalMs, GameConfig.TickMs);

        Actor? ally = null;
        var bestMissing = -1;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Id == shielder.Id || m.Floor != shielder.Floor) continue;
            if (m.MonsterShield > 0) continue;
            if (Chebyshev(shielder.X, shielder.Y, m.X, m.Y) > profile.ShieldRadius) continue;
            var missing = m.MaxHp - m.Hp;
            if (missing > bestMissing || (missing == bestMissing && (ally is null || m.Id < ally.Id)))
            {
                bestMissing = missing;
                ally = m;
            }
        }
        if (ally is null) return;

        var amount = Math.Max(ally.MaxHp * Math.Min(profile.ShieldFraction, GameConfig.MonsterShieldCapFraction), 1);
        ally.MonsterShield = amount;
        Emit("effect", ally.X, ally.Y, 0, 0, GameConfig.MonsterShieldFx);
    }

    private int CountOwnedSummons(int ownerId) =>
        _monsters.Count(m => m.OwnerId == ownerId && m.Hp > 0);

    private int CountAliveSummons() =>
        _monsters.Count(m => m.IsSummon && m.Hp > 0);

    private Actor? SpawnSummon(Actor owner, string speciesName)
    {
        var species = _monsterRegistry.Get(speciesName);
        var floor = _floors[owner.Floor];
        // deterministic ring scan around the summoner (radius 1 then 2, fixed order)
        for (var radius = 1; radius <= 2; radius++)
            for (var dy = -radius; dy <= radius; dy++)
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                    var x = owner.X + dx;
                    var y = owner.Y + dy;
                    if (floor.IsBlocked(x, y) || OccupiedBy(owner.Floor, x, y) is not null) continue;

                    var actor = new Actor
                    {
                        Id = _nextActorId++,
                        Species = species,
                        Floor = owner.Floor,
                        X = x, Y = y, FromX = x, FromY = y,
                        Hp = (int)(species.Health * owner.StatMult),
                        MaxHp = (int)(species.Health * owner.StatMult),
                        AttackReadyAtMs = new long[Math.Max(species.Attacks.Count, 1)],
                        DefenseReadyAtMs = new long[species.Defenses.Count],
                        SummonReadyAtMs = new long[species.Summons.Count],
                        NextWanderAtMs = _rng.Range(0, GameConfig.MonsterWanderIntervalMs),
                        StatMult = owner.StatMult,
                        OwnerId = owner.Id,
                        Facing = (Dir)_rng.Next(4)
                    };
                    AcquirePlayer(actor);
                    _monsters.Add(actor);
                    Emit("effect", x, y, 0, 0, 11); // teleport poof
                    return actor;
                }
        return null;
    }

    private int RollMonsterDamage(Actor monster, MonsterAttack attack)
    {
        var min = Math.Min(attack.MinDamage, attack.MaxDamage);
        var max = Math.Max(attack.MinDamage, attack.MaxDamage);
        var roll = _rng.Range(min, max) * monster.StatMult;
        var tuning = monster.Species?.IsAuthored == true ? 1 : GameConfig.MonsterDamageTuning;
        return Math.Max((int)(roll * tuning), 1);
    }

    private void DamagePlayer(int damage, string damageType, Actor source)
    {
        if (Player.Hp <= 0) return;
        // Dodge: short i-frames right after dash cancel the hit (market-standard dodge).
        if (NowMs < _dashInvulnUntilMs)
        {
            Emit("text", Player.X, Player.Y, 0, 0, 0, "DODGE");
            return;
        }
        var traitReduction = _trait.Kind switch
        {
            "fortress" => _trait.Value * _traitMult,
            "bulwark" when Player.Hp < Player.MaxHp * _trait.Param => _trait.Value * _traitMult,
            _ => 0
        };
        // Seren: Immortal Stance cuts incoming damage with high combo and Zenith Stance.
        var immortal = HasEcho("immortal_stance") && NowMs <= _comboExpireMs
                       && _comboHits >= GameConfig.EchoImmortalComboThreshold
            ? GameConfig.EchoImmortalDamageReduction : 0;
        var reduction = Math.Min(
            CardValue("damageReduction") + EquipmentStats.DamageReduction
            + Loadout.Mastery.DamageReductionBonus + traitReduction + immortal,
            GameConfig.PlayerDamageReductionCap);
        var resistance = EquipmentStats.Resistance(damageType);
        var final = damage * (1 - reduction) * (1 - resistance);
        if (NowMs < source.SappedUntilMs)
            final *= GameConfig.SappedStrengthDamageMultiplier;
        if (IsBuffActive("shield")) final *= 0.5;
        if (IsBuffActive(GameConfig.RynnaBloodlustBuff))
            final *= GameConfig.RynnaBloodlustDamageTakenMultiplier;
        var value = Math.Max((int)final, 1);

        // Eloa Martyr / Velvet Pact: Echo shield absorbs before health.
        value = AbsorbWithEchoShield(value);
        if (value <= 0)
        {
            Emit("text", Player.X, Player.Y, 0, 0, 0, "SHIELD");
            return;
        }

        Player.Hp -= value;
        _gauge = Math.Min(_gauge + value * GameConfig.GaugeFillPerDamageTaken * (1 + CardValue("gaugePercent")) * _gaugeRate, GameConfig.UltimateGaugeMax);
        AddRynnaCharge(value * GameConfig.RynnaChargePerDamageTaken
            * (IsBuffActive(GameConfig.RynnaBloodlustBuff) ? GameConfig.RynnaBloodlustChargeTakenMultiplier : 1));
        Emit("damage", Player.X, Player.Y, 0, 0, value, damageType, Player.Id);

        if (Player.Hp <= 0)
        {
            Player.Hp = 0;
            Emit("effect", Player.X, Player.Y, 0, 0, 18); // mort area
            EndRun(false, $"killed by {source.Species?.Name ?? "?"}");
        }
    }

    private void Wander(Actor monster)
    {
        if (monster.IsMoving(NowMs) || NowMs < monster.NextWanderAtMs) return;
        monster.NextWanderAtMs = NowMs + GameConfig.MonsterWanderIntervalMs + _rng.Next(2000);
        if (!_rng.Chance(0.4)) return;
        var dx = _rng.Range(-1, 1);
        var dy = _rng.Range(-1, 1);
        TryStep(monster, dx, dy, MonsterSpeed(monster));
    }

    private void StepToward(Actor monster, int tx, int ty)
    {
        var dx = Math.Sign(tx - monster.X);
        var dy = Math.Sign(ty - monster.Y);
        var speed = MonsterSpeed(monster);
        var forward = new List<(int Dx, int Dy)>(3);
        AddStepCandidate(forward, dx, dy);
        AddStepCandidate(forward, dx, 0);
        AddStepCandidate(forward, 0, dy);
        if (TryBestMonsterStep(monster, tx, ty, speed, forward)) return;

        var lateral = new List<(int Dx, int Dy)>(2);
        if (_rng.Next(2) == 0)
        {
            AddStepCandidate(lateral, -dy, dx);
            AddStepCandidate(lateral, dy, -dx);
        }
        else
        {
            AddStepCandidate(lateral, dy, -dx);
            AddStepCandidate(lateral, -dy, dx);
        }
        TryBestMonsterStep(monster, tx, ty, speed, lateral);
    }

    private static void AddStepCandidate(List<(int Dx, int Dy)> candidates, int dx, int dy)
    {
        if ((dx != 0 || dy != 0) && !candidates.Contains((dx, dy)))
            candidates.Add((dx, dy));
    }

    private bool TryBestMonsterStep(Actor monster, int tx, int ty, int speed,
        List<(int Dx, int Dy)> candidates)
    {
        var open = candidates
            .Where(c => CanStep(monster, c.Dx, c.Dy))
            .Select(c => new
            {
                Step = c,
                Distance = Chebyshev(monster.X + c.Dx, monster.Y + c.Dy, tx, ty),
                Neighbors = AdjacentLivingMonsters(monster, monster.X + c.Dx, monster.Y + c.Dy)
            })
            .ToList();
        if (open.Count == 0) return false;

        var bestDistance = open.Min(c => c.Distance);
        var sameDistance = open.Where(c => c.Distance == bestDistance).ToList();
        var leastCrowded = sameDistance.Min(c => c.Neighbors);
        var choice = sameDistance.First(c => c.Neighbors == leastCrowded);
        return TryStep(monster, choice.Step.Dx, choice.Step.Dy, speed);
    }

    private int AdjacentLivingMonsters(Actor actor, int x, int y) =>
        _monsters.Count(m => m.Id != actor.Id && m.Hp > 0 && m.Floor == actor.Floor
                             && Chebyshev(m.X, m.Y, x, y) <= 1);

    private void StepAway(Actor monster, int tx, int ty)
    {
        var dx = -Math.Sign(tx - monster.X);
        var dy = -Math.Sign(ty - monster.Y);
        TryStep(monster, dx, dy, MonsterSpeed(monster));
    }

    // ---- pickup / POIs ----

    private void AddLootedItem(int itemId, string name, int count)
    {
        var existing = ItemsLooted.FirstOrDefault(item => item.ItemId == itemId);
        if (existing is null)
        {
            ItemsLooted.Add(new RewardItemDto(itemId, name, count));
            return;
        }

        ItemsLooted.Remove(existing);
        ItemsLooted.Add(existing with { Count = existing.Count + count });
    }

    private void TryInteract(int x, int y)
    {
        if (Player.Hp <= 0 || Chebyshev(Player.X, Player.Y, x, y) > 1) return;
        var poi = _pois.FirstOrDefault(p => p.Floor == _currentFloor && p.X == x && p.Y == y && !p.Used);
        if (poi is null) return;

        if (poi.Kind == "ladder")
        {
            DescendToFloor(1);
            return;
        }

        if (poi.Kind == "sanctuary")
        {
            // G-06: Echo Sanctuary altar: guaranteed choice beat (signaled on minimap).
            poi.Used = true;
            Emit("effect", x, y, 0, 0, 49); // holy/energy burst
            Emit("text", x, y, 0, 0, 0, "ECHO SANCTUARY");
            OfferCardBeat();
            return;
        }

        // G-09: chest = Echo altar / run shop. Variants: mimic (fight), cursed (greed), and normal.
        poi.Used = true;
        _chestsOpened++;

        if (poi.Variant == "mimic")
        {
            OpenMimic(x, y);
            return;
        }

        Emit("effect", x, y, 0, 0, 3); // poff
        var cursed = poi.Variant == "cursed";
        if (cursed) ApplyChestCurse(x, y);

        // chest loot (keeps the chest gratifying beyond the altar card)
        GrantChestLoot(x, y);

        // Echo material (grows the account): cursed guarantees N, normal by chance
        if (cursed)
            for (var i = 0; i < GameConfig.CursedChestMaterialDrops; i++) GrantGearMaterial(x, y);
        else if (_rng.Chance(GameConfig.ChestMaterialDropChance))
            GrantGearMaterial(x, y);

        // altar: opens a card offer (overlay reuses reroll/ban/shop). Cursed = blessed offer.
        OfferCardBeat(blessed: cursed);
    }

    /// <summary>G-09: raw chest loot (gold + equippable tier items), flying to the player.</summary>
    private void GrantChestLoot(int x, int y)
    {
        var gold = (long)(_rng.Range(40, 120) * Tier.StatMultiplier * (1 + CardValue("goldPercent")));
        _gold += gold;
        EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", x, y, isGold: true);
        Emit("effect", x, y, 0, 0, 29); // fireworks

        if (Tier.CommonMobs.Concat(Tier.EliteMobs).All(name => _monsterRegistry.Get(name).IsAuthored))
        {
            for (var i = 0; i < GameConfig.KaezanChestItemDrops; i++)
                if (TryPickKaezanDropItem("chest", relicOnly: false, out var item))
                    CollectLoot(item.ItemId, item.Name, 1, x, y);
        }
        else
        {
            var lootPool = Tier.CommonMobs.Concat(Tier.EliteMobs)
                .SelectMany(name => _monsterRegistry.Get(name).Loot)
                .Where(l => _data.IsEquippableLoot(l.ItemId))
                .ToList();
            for (var i = 0; i < 2 && lootPool.Count > 0; i++)
            {
                var entry = _rng.Pick(lootPool);
                CollectLoot(entry.ItemId, entry.Name, 1, x, y);
            }
        }
    }

    /// <summary>G-09: tier Echo material (grows the account, feeds the Kaeli equipment screen).</summary>
    private void GrantGearMaterial(int x, int y)
    {
        var name = GameConfig.GearMaterialName(Tier.Tier);
        AddLootedItem(GameConfig.GearMaterialItemId(Tier.Tier), name, 1);
        EmitLootFly(GameConfig.GearMaterialFlySpriteId, name, x, y, isGold: false);
    }

    /// <summary>G-09: common ambush in the chest (risk cost). Deterministic via _rng.</summary>
    private void SpawnChestAmbush(int x, int y, int count)
    {
        var room = Floor.Rooms.FirstOrDefault(r => r.Contains(x, y)) ?? Floor.Rooms[0];
        for (var i = 0; i < count; i++)
        {
            var mob = SpawnMonster(_currentFloor, _rng.Pick(Tier.CommonMobs), room);
            if (mob is not null)
            {
                AcquirePlayer(mob);
                Emit("effect", mob.X, mob.Y, 0, 0, 11); // teleport
            }
        }
    }

    /// <summary>G-09: cursed chest: ambush + curse (slow) on the player. Greed vs safety.</summary>
    private void ApplyChestCurse(int x, int y)
    {
        Emit("effect", x, y, 0, 0, 18); // mort area
        Emit("text", x, y, 0, 0, 0, "CURSED CHEST!");
        SpawnChestAmbush(x, y, GameConfig.CursedChestAmbush);
        _playerSlowUntilMs = NowMs + GameConfig.CursedChestSlowMs;
        _playerSlowFactor = GameConfig.CursedChestSlowFactor;
    }

    /// <summary>G-09: mimic: corrupted Echo chest that spawns on top of the chest, reinforced, and charges in.</summary>
    private void OpenMimic(int x, int y)
    {
        Emit("effect", x, y, 0, 0, 11); // teleport
        Emit("text", x, y, 0, 0, 0, "MIMIC!");
        var room = Floor.Rooms.FirstOrDefault(r => r.Contains(x, y)) ?? Floor.Rooms[0];
        var mimic = SpawnMonster(_currentFloor, _rng.Pick(Tier.EliteMobs), room, isElite: true);
        if (mimic is null) return;
        mimic.X = mimic.FromX = x;
        mimic.Y = mimic.FromY = y;
        mimic.MaxHp = mimic.Hp = (int)(mimic.Hp * GameConfig.MimicHpScale);
        mimic.IsMimic = true;
        AcquirePlayer(mimic);
    }

    private void DescendToFloor(int floorIndex)
    {
        _currentFloor = floorIndex;
        Player.Floor = floorIndex;
        _floorEnteredMs = NowMs; // G-10: re-arms auto-loot delay on entering the new floor
        _mobLastX = int.MinValue; _mobLastY = int.MinValue; // resets orbit anti-backtracking on the new floor
        _killsSinceChest = 0; // re-arms the dropped-chest cadence on the new floor
        var entry = _floors[floorIndex].Entry;
        Player.X = entry.X; Player.Y = entry.Y;
        Player.FromX = entry.X; Player.FromY = entry.Y;
        Player.StepDurMs = 0;
        Player.TargetId = 0;
        _manualTargetId = 0;
        MapDirty = true;
        Emit("effect", entry.X, entry.Y, 0, 0, 11);

        // G-06: clearing a floor is a beat: grants a choice (predictable progression milestone).
        if (GameConfig.OfferChoiceOnFloorClear) OfferCardBeat();
    }

    // ---- run end ----

    internal void EndRun(bool victory, string reason)
    {
        if (Ended is not null) return;
        // Training Room is a sandbox: leaving grants nothing (no gold/xp/kaeros) so it can't be AFK-farmed.
        if (Mode == GameMode.Training)
        {
            Ended = new RunEndDto(false, reason, 0, 0, 0, _kills, _runLevel, NowMs, ItemsLooted.ToList(), []);
            return;
        }
        var goldEarned = victory ? _gold : (long)(_gold * GameConfig.DefeatGoldKeptFraction);
        var kaeros = victory ? GameConfig.VictoryKaerosBase + GameConfig.VictoryKaerosPerTier * (Tier.Tier - 1) : 0;
        var accountXp = (victory ? GameConfig.AccountXpPerVictory : GameConfig.AccountXpPerDefeat)
                        + GameConfig.AccountXpPerRunLevel * _runLevel;

        Ended = new RunEndDto(
            victory, reason, goldEarned, accountXp, kaeros,
            _kills, _runLevel, NowMs,
            ItemsLooted.ToList(), []);
    }

    // ---- geometry helpers ----

    /// <summary>Bresenham walk over blocked tiles (endpoints excluded).</summary>
    private bool HasLineOfSight(int x0, int y0, int x1, int y1)
    {
        var floor = Floor;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        int x = x0, y = y0;
        while (x != x1 || y != y1)
        {
            var px = x;
            var py = y;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
            if (DiagonalCornerBlocked(floor, px, py, x, y)) return false;
            if ((x != x1 || y != y1) && floor.IsBlocked(x, y)) return false;
        }
        return true;
    }

    /// <summary>
    /// Walks the player -> (tx,ty) line and returns the farthest reachable tile: limited to
    /// <paramref name="maxRange"/> (Chebyshev) and stopping before any wall. Returns the tile
    /// the target if it is in range and visible. Used for the spell to "fire along the Kaeli line".
    /// </summary>
    private (int x, int y) AimAlongLine(int tx, int ty, int maxRange)
    {
        var floor = Floor;
        int x0 = Player.X, y0 = Player.Y;
        int dx = Math.Abs(tx - x0), dy = Math.Abs(ty - y0);
        int sx = x0 < tx ? 1 : -1, sy = y0 < ty ? 1 : -1;
        var err = dx - dy;
        int x = x0, y = y0, lastX = x0, lastY = y0;
        while (x != tx || y != ty)
        {
            var px = x;
            var py = y;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
            if (DiagonalCornerBlocked(floor, px, py, x, y)) break;
            if (floor.IsBlocked(x, y)) break;
            if (Chebyshev(x0, y0, x, y) > maxRange) break;
            lastX = x; lastY = y;
        }
        return (lastX, lastY);
    }

    private static bool DiagonalCornerBlocked(DungeonFloor floor, int fromX, int fromY, int toX, int toY)
    {
        var dx = Math.Sign(toX - fromX);
        var dy = Math.Sign(toY - fromY);
        return dx != 0 && dy != 0
            && (floor.IsBlocked(fromX + dx, fromY)
                || floor.IsBlocked(fromX, fromY + dy));
    }

    private static int Chebyshev(Actor a, Actor b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private static int Chebyshev(int x1, int y1, int x2, int y2) => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    private Actor? MonsterAt(int x, int y) =>
        _monsters.FirstOrDefault(m => m.Floor == _currentFloor && m.Hp > 0 && m.X == x && m.Y == y);

    private Actor? NearestMonster(int maxRange)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (!IsTargetableByPlayer(m, maxRange)) continue;
            var d = Chebyshev(m, Player);
            if (d <= maxRange && d < bestDist) { bestDist = d; best = m; }
        }
        return best;
    }

    private (int dx, int dy) DirDelta(Dir facing, Actor? target)
    {
        if (target is not null)
        {
            var dx = Math.Sign(target.X - Player.X);
            var dy = Math.Sign(target.Y - Player.Y);
            if (dx != 0 || dy != 0) return (dx, dy);
        }
        return facing switch
        {
            Dir.North => (0, -1),
            Dir.East => (1, 0),
            Dir.South => (0, 1),
            _ => (-1, 0)
        };
    }

    private IEnumerable<(int X, int Y)> CircleTiles(int cx, int cy, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                var x = cx + dx;
                var y = cy + dy;
                if (Math.Abs(dx) + Math.Abs(dy) > radius * GameConfig.AoeRoundingFactor) continue; // rounded diamond (MG-04)
                if (!Floor.IsBlocked(x, y)) yield return (x, y);
            }
    }

    private IEnumerable<(int X, int Y)> ConeTiles(int ox, int oy, int dx, int dy, int reach)
    {
        // symmetric frontal fan with the SAME angular spread in any of the 8 directions.
        // forward = projection in target direction; perp = component perpendicular to the axis.
        // The cone is independent of whether the target is diagonal or in a straight line.
        for (var ry = -reach; ry <= reach; ry++)
            for (var rx = -reach; rx <= reach; rx++)
            {
                if (rx == 0 && ry == 0) continue;
                if (Math.Max(Math.Abs(rx), Math.Abs(ry)) > reach) continue;
                var forward = rx * dx + ry * dy;
                if (forward <= 0) continue;                 // tiles ahead only
                var perp = Math.Abs(rx * dy - ry * dx);
                if (perp > forward) continue;               // 45-degree half-spread (90-degree cone)
                var x = ox + rx;
                var y = oy + ry;
                if (!Floor.IsBlocked(x, y)) yield return (x, y);
            }
    }

    private void Emit(string kind, int x, int y, int toX = 0, int toY = 0, int value = 0,
        string text = "", int actorId = 0, bool crit = false) =>
        _events.Add(new EventDto(kind, x, y, toX, toY, value, text, actorId, crit));

    // ---- snapshot ----

    private MapDto BuildMap()
    {
        // G-09: mimic travels as the empty variant (only "cursed" is telegraphed to the client).
        var pois = _pois.Where(p => p.Floor == _currentFloor)
            .Select(p => new PoiDto(p.Id, p.Kind, p.X, p.Y, PoiSpriteId(p.Kind),
                p.Variant == "cursed" ? "cursed" : "", p.Used))
            .ToList();
        // LM-08: helper shared with the admin biome preview (LM-09). Biome resolved at
        // construction (not reread in the tick); ground/wall/decor + rooms + palette are read identically.
        return MapDto.FromFloor(Floor, _biome.Atmosphere, _currentFloor, pois);
    }

    private static ushort PoiSpriteId(string kind) => kind switch
    {
        "chest" => DungeonGenerator.ChestId,
        "sanctuary" => DungeonGenerator.SanctuaryId,
        _ => DungeonGenerator.LadderDownId,
    };

    private List<string> BuildActiveConditions()
    {
        var conditions = _playerConditions.Select(c => c.Type).ToList();
        if (NowMs < _playerSlowUntilMs) conditions.Add("slow");
        return conditions;
    }

    /// <summary>K-04: live signature-passive state (player side) for the HUD.</summary>
    private TraitStateDto BuildTraitState()
    {
        var kind = _trait.Kind;
        var name = _trait.Name;
        switch (kind)
        {
            case "discipline": // Seren: combo on the same target
            {
                if (IsBuffActive(GameConfig.UltStateRampUnlocked))
                {
                    var maxBonus = _trait.Param * _traitMult;
                    return new TraitStateDto(kind, name, GameConfig.SerenPerfectCutEvery, GameConfig.SerenPerfectCutEvery,
                        $"ZENITH (+{Math.Round(maxBonus * 100)}%)");
                }
                var hits = NowMs <= _comboExpireMs ? _comboHits : 0;
                var steps = _trait.Value > 0 ? Math.Ceiling(_trait.Param / _trait.Value) : 0;
                var bonus = Math.Min(hits * _trait.Value, _trait.Param) * _traitMult;
                return new TraitStateDto(kind, name, hits, steps,
                    hits > 0 ? $"x{hits} (+{Math.Round(bonus * 100)}%)" : "-");
            }
            case "static_charge": // Rynna: charge bar
            {
                var marked = _monsters.Count(m => m.Hp > 0 && m.Floor == _currentFloor && ActiveStaticMark(m));
                var detail = $"{Math.Round(_staticCharge)}/{GameConfig.RynnaChargeMax:0}";
                if (marked > 0) detail += $" · {marked} marked";
                return new TraitStateDto(kind, name, Math.Round(_staticCharge), GameConfig.RynnaChargeMax, detail);
            }
            case "contagion": // Rin: burning enemies (+ Infernal Ball burn multiplier while active)
            {
                var burning = _monsters.Count(m => m.Hp > 0 && m.Floor == _currentFloor && IsBurning(m));
                var mult = RinBurnMult();
                var detail = burning > 0 ? $"{burning} burning" : "-";
                if (mult > 0) detail += $" ·×{1 + mult:0.0} burn";
                return new TraitStateDto(kind, name, burning, 0, detail);
            }
            case "prey": // Gaia: hunt ramp against the Prey
            {
                var prey = _monsters.FirstOrDefault(m => m.Id == _preyId && m.Hp > 0);
                if (prey is null) return new TraitStateDto(kind, name, 0, 0, "no prey");
                var ramp = Math.Min((NowMs - _preyStartMs) / 1000.0 * _trait.Value, _trait.Param) * _traitMult;
                return new TraitStateDto(kind, name, Math.Round(ramp * 100), 0, $"+{Math.Round(ramp * 100)}%");
            }
            case "shatter": // Lunara: Frostbite stacks and Shatter haste
            {
                var frosted = _monsters.Count(m => m.Hp > 0 && m.Floor == _currentFloor && ActiveFrostStacks(m) > 0);
                var detail = frosted > 0 ? $"{frosted} frosted" : "-";
                if (NowMs < _traitHasteUntilMs) detail += " · HASTE";
                return new TraitStateDto(kind, name, frosted, 0, detail);
            }
            default: // judgment / decay: live state appears as a per-target mark
                return new TraitStateDto(kind, name, 0, 0, "");
        }
    }

    /// <summary>K-04: per-target mark (stacks/tag) that the renderer draws over the monster.</summary>
    private (int Stacks, string Tag) MonsterTraitState(Actor m)
    {
        switch (_trait.Kind)
        {
            case "judgment":
                var sin = NowMs < m.SinUntilMs ? m.SinStacks : 0;
                return (sin, sin >= GameConfig.EloaSinStacksToJudge ? "judged" : "");
            case "decay":
                return (NowMs < m.DecayUntilMs ? m.DecayStacks : 0, "");
            case "shatter":
                var frost = ActiveFrostStacks(m);
                return (frost, frost > 0 ? "frosted" : "");
            case "prey":
                return (0, m.IsPrey ? "prey" : "");
            case "static_charge":
                return (ActiveStaticMark(m) ? 1 : 0, ActiveStaticMark(m) ? "static" : "");
            default:
                return (0, "");
        }
    }

    private SnapshotDto BuildSnapshot()
    {
        var skillBar = CurrentSkillBar();
        var skills = new List<SkillStateDto>(5);
        for (var i = 0; i < skillBar.Length; i++)
        {
            var skill = skillBar[i];
            var isUlt = i == 4;
            var remaining = isUlt || FreeCast ? 0 : Math.Max(_skillReadyAtMs[i] - NowMs, 0);
            var ready = FreeCast || (isUlt ? _gauge >= GameConfig.UltimateGaugeMax : remaining == 0);
            var cooldownTotal = isUlt
                ? skill.CooldownMs
                : (int)(skill.CooldownMs
                        * Loadout.Mastery.CooldownMult
                        * (1 - EquipmentStats.CooldownReduction));
            skills.Add(new SkillStateDto(
                skill.Id, skill.Name, skill.Element, skill.Description,
                remaining, cooldownTotal, ready));
        }

        var boss = _monsters.FirstOrDefault(m => m.IsBossActor && m.Floor == _currentFloor);

        var player = new PlayerDto(
            Player.Id, Player.X, Player.Y, (int)Player.Facing, Player.Hp, Player.MaxHp,
            Player.FromX, Player.FromY, Player.StepDurMs, Player.StepStartMs,
            new OutfitDto(Loadout.Skin.LookType, Loadout.Skin.Head, Loadout.Skin.Body,
                Loadout.Skin.Legs, Loadout.Skin.Feet,
                // addons come exclusively from the selected skin (0 = none); ascension does not force them
                Loadout.Skin.Addons,
                Loadout.Skin.MountLookType > 0 ? Loadout.Skin.MountLookType : EquipmentStats.MountLookType),
            Player.TargetId, FreeCast ? GameConfig.UltimateGaugeMax : _gauge, skills,
            PlayerClass.Id, PlayerClass.Name,
            CurrentStance.Id, CurrentStance.Name, CurrentStance.Element, PlayerClass.CanToggleStance,
            Math.Max(_autoAttackReadyAtMs - NowMs, 0),
            new AutoHelperSettingsDto(
                _autoHelperTargeting, _autoHelperSkills, _autoHelperUltimate,
                _autoHelperTargetPreference, _autoHelperMovementMode, _defaultAutoHelperMovementMode,
                _autoHelperAutoHeal, _autoHelperHealPct, _autoHelperNavMode, _autoHelperAutoCards),
            _buffsUntilMs.Where(b => NowMs < b.Value).Select(b => b.Key).ToList(),
            BuildActiveConditions(),
            new EquipmentStatsDto(
                EquipmentStats.AttackBonus,
                EquipmentStats.MaxHpBonus,
                EquipmentStats.DamageReduction,
                EquipmentStats.MoveSpeedPercent,
                EquipmentStats.SkillPowerMultiplier,
                EquipmentStats.CritChance,
                EquipmentStats.CritDamage,
                EquipmentStats.CooldownReduction),
            _potionCharges, GameConfig.PotionChargesPerRun, GameConfig.PotionSlotItemId(Tier.Tier),
            Math.Max(_potionReadyAtMs - NowMs, 0), GameConfig.PotionCooldownMs,
            GameConfig.PotionSlotHealFraction(Tier.Tier),
            Math.Max(_dashReadyAtMs - NowMs, 0), GameConfig.DashCooldownMs, NowMs >= _dashReadyAtMs,
            BuildTraitState(), FreeCast);

        var monsters = _monsters
            .Where(m => m.Hp > 0 && m.Floor == _currentFloor)
            .Select(m =>
            {
                var (traitStacks, traitTag) = MonsterTraitState(m);
                return new MonsterDto(
                    m.Id, m.Species!.Name, m.X, m.Y, (int)m.Facing, m.Hp, m.MaxHp,
                    m.FromX, m.FromY, m.StepDurMs, m.StepStartMs,
                    new OutfitDto(m.Species.Outfit.LookType, m.Species.Outfit.Head, m.Species.Outfit.Body,
                        m.Species.Outfit.Legs, m.Species.Outfit.Feet, m.Species.Outfit.Addons),
                    m.IsBossActor, m.IsStunned(NowMs), m.ActiveMark(NowMs),
                    traitStacks, traitTag);
            })
            .ToList();

        var items = _groundItems
            .Where(i => i.Floor == _currentFloor)
            .Select(i => new GroundItemDto(i.Id, i.X, i.Y, i.ItemId, i.Count))
            .ToList();

        var run = new RunStateDto(
            Tier.Tier, Tier.Name, Seed,
            _runLevel, _runXp, GameConfig.XpForRunLevel(_runLevel),
            _gold, _kills,
            _cards.Select(c =>
            {
                var def = Cards.ById[c.Key];
                return new CardStackDto(c.Key, def.Name, c.Value, def.Rarity, def.TagList);
            }).ToList(),
            _pendingOffer,
            _cardRerollsRemaining, _bannedCards.Count, GameConfig.CardRerollGoldCost,
            boss?.Hp, boss?.MaxHp, boss?.Species!.Name,
            boss is { PostureMax: > 0 } ? boss.Posture : null,
            boss is { PostureMax: > 0 } ? boss.PostureMax : null,
            boss is not null && boss.IsStaggered(NowMs),
            boss?.PostureCycle ?? 0,
            NowMs, Ended,
            ItemsLooted.ToList(),
            CurrentNavTargetDto());

        return new SnapshotDto(TickCount, NowMs, _currentFloor, player, monsters, items, _events.ToList(), run);
    }

    public void RequestMapRefresh() => MapDirty = true;
}
