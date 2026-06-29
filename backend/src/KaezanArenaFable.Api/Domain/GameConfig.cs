namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// All simulation/meta constants live here (kaezan-arena ArenaConfig convention).
/// Never hardcode a gameplay value anywhere else.
/// </summary>
public static class GameConfig
{
    // ---- simulation ----
    public const int TickMs = 100;
    // G-01: 250→340 (step 400ms→~294ms/tile) for hunt pacing; keeps margin above MinStepMs.
    public const int PlayerBaseSpeed = 340;
    // G-01: reviewed and kept at 2 — mobs already move at 430–625ms vs player ~294ms, kites well.
    public const int MonsterSpeedMultiplier = 2;
    public const int GroundFriction = 100;
    public const double DiagonalStepFactor = 1.4;
    // G-01: 160→140, more headroom for haste/move-speed without crossing the base step floor.
    public const int MinStepMs = 140;
    public const int MaxStepMs = 1400;
    // G-01: 80→130, smooths turning/stopping (buffer in SetMoveDirection + chain in TickPlayerMovement).
    public const int StepGraceMs = 130;
    // H-07: PULL range (distance at which a mob "sees" you and gives chase). 8→10 (feel feedback 2026-06-29,
    // 9th pass): with the Tibia-style limited camera, mobs should CHASE the player (horde closing in
    // rather than standing idle in a corner). Combined with the helper's straggler-seek (goes after the
    // last lost mob), the room closes without getting stuck. Still requires LoS — no aggro through wall/rock.
    public const int MonsterAggroRange = 10;
    // H-07 (aggro persistence / overlure). The three values below sustain a deliberate train on the return
    // lap (overlure) without turning into infinite glue — every drop is still finite, so a real escape
    // (running to the ladder / sitting behind a wall) still breaks the pile.
    // 12→16: the train tail can fall further behind during the return before the drop timer starts. Consistent
    // with the large rooms of H-01 (RoomMax 16) and the open cave of H-06 (Floor1Size 52) — in open terrain
    // the tail easily exceeds 12 tiles on a lap peak without the player having truly escaped.
    public const int AggroDropRange = 16;
    // 4000→8000: the player runs at ~294ms/tile vs mob ~430–625ms, so on the overlure the lead surges ahead
    // and the tail falls behind. AggroOutOfRangeSinceMs resets as soon as the mob re-enters AggroDropRange
    // (each lap pass), so 8s only fires when the player truly vanishes — plenty of time for the pile to regroup.
    public const int AggroDropOutOfRangeMs = 8000;
    // 6000→8000: rounding a corner on the lap breaks LoS for rear mobs for a few seconds; 8s holds through
    // the loop bend but still releases anyone who truly loses the player behind a wall.
    public const int AggroDropNoLosMs = 8000;
    public const int MonsterWanderIntervalMs = 1600;
    // MG-02: superseded by RoleTuning.BaseAutoAttackMs (auto speed is now per role).
    // Kept as a historical reference for the pre-role baseline; do not use in the tick.
    public const int PlayerAutoAttackMs = 1800;
    public const int AutoHelperTargetRange = 8;
    public const double AutoHelperHealHpFraction = 0.70;
    public const string AutoHelperTargetPreferenceLowestHp = "lowestHp";
    public const string AutoHelperTargetPreferenceNearest = "nearest";
    public const string AutoHelperMovementModeNone = "none";
    public const string AutoHelperMovementModeFollow = "follow";
    public const string AutoHelperMovementModeAvoid = "avoid";
    public const int AutoHelperMovementModeNoneCode = 0;
    public const int AutoHelperMovementModeFollowCode = 1;
    public const int AutoHelperMovementModeAvoidCode = 2;
    public const int AutoHelperFollowDistance = 1;
    public const int AutoHelperAvoidDistance = 2;
    // Skill discipline (feel feedback 2026-06-29): the helper was casting AoE/field on every cooldown
    // even on a lone mob = "skill spam at nothing". Now area skills (area/field/nova/ring/cone) only
    // fire when they would hit at least this many targets — OR when a boss/elite is in the footprint (to
    // avoid wasting damage on a boss). Single/chain/barrage still trigger on 1 target.
    public const int AutoHelperAoeMinTargets = 2;
    // Mobbing (feel feedback 2026-06-29, 3rd pass): instead of planting and poking ("locked mode"),
    // a ranged Kaeli ORBITS the pile when enough mobs are aggroed — walks around the pack center
    // to bunch them up and dumps AoE on top. Dynamic movement, kills in a pile, satisfying to watch.
    public const int HelperGatherThreshold = 3;  // minimum pile to enter orbit-mob mode
    public const int HelperGatherRange = 8;       // range (Chebyshev) that counts as "nearby" for the pack
    public const int HelperMobOrbitRadius = 3;    // target distance from pack center when orbiting (kite)

    // G-10: HELPER automations (gacha autoplay style). Everything deterministic in the tick.
    // Auto-heal: uses the run potion when health drops below this percentage (configurable in UI;
    // the potion respects its own charges/cooldown). 50% is the default; range 10..90.
    public const int AutoHelperHealPctDefault = 50;
    public const int AutoHelperHealPctMin = 10;
    public const int AutoHelperHealPctMax = 90;
    public static int ClampHealPct(int pct) => Math.Clamp(pct, AutoHelperHealPctMin, AutoHelperHealPctMax);
    // Auto-pick card: on the offer, automatically picks the highest rarity (echo>rare>common; tie-break
    // by stable order). Small delay so the offer flashes on screen before resolving. On by default.
    public const int AutoHelperAutoCardsFlag = 16;
    public const int AutoCardPickDelayMs = 700;
    // Auto-loot (helper "cavebot"): walks on its own to the nearest active chest/altar, opens it and
    // repeats; when nothing left to collect, heads to the exit — always fighting on the way. On by
    // default. No "rush/skip" mode by design (not incentivizing skipping the map).
    //   off  = pathing off (normal combat: stand/follow/avoid govern movement)
    //   loot = explores while looting, then exits
    public const string AutoHelperNavOff = "off";
    public const string AutoHelperNavLoot = "loot";
    // Waits ~1s at the start of each floor before walking on its own (screen loads first).
    public const int AutoLootStartDelayMs = 1000;
    // bit for auto-heal in the flag bitmask of the ToggleAutoHelper command (1=target,2=skills,4=ult).
    public const int AutoHelperAutoHealFlag = 8;

    public static string NormalizeAutoHelperNav(string? nav) =>
        nav == AutoHelperNavLoot ? AutoHelperNavLoot : AutoHelperNavOff;
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
    // MVP/difficulty: 0.26→0.40 (feel feedback 2026-06-29, 8th pass): "the health bar doesn't move = bad".
    // The goal is NOT for the player to die (auto-heal at 50% + potion handles it), but for health to
    // visibly swing during the pile/box for tension. Tunable by feel.
    public const double MonsterDamageTuning = 0.40;
    public const int ConditionMaxTicks = 10;
    public const int ConditionDefaultTickMs = 2000;
    public const double ConditionResistCap = 0.85;
    /// <summary>Canary speedChange is an absolute speed delta; divide by this to get a factor.</summary>
    public const double SpeedChangeReference = 600.0;
    public const double SlowFactorFloor = 0.40;
    // ---- Dash / Dodge (Space) — GAME MECHANIC (not a helper bypass) ----
    /// <summary>Dash on Space (market trend): slides the player exactly `DashTiles` tiles in the CARDINAL
    /// movement direction (or facing) — never diagonal — stopping at the first wall or mob (does not pass
    /// through, does not cut wall corners), with brief i-frames (dodge). It is NOT a teleport: the player
    /// slides fast (DashStepMs per tile) leaving a poof trail, reading as motion. The auto-helper uses this
    /// SAME ability (same cooldown/resource) to slip out of a corner while kiting the boss. Deterministic.
    /// 3 tiles (was 4): 4 felt too long, especially for melee. Was on Left Shift, moved to Space because
    /// 5× Shift triggers the Windows Sticky Keys popup.</summary>
    public const int DashTiles = 3;
    public const int DashCooldownMs = 2500;
    public const int DashIFramesMs = 300;
    public const int DashTrailFx = 11; // poof used as a trail along the dash path
    /// <summary>Dash slide duration (ms) PER tile travelled. The dash does not teleport: it slides fast from
    /// origin to destination (like the monster charge) leaving a poof trail — reads as motion, not a blink.
    /// 55ms/tile = ~165ms for 3 tiles (much faster than the normal ~294ms/tile step).</summary>
    public const int DashStepMs = 55;
    // ---- Dash Signatures (role-keyed dash; same cooldown/i-frames for all, but the MOVEMENT and payoff differ) ----
    /// <summary>The Space ability behaves differently per role:
    /// - Knight (Cleave): a short BLINK (instant, DashKnightBlinkTiles) that may pass OVER a mob in front but must
    ///   land on a FREE tile, then an Exori-style nova around the landing. Damage only on landing, never terrain.
    /// - Archer (Sprint): a DashTiles slide that PASSES THROUGH mobs (stops only at walls), lands on the farthest
    ///   free tile, and grants a brief move-speed haste. Pure mobility — no damage.
    /// - Mage (Trail): a DashTiles slide that STOPS before the first wall/mob and seeds a weak spreading scorch
    ///   trail (Contagion) on the tiles crossed. Damage scales off PlayerAttack()*RoleSkillMult(). Tune here.</summary>
    public const string DashStrikeElement = "physical"; // Knight cleave: reaction-inert (no element-combo farming)
    public const int DashKnightBlinkTiles = 2;     // Knight blink is shorter than the 3-tile dash
    public const int DashCleaveRadius = 1;          // Exori-style nova: the 8 tiles around the landing
    public const double DashCleaveAtkScale = 0.70;  // Knight: concentrated burst at the landing
    public const int DashCleaveFx = 35;             // impact burst FX at the landing tile
    public const int DashArcherHasteMs = 1500;      // Archer: brief move-speed buff after the sprint (kite identity)
    public const double DashArcherHasteFactor = 1.5;
    public const double DashTrailFieldAtkScale = 0.18; // Mage: weak DoT per tick (Rin's cast field ~0.40)
    public const int DashTrailFieldFx = 7;             // fire FX (matches Rin's cast fire field)
    public const int DashTrailFieldTickMs = 600;
    public const int DashTrailFieldLifeMs = 1600;      // short — expires fast vs the ~5s cast field
    public const int DashTrailFieldSpreadChance = 20;  // vs 45 for the cast field
    public const int DashTrailFieldGenerations = 1;    // <=1 — barely crawls (cast field crawls 3)
    /// <summary>Taunt (rider "taunt"): how long a taunted enemy abandons kiting and
    /// marches to melee (ignores TargetDistance and low-health flee). Melee skill.</summary>
    public const int MeleeTauntMs = 2500;
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
    /// Direct arena-scale baselines (Health, Damage, Armor, Speed, Experience). Authored bosses use
    /// these values and never receive the legacy BossHpScale multiplier; <see cref="MonsterDamageTuning"/>
    /// is inert for them (they have <c>StatMult=1</c>), so the real mob damage lever is the
    /// <c>Damage</c> column here.
    /// MG-08 (simulator calibration, tools/BalanceSim): the <c>Health</c> column was rescaled to
    /// hit TTK targets in action cycles (common ~3 · elite ~6 · boss ~12) with same-tier gear,
    /// and <c>Damage</c> was lowered for deaths ~0 (mage/archer) — boss never &lt; 8 cycles, no one-shot
    /// from boss/elite. Each number justified by the sweep (docs/balance/mg08_before.csv→mg08_after.csv).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, MonsterStatLine> MonsterStatLines =
        new Dictionary<string, MonsterStatLine>
        {
            ["1:common"] = new(580, 5, 2, 80, 15),
            ["1:elite"] = new(650, 8, 4, 84, 38),
            ["1:boss"] = new(8200, 12, 6, 82, 120),
            ["2:common"] = new(840, 9, 6, 85, 45),
            ["2:elite"] = new(1100, 14, 9, 90, 110),
            ["2:boss"] = new(9700, 22, 13, 88, 350),
            ["3:common"] = new(1200, 15, 12, 90, 120),
            ["3:elite"] = new(1300, 25, 18, 95, 300),
            ["3:boss"] = new(19800, 35, 24, 94, 850),
            ["4:common"] = new(2050, 26, 22, 100, 320),
            ["4:elite"] = new(2600, 40, 32, 105, 800),
            ["4:boss"] = new(33600, 52, 42, 102, 1900),
            ["5:common"] = new(3800, 30, 36, 110, 850),
            ["5:elite"] = new(6000, 45, 52, 116, 2100),
            ["5:boss"] = new(68000, 85, 66, 112, 4800),
        };

    public static readonly MonsterStatPreset[] MonsterStatPresets =
    [
        new("balanced", "Balanced", "No deviation from baseline.", 1, 1, 1, 1),
        new("tank", "Tough", "More health, less damage and mobility.", 1.25, 0.85, 0.90, 0.90),
        new("glass", "Glass Cannon", "More damage, less health.", 0.80, 1.20, 1, 1.05),
        new("swift", "Swift", "Moves and attacks faster, with less impact.", 0.90, 0.90, 1.20, 1.15),
        new("caster", "Caster", "Strong attacks and deliberate pacing.", 0.90, 1.10, 0.95, 0.95),
    ];

    public static readonly MonsterElementProfile[] MonsterElementProfiles =
    [
        new("physical", "Physical", 1, 0, null),
        new("fire", "Fire", 16, 4, "fire"),
        new("ice", "Ice", 44, 29, "freeze"),
        new("energy", "Energy", 12, 5, "energy"),
        new("earth", "Earth", 17, 30, "poison"),
        new("holy", "Holy", 40, 31, "dazzle"),
        new("death", "Death", 18, 11, "curse"),
    ];

    public static readonly MonsterBehaviorProfile[] MonsterBehaviorProfiles =
    [
        new("bruiser", "Bruiser", "Melee pressure with hits of varying weight.", 1, 85,
        [
            new("melee", 1, 0, 0, 0, false, 1700, 100, 0.72, 1.08, false, 0),
            new("spell", 1, 1, 0, 0, false, 3200, 28, 0.48, 0.78, true, 0),
        ]),
        new("skirmisher", "Skirmisher", "Short, quick, less predictable attacks.", 1, 65,
        [
            new("melee", 1, 0, 0, 0, false, 1150, 82, 0.52, 0.88, false, 0),
            new("melee", 1, 0, 0, 0, false, 2300, 45, 0.75, 1.12, true, 0),
        ]),
        new("ranger", "Ranger", "Keeps distance and alternates physical and elemental shots.", 5, 90,
        [
            new("spell", 5, 0, 0, 0, true, 1750, 88, 0.62, 1.02, false, 0),
            new("spell", 5, 0, 0, 0, true, 2850, 52, 0.78, 1.18, true, 0),
        ]),
        new("artillery", "Artillery", "Slow area attacks with high impact.", 6, 95,
        [
            new("spell", 6, 2, 0, 0, true, 2700, 78, 0.78, 1.22, true, 0),
            new("spell", 6, 3, 0, 0, true, 4400, 35, 0.95, 1.38, true, 0),
        ]),
        new("breather", "Breather", "Combines bite and elemental cone.", 2, 85,
        [
            new("melee", 1, 0, 0, 0, false, 1750, 92, 0.60, 0.96, false, 0),
            new("spell", 0, 0, 4, 2, false, 3100, 62, 0.72, 1.18, true, 0),
        ]),
        new("controller", "Controller", "Moderate damage with occasional elemental condition.", 4, 88,
        [
            new("spell", 4, 0, 0, 0, true, 1850, 86, 0.52, 0.86, true, 0),
            new("spell", 4, 1, 0, 0, true, 3600, 42, 0.40, 0.72, true, 0.75),
        ]),
        new("support", "Support", "Light pressure with intermittent self-healing.", 4, 90,
        [
            new("spell", 4, 0, 0, 0, true, 2050, 88, 0.55, 0.90, true, 0),
        ], 0.07, 4200),
        new("juggernaut", "Juggernaut", "Slow, tough, and dangerous when it connects.", 1, 95,
        [
            new("melee", 1, 0, 0, 0, false, 2300, 100, 0.82, 1.28, false, 0),
            new("spell", 1, 2, 0, 0, false, 4200, 32, 0.60, 1.02, true, 0),
        ]),

        // ---- G-08B: new archetypes ----
        // swarm: cheap and fast instances; spawn cost 1 (doubles count per room) = numerical pressure.
        new("swarm", "Swarm", "Cheap and fast minions that press through numbers.", 1, 60,
        [
            new("melee", 1, 0, 0, 0, false, 850, 90, 0.28, 0.50, false, 0),
        ], SpawnCost: 1),
        // summoner: keeps distance and endlessly summons Echolings (reuses TickMonsterSummons).
        new("summoner", "Summoner", "Retreats and endlessly summons echo minions.", 5, 88,
        [
            new("spell", 5, 0, 0, 0, true, 2300, 70, 0.46, 0.82, true, 0),
        ], SummonSpecies: "monster:t1-echoides", SummonCount: 2, SummonChance: 70, SummonIntervalMs: 5200, SummonMax: 4),
        // posture-tank: armor that only takes meaningful damage after Echo Break (PostureScale feeds the bar).
        new("posture-tank", "Posture Sentinel", "Impassive armor: only opens a damage window on Echo Break.", 1, 95,
        [
            new("melee", 1, 0, 0, 0, false, 2100, 100, 0.70, 1.12, false, 0),
            new("spell", 1, 1, 0, 0, false, 4000, 30, 0.55, 0.95, true, 0),
        ], PostureScale: 0.55),
        // charger: retreats and charges in with a brutal dash (MonsterAttackPattern.Kind = "charge").
        new("charger", "Charger", "Circles at range and charges with a brutal rush.", 2, 65,
        [
            new("melee", 1, 0, 0, 0, false, 1600, 80, 0.52, 0.84, false, 0),
            new("charge", 6, 0, 0, 0, false, 4200, 85, 1.05, 1.55, false, 0),
        ]),
        // bomber/suicide: runs to the target and explodes in an area upon dying near it.
        new("bomber", "Bomber", "Rushes the target and detonates in an echo explosion.", 1, 45,
        [
            new("melee", 1, 0, 0, 0, false, 1200, 70, 0.22, 0.42, false, 0),
        ], ExplodeRadius: 1, ExplodeDamageScale: 1.7),
        // shielder: shields the most injured nearby ally → forces the helper to focus it first.
        new("shielder", "Echo Bearer", "Raises barriers on allies and forces focus onto itself.", 5, 90,
        [
            new("spell", 5, 0, 0, 0, true, 2600, 55, 0.40, 0.72, true, 0),
        ], ShieldRadius: 4, ShieldFraction: 0.35, ShieldIntervalMs: 3500),
    ];

    /// <summary>G-08B: O(1) lookup by id for the tick to read behavior fields (summon/posture/explode/shield).</summary>
    public static readonly IReadOnlyDictionary<string, MonsterBehaviorProfile> MonsterBehaviorById =
        MonsterBehaviorProfiles.ToDictionary(b => b.Id, StringComparer.OrdinalIgnoreCase);

    public static MonsterBehaviorProfile? BehaviorProfile(string id) =>
        MonsterBehaviorById.GetValueOrDefault(id);

    /// <summary>Per-tick FX by condition type (tibia CONST_ME ids).</summary>
    public static readonly IReadOnlyDictionary<string, int> ConditionTickFx = new Dictionary<string, int>
    {
        ["poison"] = 17, ["fire"] = 16, ["energy"] = 12, ["bleed"] = 1,
        ["curse"] = 18, ["freeze"] = 44, ["drown"] = 54, ["dazzle"] = 40,
    };

    /// <summary>Display labels for run-end condition reason ("killed by poison").</summary>
    public static readonly IReadOnlyDictionary<string, string> ConditionLabel = new Dictionary<string, string>
    {
        ["poison"] = "poison", ["fire"] = "burn", ["energy"] = "shock", ["bleed"] = "bleed",
        ["curse"] = "curse", ["freeze"] = "freeze", ["drown"] = "drown", ["dazzle"] = "dazzle",
    };

    // ---- run / leveling ----
    public const int MaxRunLevel = 30;
    public static long XpForRunLevel(int level) => (long)(40 * Math.Pow(level, 1.65));
    public const int CardChoicesPerOffer = 3;
    public const int MaxCardStacks = 3;
    public const int CardOfferTimeoutMs = 20000;
    public const int CardRerollsPerRun = 2;

    // ---- G-04: card framework (rarity + mechanic) ----
    /// <summary>Echo defines the run; does not stack like status effects.</summary>
    public const int EchoMaxStacks = 1;
    public static int MaxStacksForRarity(string rarity) =>
        rarity == Cards.Echo ? EchoMaxStacks : MaxCardStacks;

    // ---- G-06: cadence (fixed beats) ----
    // Level-up gives a small automatic bonus (dopamine drip, no screen opened); heavy choices
    // land on anticipatable beats: defeating an elite, clearing a floor, and the Echo Sanctuary room.
    // The cap below keeps ~6-9 choices per run, with rarity scaling with progress.
    /// <summary>Card choice cap per run (target ~6-9). Beat throttle.</summary>
    public const int MaxCardChoicesPerRun = 9;
    /// <summary>Echo Sanctuary rooms per floor (guaranteed beat, shown on minimap).</summary>
    public const int SanctuariesPerFloor = 1;
    /// <summary>Clearing a floor (going down the stairs) also grants a choice beat.</summary>
    public const bool OfferChoiceOnFloorClear = true;

    /// <summary>
    /// Offer sampling weight by rarity, scaled by run progress in [0,1]
    /// (fraction of choices already granted). Early run favors common/rare (builds the engine); late run
    /// favors rare/echo (defines the run). Deterministic: only interpolates fixed weights.
    /// </summary>
    public static double CardRarityWeight(string rarity, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        return rarity switch
        {
            Cards.Common => Lerp(100, 22, progress),
            Cards.Rare => Lerp(34, 52, progress),
            Cards.Echo => Lerp(7, 46, progress),
            _ => Lerp(100, 22, progress),
        };
    }

    // Overloaded Echo (echo_surge): ult gauge per direct hit, per stack.
    public const double CardEchoSurgeGaugePerHit = 4;
    // Double Strike (double_strike): every N hits, an extra blow (fraction of attack, per stack).
    public const int CardDoubleStrikeEvery = 3;
    public const double CardDoubleStrikeDamageMult = 0.60;
    // Detonate (detonate): condition expires → area burst (fraction of attack, per stack).
    public const int CardDetonateRadius = 1;
    public const double CardDetonateDamageMult = 0.80;
    // Nightmare Harvest (harvest, Velvet): spectre that pulses damage when killing under Decay.
    public const int CardHarvestMaxSpectres = 5;
    public const double CardHarvestSpectreDamageMult = 0.50;
    public const int CardHarvestSpectreRadius = 1;
    public const int CardHarvestSpectrePulseMs = 1000;
    public const int CardHarvestSpectreDurationMs = 6000;
    public const int CardHarvestSpectreFx = 18; // death area fx
    public const int UltimateGaugeMax = 100;
    public const int GaugeFillPerKill = 8;
    public const double GaugeFillPerDamageTaken = 0.5;

    // ---- dungeon generation ----
    // H-01 (G2): larger, fewer rooms — space to stack a hunt (overlure/box), like in
    // Tibia (few large caves instead of many small rooms). Floors grew to fit large rooms
    // without placement failing.
    // SINGLE ARENA (feel feedback 2026-06-29, 4th pass): "multiple rooms on the same map is not
    // fun" + the "clear everything then walk to the chest" flow felt artificial. Now each floor is ONE
    // large open room (RoomsFloorN=1) where the Kaeli mobs/lures, clears, and picks up the chest/stairs.
    // The generator fills the floor with a single arena; RoomMin/Max remain as reference (single-arena
    // forces the size). Smaller floor = arena sized to the floor, no dead corridor.
    // 34→26 (feedback 2026-06-29, 8th pass): "room 1 still feels too large". Smaller arena = denser,
    // closer horde, less dead walking. Organic erosion (ErodeArena) still trims the edge.
    public const int Floor1Size = 26;
    // Boss arena smaller than the horde arena (feedback 2026-06-29): focused chamber, boss at the back + escort.
    public const int Floor2Size = 22;
    public const int RoomMin = 12;
    public const int RoomMax = 20;
    public const int RoomsFloor1 = 1;
    public const int RoomsFloor2 = 1;
    /// <summary>Chebyshev radius around the entrance where mobs avoid spawning — so the Kaeli doesn't start
    /// the run buried in the middle of the arena horde.</summary>
    public const int SpawnEntrySafeRadius = 6;
    /// <summary>1-tile box alcove (H-03): choke for the "mob/run" tactic. Disabled in favor of open-map
    /// direction — the game is now about orbiting the pile in an open arena, not retreating to a bottleneck.
    /// (static readonly, not const: the generator checks at runtime, avoids CS0162 unreachable code.)</summary>
    public static readonly bool EnableBoxNiches = false;
    /// <summary>H-01: room placement attempts (large rooms fill more of the floor → more overlap rejections,
    /// so the attempt margin rises to guarantee the target count).</summary>
    public const int RoomPlacementAttempts = 300;
    public const int ChestsPerFloor = 2;
    // Dynamic loot (feel feedback 2026-06-29, 8th pass): instead of fixed chests scattered around (the Kaeli
    // would go "straight to them", which felt odd), a chest DROPS on the corpse every N kills — spawns mid-fight
    // and the Kaeli detours to grab it while luring. Never a mimic (always a benefit; can be cursed = ambush).
    public const int ChestDropEveryKills = 6;
    /// <summary>Single organic arena: the entire room is seeded with noise and smoothed by CA (irregular
    /// cave, not a square). 0.42 leaves the center mostly open with rock clusters/pillars at the edges;
    /// a central core is forced open + flood-fill ensures the stage is connected.</summary>
    public const double ArenaFillProb = 0.42;
    // Density: with large, open rooms, the budget goes back to 16 (the area factor fills the arena).
    // Target: ~10-16 mobs per large room = a satisfying pile to orbit and melt with AoE, without becoming a wall.
    public const int SpawnBudgetBase = 16;
    public const double SpawnBudgetTierGrowth = 0.55;
    // H-01: spawn budget scales with room area (room.W*room.H / baseline), clamped. With large
    // rooms (H-01) the cap rose so the room doesn't spawn empty — a large cave needs to fit the pile.
    /// <summary>Room area (tiles) worth factor 1.0 in spawn budget. 160 with large rooms
    /// (a ~13×13 room is worth 1.0); prevents every arena from hitting the clamp ceiling and becoming a mob wall.</summary>
    public const double SpawnRoomAreaBaseline = 160.0;
    /// <summary>Floor of the area factor (small rooms don't become empty).</summary>
    public const double SpawnBudgetSizeClampMin = 0.6;
    /// <summary>Ceiling of the area factor. 2.6: the single arena is the entire floor, so the budget rises to
    /// fill it with mobs (a satisfying horde to mob), without letting the guard of 50 burst all at once.</summary>
    public const double SpawnBudgetSizeClampMax = 2.6;

    // ---- H-02 (B1): organic rooms (cellular automaton) ----
    // After carving the rectangle, erode the border with a deterministic CA pass (classic 4-5 rule)
    // so the room reads as an irregular blob instead of a box. Only the border ring is seeded with rock;
    // the interior stays open, and a flood-fill from the center ensures the room remains a single connected
    // component (corridors link center↔center). All using the run Rng → deterministic.
    /// <summary>Rooms with a shorter side below this are not eroded (too small — erosion would strangle them).</summary>
    public const int OrganicRoomMinSize = 7;
    /// <summary>Width (tiles) of the border ring where rock is seeded; the room interior remains intact/open.</summary>
    public const int OrganicSeedBand = 3;
    /// <summary>Probability of a border ring cell spawning as rock before smoothing.</summary>
    public const double OrganicFillProb = 0.45;
    /// <summary>Cellular automaton smoothing iterations (more = smoother/rounder outline).</summary>
    public const int OrganicCaIterations = 4;
    /// <summary>4-5 rule: cell becomes rock if it has ≥ this many rock 8-neighbors (outside the rectangle counts as rock).</summary>
    public const int OrganicWallThreshold = 5;
    /// <summary>4-5 rule: cell becomes floor if it has ≤ this many rock 8-neighbors.</summary>
    public const int OrganicFloorThreshold = 3;

    // ---- corridors: minimum width 2 (never 1 sqm) ----
    /// <summary>Minimum corridor width (tiles). 3 (was 2): wide passage for the Kaeli to run and the train
    /// to follow side by side — a width-2 corridor still pinched mob movement.</summary>
    public const int CorridorWidthMin = 3;
    /// <summary>Maximum corridor width (tiles). 4 (was 3): almost a corridor-room, map reads as open.</summary>
    public const int CorridorWidthMax = 4;

    // ---- H-03 (G3): box niche (alcove with a 1-tile mouth) ----
    // In each combat room, carve a closed alcove with a single 1-tile entrance. The corridor
    // stays wide (2–3, closed decision); the ONLY 1-tile choke is the alcove mouth. Tactic: lure the
    // pile in the open room, retreat to the alcove and tank mobs in single file through the mouth. The
    // enclosure (back + sides + mouth wall) is built entirely INSIDE the room rectangle, flush against
    // one wall — never depends on external rock or borders a corridor outside the rect. Each position is
    // validated by BFS: only commits if the interior is reachable through the mouth AND no previously
    // reachable cell is orphaned by the new walls. Deterministic (only the Rng chooses the wall; center-out slide).
    /// <summary>Open interior side of the alcove (tiles). Footprint = this + 2 (1-tile wall ring).</summary>
    public const int BoxInteriorSize = 3;
    /// <summary>Alcove mouth width (tiles). Fixed at 1 — it is the "box close" choke (closed decision).</summary>
    public const int BoxMouthWidth = 1;
    /// <summary>Combat rooms with a shorter side below this do not receive an alcove (not enough room to fight).</summary>
    public const int BoxRoomMinSize = 8;

    // ---- LM-07: generation quality (clustered decor instead of dotted) ----
    /// <summary>Chebyshev radius of ambient prop clusters (decor) within a room.</summary>
    public const int DecorClusterRadius = 1;
    /// <summary>Radius of accent puddles (e.g. lava) — larger than decor so they read as environment.</summary>
    public const int AccentClusterRadius = 2;
    /// <summary>Chance falloff per ring from the cluster center (0 = solid, 1 = center only).</summary>
    public const double ClusterFalloff = 0.45;
    /// <summary>Scales the number of clusters per room (area × biome chance × this). Keeps decor sparse.</summary>
    public const double DecorDensityScale = 0.5;

    // ---- G-07: room types (graph + risk/reward fork) ----
    /// <summary>Elite room (risk detour) forces elites up to this cap; the rest of the budget becomes common.</summary>
    public const int EliteRoomMaxElites = 2;
    /// <summary>Event/hazard room: spawn budget expanded by this factor (swarm = the hazard).</summary>
    public const double HazardBudgetMult = 1.35;
    /// <summary>Miniboss only appears on floors with at least this many rooms (avoids crowding small maps).</summary>
    public const int MiniBossMinRooms = 6;
    /// <summary>Miniboss HP = elite HP × this factor (mini-climax before the boss).</summary>
    public const double MiniBossHpScale = 2.4;
    /// <summary>Common mobs escorting the miniboss.</summary>
    public const int MiniBossEscort = 2;

    // ---- MG-02: roles (Knight · Mage · Archer) — primary identity axis ----
    // Each role drives auto vs skill damage, auto speed, range, and AOE size. These are the
    // SEED values (refined by MG-06/MG-07 via simulator); in MG-05 they become editable in admin.
    // Target orders: auto archer/knight > mage; skill mage > archer > knight; spd archer > knight > mage;
    // range archer > mage > knight; aoe mage > knight > archer.
    public static readonly IReadOnlyDictionary<KaeliRole, RoleTuning> Roles =
        new Dictionary<KaeliRole, RoleTuning>
        {
            //                       AutoDmg SkillDmg AutoMs Range Aoe
            [KaeliRole.Mage]   = new(0.75,   1.15,    2000,  4,    1.00),
            [KaeliRole.Archer] = new(1.15,   0.95,    1400,  5,    0.65),
            [KaeliRole.Knight] = new(1.05,   0.80,    1700,  1,    0.80),
        };

    // ---- MG-04: AOE resize ----
    // Footprint of area shapes. Previously CircleTiles/RingTiles capped at radius*1.5 (almost a
    // square); 1.25 gives a more honest diamond. Applied inside helpers, without touching call sites.
    public const double AoeRoundingFactor = 1.25;
    // Maximum radius of an ultimate BEFORE the role's AoeScale: the ult still "bursts" (floor of 2
    // guaranteed in GameWorld.SkillRadius), but none become full-screen. Mage ult lands at 3, archer/knight ~2.
    public const int UltimateRadiusCap = 3;

    // ---- spreading terrain (spreading fields — Rin's Contagion) ----
    // A GroundField with a spread budget ignites, each tick, one free neighboring tile (deterministic
    // choice in the run Rng), creating a fire that creeps across the map = visual chaos + an area that
    // grows on its own. The per-floor cap below limits growth so the simulation never explodes.
    /// <summary>Cap on simultaneous field tiles per floor (cuts propagation when reached).</summary>
    public const int FieldMaxTilesPerFloor = 80;
    /// <summary>Lifetime (ms) of a child tile lit by propagation. Short: the fire front "walks"
    /// instead of covering everything at once (the tail fades as the edge advances).</summary>
    public const int FieldSpreadChildLifeMs = 2600;

    // ---- player damage ----
    /// <summary>Global player damage multiplier (autos + skills). MVP/difficulty: we were dealing too little damage.</summary>
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
    public const double EquipmentSkillPowerPerPoint = 0.02;
    public const double EquipmentCritChanceCap = 0.50;
    public const double EquipmentCritDamageCap = 2.00;
    public const double EquipmentLifeStealChanceCap = 1.00;
    public const double EquipmentLifeStealAmountCap = 0.50;
    public const double EquipmentCooldownReductionCap = 0.40;
    public const double EquipmentMoveSpeedCap = 0.50;
    public const double EquipmentResistanceCap = 0.75;
    public const double EquipmentWeaponElementMatchDamageBonus = 0.10;
    public const int MountHpPerSpeed = 2;
    public const double MountMoveSpeedPercentPerSpeed = 0.005;
    public const double BossMountDropChance = 0.20;
    public const int AuthoredItemIdBase = 900000;
    public const int ItemMaxAttack = 500;
    public const int ItemMaxArmor = 200;
    public const int ItemMaxDefense = 300;
    public const int ItemMaxElementDamage = 500;
    public const int ItemMaxSkillPower = 50;
    public const int ItemMaxMountSpeed = 100;
    public const int ItemMaxSalePrice = 1_000_000_000;
    public const int AdminItemGrantMax = 99;
    public const string AuthoredItemTagNormal = "normal";
    public const string AuthoredItemTagRelic = "relic";
    public const double AuthoredItemRelicMultiplierDefault = 1.25;
    public const double AuthoredItemRelicMultiplierMin = 1.05;
    public const double AuthoredItemRelicMultiplierMax = 1.60;
    public static readonly int[] AuthoredItemSetTiers = [0, 1, 2, 3, 4, 5];
    public static readonly ItemBalanceGrade[] AuthoredItemBalanceGrades =
    [
        new("low", "Low"),
        new("moderate", "Moderate"),
        new("high", "High"),
    ];
    public static readonly ItemBalanceRange[] AuthoredItemBalanceRanges =
    [
        new("attack", 0, 8, 14, 15, 22, 23, 30),
        new("attack", 1, 8, 14, 15, 22, 23, 30),
        new("attack", 2, 22, 34, 35, 48, 49, 62),
        new("attack", 3, 48, 68, 69, 92, 93, 118),
        new("attack", 4, 90, 120, 121, 155, 156, 190),
        new("attack", 5, 150, 190, 191, 235, 236, 290),

        new("armor", 0, 2, 4, 5, 7, 8, 10),
        new("armor", 1, 2, 4, 5, 7, 8, 10),
        new("armor", 2, 7, 11, 12, 16, 17, 22),
        new("armor", 3, 16, 24, 25, 34, 35, 45),
        new("armor", 4, 32, 43, 44, 58, 59, 74),
        new("armor", 5, 52, 68, 69, 86, 87, 105),

        new("defense", 0, 4, 8, 9, 13, 14, 18),
        new("defense", 1, 4, 8, 9, 13, 14, 18),
        new("defense", 2, 14, 22, 23, 31, 32, 42),
        new("defense", 3, 34, 48, 49, 64, 65, 82),
        new("defense", 4, 58, 78, 79, 102, 103, 128),
        new("defense", 5, 88, 112, 113, 142, 143, 176),

        new("mountSpeed", 0, 6, 10, 11, 16, 17, 22),
        new("mountSpeed", 1, 6, 10, 11, 16, 17, 22),
        new("mountSpeed", 2, 14, 20, 21, 28, 29, 36),
        new("mountSpeed", 3, 24, 32, 33, 42, 43, 52),
        new("mountSpeed", 4, 36, 46, 47, 58, 59, 70),
        new("mountSpeed", 5, 48, 60, 61, 74, 75, 90),

        new("elementDamage", 0, 3, 5, 6, 8, 9, 12),
        new("elementDamage", 1, 3, 5, 6, 8, 9, 12),
        new("elementDamage", 2, 8, 12, 13, 18, 19, 25),
        new("elementDamage", 3, 18, 26, 27, 36, 37, 48),
        new("elementDamage", 4, 32, 43, 44, 56, 57, 72),
        new("elementDamage", 5, 50, 66, 67, 84, 85, 105),

        new("skillPower", 0, 1, 2, 3, 4, 5, 6),
        new("skillPower", 1, 1, 2, 3, 4, 5, 6),
        new("skillPower", 2, 4, 6, 7, 9, 10, 12),
        new("skillPower", 3, 7, 10, 11, 14, 15, 18),
        new("skillPower", 4, 11, 15, 16, 20, 21, 25),
        new("skillPower", 5, 16, 21, 22, 28, 29, 35),

        new("critChance", 0, 0.02, 0.04, 0.05, 0.07, 0.08, 0.10),
        new("critChance", 1, 0.02, 0.04, 0.05, 0.07, 0.08, 0.10),
        new("critChance", 2, 0.05, 0.07, 0.08, 0.11, 0.12, 0.15),
        new("critChance", 3, 0.08, 0.11, 0.12, 0.16, 0.17, 0.21),
        new("critChance", 4, 0.10, 0.14, 0.15, 0.20, 0.21, 0.26),
        new("critChance", 5, 0.12, 0.16, 0.17, 0.23, 0.24, 0.30),

        new("critDamage", 0, 0.08, 0.12, 0.13, 0.18, 0.19, 0.25),
        new("critDamage", 1, 0.08, 0.12, 0.13, 0.18, 0.19, 0.25),
        new("critDamage", 2, 0.16, 0.24, 0.25, 0.34, 0.35, 0.46),
        new("critDamage", 3, 0.28, 0.40, 0.41, 0.54, 0.55, 0.70),
        new("critDamage", 4, 0.40, 0.54, 0.55, 0.72, 0.73, 0.92),
        new("critDamage", 5, 0.52, 0.68, 0.69, 0.90, 0.91, 1.15),

        new("lifeStealChance", 0, 0.03, 0.05, 0.06, 0.08, 0.09, 0.12),
        new("lifeStealChance", 1, 0.03, 0.05, 0.06, 0.08, 0.09, 0.12),
        new("lifeStealChance", 2, 0.06, 0.09, 0.10, 0.13, 0.14, 0.18),
        new("lifeStealChance", 3, 0.09, 0.13, 0.14, 0.19, 0.20, 0.26),
        new("lifeStealChance", 4, 0.12, 0.17, 0.18, 0.24, 0.25, 0.33),
        new("lifeStealChance", 5, 0.16, 0.22, 0.23, 0.30, 0.31, 0.40),

        new("lifeStealAmount", 0, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07),
        new("lifeStealAmount", 1, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07),
        new("lifeStealAmount", 2, 0.04, 0.06, 0.07, 0.09, 0.10, 0.12),
        new("lifeStealAmount", 3, 0.06, 0.09, 0.10, 0.13, 0.14, 0.17),
        new("lifeStealAmount", 4, 0.08, 0.12, 0.13, 0.17, 0.18, 0.22),
        new("lifeStealAmount", 5, 0.10, 0.15, 0.16, 0.21, 0.22, 0.28),

        new("cooldownReduction", 0, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07),
        new("cooldownReduction", 1, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07),
        new("cooldownReduction", 2, 0.04, 0.06, 0.07, 0.09, 0.10, 0.12),
        new("cooldownReduction", 3, 0.06, 0.09, 0.10, 0.13, 0.14, 0.17),
        new("cooldownReduction", 4, 0.08, 0.12, 0.13, 0.17, 0.18, 0.22),
        new("cooldownReduction", 5, 0.10, 0.14, 0.15, 0.19, 0.20, 0.24),

        new("moveSpeedPercent", 0, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07),
        new("moveSpeedPercent", 1, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07),
        new("moveSpeedPercent", 2, 0.04, 0.06, 0.07, 0.09, 0.10, 0.12),
        new("moveSpeedPercent", 3, 0.06, 0.09, 0.10, 0.13, 0.14, 0.17),
        new("moveSpeedPercent", 4, 0.08, 0.12, 0.13, 0.17, 0.18, 0.22),
        new("moveSpeedPercent", 5, 0.10, 0.14, 0.15, 0.19, 0.20, 0.24),

        new("resistance", 0, 0.03, 0.05, 0.06, 0.08, 0.09, 0.10),
        new("resistance", 1, 0.03, 0.05, 0.06, 0.08, 0.09, 0.10),
        new("resistance", 2, 0.05, 0.08, 0.09, 0.12, 0.13, 0.16),
        new("resistance", 3, 0.07, 0.11, 0.12, 0.16, 0.17, 0.22),
        new("resistance", 4, 0.10, 0.14, 0.15, 0.20, 0.21, 0.27),
        new("resistance", 5, 0.12, 0.16, 0.17, 0.22, 0.23, 0.30),
    ];

    public static double AuthoredItemRecommendedValue(int tier, string stat)
    {
        var t = Math.Clamp(tier, 0, 5);
        var range = AuthoredItemBalanceRanges.FirstOrDefault(entry =>
            entry.Tier == t && entry.Stat.Equals(stat, StringComparison.OrdinalIgnoreCase));
        return range is null ? 0 : (range.ModerateMin + range.ModerateMax) / 2;
    }

    public static int AuthoredItemRecommendedInt(int tier, string stat) =>
        (int)Math.Round(AuthoredItemRecommendedValue(tier, stat), MidpointRounding.AwayFromZero);

    public static int AuthoredItemSalePrice(int tier)
    {
        var t = Math.Clamp(tier, 0, 5);
        return t <= 0 ? 80 : 80 * t * t;
    }

    public static int MountItemId(int lookType) => -lookType;
    public static readonly IReadOnlyDictionary<int, int> TierMountLookTypes = new Dictionary<int, int>
    {
        [1] = 368, // Widow Queen
        [2] = 370, // War Bear
        [3] = 390, // Crystal Wolf
        [4] = 506, // Dragonling
        [5] = 626, // Flamesteed
    };

    // ---- sustain baseline (bridge until sets: every Kaeli has a minimum, without depending on a card) ----
    /// <summary>Passive regen per second as a fraction of max health, even without the regen card.</summary>
    public const double BaselineRegenPctPerSec = 0.006;
    /// <summary>+regen per run-level (rewards those who level up within the run).</summary>
    public const double BaselineRegenPctPerRunLevel = 0.0006;
    /// <summary>Minimum life leech: fraction of damage dealt returned as health, even without a card.</summary>
    public const double BaselineLifesteal = 0.02;

    // ---- functional drops (consumables heal on pickup; junk becomes gold) ----
    /// <summary>Food heals this fraction of max health when picked up.</summary>
    public const double FoodHealPct = 0.05;
    /// <summary>Health potions heal by a fraction of max health — stronger potion, more healing.</summary>
    public const double PotionHealBasic = 0.15;
    public const double PotionHealStrong = 0.25;
    public const double PotionHealGreat = 0.40;
    /// <summary>Gold coin sprite (Tibia) used in the loot-flying-to-player animation.</summary>
    public const int GoldCoinItemId = 3031;

    // ---- Kaezan authored loot ----
    public const double KaezanCommonItemDropChance = 0.08;
    public const double KaezanEliteItemDropChance = 0.24;
    public const double KaezanCommonClassDropWeight = 0.65;
    public const double KaezanEliteClassDropWeight = 0.70;
    public const double KaezanBossClassDropWeight = 0.80;
    public const double KaezanChestClassDropWeight = 0.60;
    public const double KaezanBossRelicDropChance = 0.30;
    public const int KaezanChestItemDrops = 2;

    // ---- G-09: chest = Echo altar / run shop + cursed + mimics + gear material ----
    /// <summary>Chance of a chest actually being a mimic (corrupted elite). Surprise — hidden from the client.</summary>
    public const double ChestMimicChance = 0.12;
    /// <summary>Chance of a non-mimic chest being cursed: strong Echo + ambush/curse. Telegraphed.</summary>
    public const double ChestCursedChance = 0.22;
    /// <summary>Mimic HP = elite HP × this factor (corrupted Echo chest = mini-climax).</summary>
    public const double MimicHpScale = 2.0;
    /// <summary>Common mobs that ambush when opening a cursed chest (the curse itself).</summary>
    public const int CursedChestAmbush = 3;
    /// <summary>Curse on the player upon opening a cursed chest: temporary slow.</summary>
    public const int CursedChestSlowMs = 6000;
    public const double CursedChestSlowFactor = 0.6;
    /// <summary>Gold cost of a reroll when the free ones run out (the run altar "shop").</summary>
    public const int CardRerollGoldCost = 150;
    /// <summary>Blessed offer (cursed chest): weighted like the end of the run (favors rare/echo).</summary>
    public const double BlessedOfferProgress = 1.0;

    /// <summary>Echo Material: synthetic ids (1 per tier) that flow through the account inventory to
    /// the equipment screen. Outside the item catalog (not equippable, not sellable).</summary>
    public const int GearMaterialItemIdBase = 950000;
    public static int GearMaterialItemId(int tier) => GearMaterialItemIdBase + Math.Clamp(tier, 1, 5);
    public static bool IsGearMaterial(int itemId) =>
        itemId > GearMaterialItemIdBase && itemId <= GearMaterialItemIdBase + 5;
    public static string GearMaterialName(int tier) => $"Echo Shard · T{Math.Clamp(tier, 1, 5)}";
    /// <summary>Chance of a common chest dropping 1 Echo material; cursed/mimic guarantee N.</summary>
    public const double ChestMaterialDropChance = 0.45;
    public const int CursedChestMaterialDrops = 2;
    /// <summary>Sprite (precious gem) used in the material-flying-to-player animation.</summary>
    public const int GearMaterialFlySpriteId = 2478;

    public static (int Min, int Max) KaezanDropGoldRange(int tier, string rank)
    {
        var t = Math.Clamp(tier, 1, 5);
        return rank switch
        {
            "boss" => (75 * t, 130 * t),
            "elite" => (14 * t, 28 * t),
            _ => (4 * t, 10 * t)
        };
    }

    // ---- slot potion (run resource, independent of mob loot) ----
    /// <summary>Potion charges the player starts each run with (slot 5).</summary>
    public const int PotionChargesPerRun = 2;
    /// <summary>Cooldown between uses of the slot potion.</summary>
    public const int PotionCooldownMs = 1500;

    /// <summary>Healing fraction of the slot potion based on run tier (scales like equipment).</summary>
    public static double PotionSlotHealFraction(int tier) => tier switch
    {
        <= 2 => PotionHealBasic,
        <= 4 => PotionHealStrong,
        _ => PotionHealGreat,
    };

    /// <summary>Sprite/icon of the slot potion based on tier — matches the healing amount.</summary>
    public static int PotionSlotItemId(int tier) => tier switch
    {
        <= 2 => 266, // health potion
        <= 4 => 236, // strong health potion
        _ => 239,    // great health potion
    };
    /// <summary>Words that mark an item as food (resolved to ids in GameData).</summary>
    public static readonly string[] FoodNameWords =
    [
        "meat", "ham", "cheese", "fish", "cookie", "cherry", "corncob", "mushroom", "worm",
        "egg", "bread", "carrot", "apple", "banana", "grape", "melon", "salmon", "mango",
        "pear", "tomato", "strawberry", "blueberry", "bun", "candy", "roll", "rye"
    ];

    // ---- death / rewards ----
    public const double DefeatGoldKeptFraction = 0.5;
    // MVP/test: inflated per-run reward. Production: Base 120, PerTier 40.
    public const int VictoryKaerosBase = 400;
    public const int VictoryKaerosPerTier = 120;
    public const long AccountXpPerVictory = 60;
    public const long AccountXpPerDefeat = 20;
    public const long AccountXpPerRunLevel = 6;
    public const int RunReconnectGraceMs = 60000;
    public const int AutoRepeatDelayMs = 2500;
    public const int FarmRunMinCount = 1;
    public const int FarmRunMaxCount = 5;
    public const int DungeonEnergyPerRun = 60;
    public const int DungeonEnergyCap = 300;
    public const int OfflineProgressMinMinutes = 5;
    public const int OfflineProgressCapMinutes = 8 * 60;
    public const int OfflineProgressGoldPerHour = 180;
    public const int OfflineProgressAccountXpPerHour = 35;
    public const double OfflineProgressTierBonus = 0.25;
    public static double OfflineProgressTierMultiplier(int tier) =>
        1 + (Math.Clamp(tier, 1, 5) - 1) * OfflineProgressTierBonus;
    public static long XpForAccountLevel(int level) => (long)(80 * Math.Pow(level, 1.7));
    public const int MaxAccountLevel = 100;

    // ---- dungeon tiers (mob rooms + boss). Account level gates. ----
    public static readonly DungeonTier[] Tiers =
    [
        new(1, "Echoing Burrow", "Worm-infested caves beneath Mount Sternum.",
            ["Rat", "Cave Rat", "Wolf", "Winter Wolf", "Bug", "Spider", "Snake", "Troll"],
            ["Rotworm", "Scorpion", "Centipede", "Troll Champion", "Carrion Worm", "Poison Spider", "Slime"],
            "Rotworm Queen", 1, 1.0),
        new(2, "Uruk Stronghold", "An orc bastion seized by greed.",
            ["Orc", "Orc Spearman", "Goblin", "Goblin Scavenger", "Dwarf", "War Wolf"],
            ["Orc Warrior", "Orc Shaman", "Dwarf Soldier", "Orc Rider", "Orc Berserker"],
            "Orc Warlord", 2, 1.35),
        new(3, "Dark Crypt", "Catacombs where the dead do not rest.",
            ["Skeleton", "Ghoul", "Ghost", "Mummy", "Bonelord"],
            ["Crypt Shambler", "Demon Skeleton", "Witch", "Vampire", "Necromancer", "Banshee"],
            "Black Knight", 3, 1.8),
        new(4, "Scaled Den", "Dragon nests in the volcanic depths.",
            ["Minotaur", "Minotaur Archer", "Minotaur Mage", "Minotaur Guard", "Fire Elemental", "Dragon Hatchling"],
            ["Cyclops", "Earth Elemental", "Dragon", "Dragon Lord Hatchling", "Frost Dragon Hatchling"],
            "Dragon Lord", 4, 2.4),
        new(5, "Echoing Abyss", "Where the echoes of the abyss take shape.",
            ["Cyclops", "Fire Devil", "Dragon", "Dragon Lord Hatchling", "Frost Dragon Hatchling"],
            ["Giant Spider", "Dragon Lord", "Frost Dragon", "Hydra", "Hellfire Fighter", "Behemoth", "Hellhound", "Dark Torturer", "Juggernaut"],
            "Demon", 5, 3.2),
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
    // MVP/test: generous economy for content testing. Production: StartingKaeros 4000, Gold 500.
    public const int StartingKaeros = 20000;
    public const int StartingGold = 3000;
    public const int ItemFallbackSalePrice = 5;
    public static readonly Dictionary<int, int> DupeShards = new() { [5] = 50 };
    public static readonly int[] AscensionShardCost = [10, 15, 25, 40, 60, 80]; // A1..A6
    public const int AddonOneAscension = 2;
    public const int AddonTwoAscension = 4;

    // ---- kaeli depth: affinity / gifts / skins / mastery (refoundation 2026-06-12) ----
    public const int AffinityMaxLevel = 10;
    /// <summary>XP required to go from level N to N+1.</summary>
    public static long XpForAffinityLevel(int level) => (long)(40 * Math.Pow(level, 1.35));
    /// <summary>+1% ATK and HP per affinity level, applied at the start of the run.</summary>
    public const double AffinityStatBonusPerLevel = 0.01;
    public const long AffinityXpVictory = 50;
    public const long AffinityXpDefeat = 20;
    public const long AffinityXpPerRunLevel = 2;
    /// <summary>Affinity levels that unlock lore fragments 1..4.</summary>
    public static readonly int[] AffinityLoreLevels = [2, 4, 6, 8];
    /// <summary>Kaeros awarded upon reaching the level (affinity milestones).</summary>
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

    /// <summary>Kaeros refunded for a Kaeli removed from the roster found in an old account.</summary>
    public const int CutKaeliRefundKaeros = 600;

    // ---- dailies ----
    public const int DailyContractCount = 3;
    // MVP/test: generous dailies. Production: Kaeros 100, Gold 150.
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
    /// <summary>Raw-damage multiplier during stagger, one entry per cycle (caps at the last).
    /// F-E rebalance: flattened from [2.5, 3.5, 5.0, 6.5]. The high multiplier was designed for
    /// Tibia's monster bosses (walls of meat); with authored bosses it became "delete" in later
    /// cycles. The break remains rewarding without erasing the boss. (Calibrate against the
    /// ECHO BREAK pivot in BalanceSim — target: breaks ≲ 40% of boss bar.)</summary>
    public static readonly double[] PostureDamageMultipliers = [1.8, 2.1, 2.4, 2.8];
    /// <summary>Each valid hit during stagger also adds this fraction of the boss max HP...
    /// F-E rebalance: 0.015 → 0.006. This bonus ignores tankiness (fixed fraction of health) and procs
    /// ~7x per window, so with AOE/helper it summed ~10%+ of the bar per break. Reduced so the break
    /// weighs through the multiplier, not a fixed HP%-drain.</summary>
    public const double PostureMaxHpBonusPct = 0.006;
    /// <summary>...but no more than once per this internal cooldown (anti multi-hit exploit).</summary>
    public const int PostureMaxHpBonusCooldownMs = 600;

    // ---- F-E: elemental reactions ----
    /// <summary>How long an element "mark" lingers on a target waiting for a second element.</summary>
    public const int ElementMarkDurationMs = 4000;

    // ---- K-04: signature traits (one per Kaeli, live state in the tick) ----
    // A distinct mechanical family per Kaeli. The main tunables live in TraitDef
    // (Value/Param, amplified by mastery via _traitMult); everything else (thresholds, stacks,
    // durations, radii) lives here. Target selection is always deterministic (shortest distance,
    // tie-break by lowest id).

    // Eloa — Seal of Judgment (mark + detonate). Hits apply Sin; upon reaching N the target
    // becomes Judged and the next hit detonates a holy burst in a small area and heals the Seraph.
    public const int EloaSinStacksToJudge = 3;
    public const int EloaJudgmentRadius = 1;
    public const int EloaSinDurationMs = 4000;

    // Seren — Discipline (combo cadence). Consecutive hits on the SAME target scale damage;
    // switching targets or standing still resets it. Every Nth hit is a Perfect Cut (guaranteed crit).
    public const int SerenDisciplineResetMs = 3000;
    public const int SerenPerfectCutEvery = 3;

    // Velvet — Accumulated Curse (stacks + execute). Each hit stacks Decay (DoT) and
    // raises the execute threshold; the more invested, the sooner the target bursts.
    public const double VelvetThresholdPerStack = 0.02; // +2% threshold per stack
    public const double VelvetThresholdCap = 0.25;      // maximum threshold (executes < 25% HP)
    public const int VelvetDecayMaxStacks = 5;
    public const int VelvetDecayTicks = 3;              // duration (in ticks) of each decay DoT
    public const int VelvetDecayTickMs = 2000;
    public const double VelvetDecayDamagePerStack = 0.10; // damage/tick per stack = fraction of attack
    public const int VelvetDecayDurationMs = 5000;      // stack expiry window without refresh

    // Rin — Contagion (spreading fire). Fire hits ignite; burn jumps between enemies and each
    // burn tick heals Rin a little (pact). Value=heal, Param=jump radius.
    public const int RinContagionIntervalMs = 2000;
    public const double RinContagionBurnPower = 0.30;   // burn damage/tick = fraction of attack
    public const int RinContagionBurnTicks = 4;
    public const int RinContagionBurnTickMs = 1000;

    // Rynna — Static Charge (charge bar). Hits fill the charge; when full, the hit that completes it
    // becomes a Discharge (short chain + paralyze) and the paralyze accelerates the ultimate.
    public const double RynnaChargeMax = 100;
    public const double RynnaChargePerHit = 20;         // 5 hits fill it
    public const double RynnaDischargeDamageMult = 1.5; // ~150% of attack per target
    public const int RynnaDischargeChainJumps = 3;
    public const int RynnaDischargeChainRange = 3;
    public const int RynnaParalyzeMs = 800;
    public const double RynnaParalyzeGaugeBonus = 8;

    // Lunara — Shatter (hit-and-run). Hitting a slowed target deals bonus damage + brief haste;
    // the Nth hit on the slowed target shatters it (burst and consumes the slow). Value=bonus, Param=slow dur.
    public const double LunaraSlowFactor = 0.65;        // 35% slow applied by ice
    public const double LunaraHasteFactor = 1.2;
    public const int LunaraHasteMs = 2000;
    public const int LunaraShatterHits = 3;
    public const double LunaraShatterDamageMult = 1.5;

    // Gaia — Prey (chase and execute). Marks a target; damage against the Prey grows the longer
    // the hunt lasts; when the Prey dies the mark jumps and Gaia gains cadence. Value=ramp/s, Param=cap.
    public const double GaiaHuntAtkSpeedBonus = 0.20;
    public const int GaiaHuntBonusMs = 3000;

    // ================= G-04B: Echoes per Kaeli (3 × 7) =================
    // Each Echo (cap of 1 stack) branches into the trait/card hooks of GameWorld — no new dispatch.
    // Win-conditions anchored in the real trait field/constant (SinStacks, _comboHits, DecayStacks,
    // burn DoTs, _staticCharge, SlowUntilMs, _preyId). Deterministic: only _rng/NowMs + tie-break by id.

    /// <summary>Echo Shield (Eloa Martyr / Velvet Pact): cap on absorbed over-health, fraction of max health.</summary>
    public const double EchoShieldCapFraction = 0.60;

    // Eloa — chain-judgment: the burst seeds Sin on those hit. sentence: Judges sooner and
    // each Judgment amplifies the next burst (accumulates up to a cap).
    public const int EchoEloaChainSinSeed = 1;
    public const int EchoSentenceStacksToJudge = 2;
    public const double EchoSentenceBurstPerStack = 0.15; // +15% burst per accumulated Judgment
    public const int EchoSentenceMaxStacks = 6;

    // Seren — endless-cadence: uncapped ramp, harsher reset. perfect-execution: Cut every 2nd,
    // guaranteed crit executes weak targets. immortal-stance: damage reduction at high combo.
    public const int EchoEndlessCadenceResetMs = 1200;
    public const int EchoPerfectCutEvery = 2;
    public const double EchoPerfectExecuteHpFraction = 0.15;
    public const int EchoImmortalComboThreshold = 4;
    public const double EchoImmortalDamageReduction = 0.40;

    // Velvet — blood-pact: each Decay charge applied becomes a shield (fraction of Curse damage).
    // viral-plague: upon death, Decay jumps with live stacks to the nearest target (reuses base DoT).
    public const double EchoBloodPactShieldFraction = 0.50;

    // Rin — pyre: damage grows with the number of burning targets. holocaust: dying in flames explodes in an area.
    // wildfire reuses Contagion (fire from any element + refresh) without a new constant.
    public const double EchoPyreDamagePerBurning = 0.08;
    public const double EchoPyreMaxBonus = 0.60;
    public const double EchoHolocaustDamageMult = 1.2;
    public const int EchoHolocaustRadius = 1;

    // Rynna — perpetual-storm: Discharge retains half the Charge and Charge fills twice as fast. overload:
    // paralyze becomes a shock DoT. thunder-core: Discharge gauge turbocharged + ult refunds Charge.
    public const double EchoPerpetualDischargeRetain = 0.50;
    public const double EchoPerpetualChargeMult = 2.0;
    public const double EchoOverloadDotPower = 0.20;
    public const int EchoOverloadDotTicks = 3;
    public const int EchoOverloadDotTickMs = 400;
    public const double EchoThunderCoreGaugeMult = 3.0;

    // Lunara — eternal-winter: enemies enter slowed upon seeing Lunara, slow stronger (no floor).
    // chain-shatter: Shatter jumps to nearby slowed targets. moon-dance: shatters on the 2nd hit.
    public const double EchoEternalWinterSlowFactor = 0.45;
    public const int EchoEternalWinterAggroSlowMs = 1500;
    public const int EchoChainShatterRange = 3;
    public const double EchoChainShatterDamageMult = 0.70;
    public const int EchoMoonDanceShatterHits = 2;

    // Gaia — eternal-hunt: Prey ramp and cap doubled. pack: 2 Preys + larger hunt bonus.
    // deep-roots: each hit on the Prey roots (heavy slow) and drives an earth poison.
    public const double EchoEternalHuntRampMult = 2.0;
    public const double EchoEternalHuntCapMult = 2.0;
    public const int EchoPackHuntBonusMs = 5000;
    public const double EchoDeepRootsSlowFactor = 0.40;
    public const int EchoDeepRootsSlowMs = 1200;
    public const double EchoDeepRootsDotPower = 0.15;
    public const int EchoDeepRootsDotTicks = 3;
    public const int EchoDeepRootsDotTickMs = 1000;

    // ---- G-08B: new archetypes + keyword interaction ----
    // charger: the dash moves in a straight line to the target (stopping 1 tile away), with an animation window on the client.
    public const int ChargeMaxTiles = 4;
    public const int ChargeDashMs = 220;
    public const int ChargeFx = 11; // teleport poof as charge trail
    // bomber/suicide: area burst on death; damage = highest kit attack × scale.
    public const int BomberExplodeFx = 35; // large explosion
    // shielder: the barrier on an ally never exceeds this fraction of the ally's max health.
    public const double MonsterShieldCapFraction = 0.50;
    public const int MonsterShieldFx = 49; // protection glow
    // Keyword interaction: G-04 tags a mob can resist/amplify (% 0-100, negative = amplifies).
    public static readonly string[] MonsterKeywordTags =
        ["sin", "combo", "curse", "burn", "charge", "frost", "prey", "posture"];
    public const int KeywordResistMin = -100;
    public const int KeywordResistMax = 100;
}

/// <summary>
/// MG-02: combat tuning per role. AoeScale is consumed in MG-04 (AOE resize); here in MG-02
/// only AutoDmgMult/SkillDmgMult/BaseAutoAttackMs/AutoRange take effect.
/// </summary>
public sealed record RoleTuning(
    double AutoDmgMult, double SkillDmgMult, int BaseAutoAttackMs, int AutoRange, double AoeScale);

/// <summary>
/// MG-05: serializable row of <see cref="RoleTuning"/> for persistence/editing in admin. The role
/// goes as a string (readable in JSON and frontend); ContentStore converts to/from the typed dictionary.
/// </summary>
public sealed record RoleTuningRow(
    string Role, double AutoDmgMult, double SkillDmgMult,
    int BaseAutoAttackMs, int AutoRange, double AoeScale);

public sealed record DungeonTier(
    int Tier, string Name, string Description,
    string[] CommonMobs, string[] EliteMobs, string Boss,
    int RequiredAccountLevel, double StatMultiplier);

public sealed record MonsterStatLine(int Health, int Damage, int Armor, int Speed, int Experience);
public sealed record MonsterStatPreset(
    string Id, string Name, string Description,
    double HpMultiplier, double DamageMultiplier, double SpeedMultiplier, double CadenceMultiplier);
public sealed record ItemBalanceGrade(string Id, string Name);
public sealed record ItemBalanceRange(
    string Stat, int Tier,
    double LowMin, double LowMax,
    double ModerateMin, double ModerateMax,
    double HighMin, double HighMax);
public sealed record MonsterElementProfile(
    string Id, string Name, int AreaEffect, int ShootEffect, string? ConditionType);
public sealed record MonsterAttackPattern(
    string Kind, int Range, int Radius, int Length, int Spread, bool Target,
    int IntervalMs, int Chance, double MinDamageScale, double MaxDamageScale,
    bool UsesElement, double ConditionDamageScale);
public sealed record MonsterBehaviorProfile(
    string Id, string Name, string Description, int TargetDistance, int StaticAttackChance,
    MonsterAttackPattern[] Attacks, double HealFraction = 0, int HealIntervalMs = 0,
    // G-08B: new archetypes. Data-only fields — the tick reads the profile by BehaviorId (no new dispatch).
    int SpawnCost = 2,                       // spawn budget cost per room (swarm = 1, numerical pressure)
    double PostureScale = 0,                 // >0: common/elite mob gains Posture (posture tank) scaled by this factor
    string SummonSpecies = "",               // summoner: authored id/name conjured (wired in MonsterAuthoring.Resolve)
    int SummonCount = 1, int SummonChance = 100, int SummonIntervalMs = 0, int SummonMax = 0,
    int ExplodeRadius = 0, double ExplodeDamageScale = 0,  // bomber/suicide: area burst on death near player
    int ShieldRadius = 0, double ShieldFraction = 0, int ShieldIntervalMs = 0); // shielder: barrier on nearby ally
