namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Skill shapes stay generic so adding a class only requires data, not a new engine dispatch.
/// FX ids are Tibia CONST_ME_* / CONST_ANI_* values.
/// </summary>
public sealed record SkillDef(
    string Id, string Name, string Shape, string Element, double Power, int CooldownMs,
    int Range, int Radius, int MissileId, int EffectId, int StunMs, string? Buff,
    int BuffMs, string Description,
    // Optional damage-over-time rider: any damaging hit can leave a DoT on the target.
    // DamagePerTick = PlayerAttack * DotPower (so it scales like a small skill, per tick).
    int DotTicks = 0, int DotTickMs = 0, double DotPower = 0,
    // Optional summon (shape "summon") / terrain (shape "field"): a construct that pulses area
    // damage, or painted ground tiles that damage/slow whoever stands on them.
    int SummonMs = 0, int SummonPulseMs = 0, double SummonPower = 0, int SummonRadius = 0,
    // Optional slow rider (any hit applies slow to the mob, reuses the chiller's slow).
    double SlowFactor = 1, int SlowMs = 0,
    // Chain (shape "chain"): jumps between targets, damage *= (1-ChainFalloff) per jump.
    int ChainJumps = 0, int ChainRange = 0, double ChainFalloff = 0,
    // Multi-beat (shape "barrage"): Strikes blows at the target point, spaced StrikeIntervalMs apart,
    // the first after StrikeDelayMs (a single delayed blow = Strikes 1 + StrikeDelayMs).
    int Strikes = 0, int StrikeIntervalMs = 0, int StrikeDelayMs = 0,
    // Hollow ring (shape "ring"): central hole of radius RingInner inside the Radius.
    int RingInner = 0,
    // Spreading terrain (shape "field" and barrage that leaves a trail): each lit tile has a
    // FieldSpreadChance% per tick to ignite a free neighbor, up to FieldSpreadGenerations generations
    // away from the origin. 0 = static field (old behavior). StrikeLeavesField makes each barrage blow
    // drop a field (reuses SummonMs/SummonPulseMs/SummonPower/SummonRadius + the spread params) — the
    // meteor rain sets the ground ablaze where it falls.
    int FieldSpreadChance = 0, int FieldSpreadGenerations = 0, bool StrikeLeavesField = false,
    // Roaming summon (shape "summon"): the construct drifts one tile toward the nearest enemy each
    // pulse and, with SummonLeavesField, seeds a spreading field of its element on the tiles it crosses
    // (reuses FieldSpreadChance/FieldSpreadGenerations). 0 = the classic stationary pulser.
    bool SummonRoams = false, bool SummonLeavesField = false,
    // Auto-modifier (shape "buff", KR-00 shared seam): arms an empowered-autoattack state. Kind is
    // "cleave" (small area at the target), "pierce" (splash to the nearest neighbor) or "lock_pierce"
    // (redirects the auto onto the trait mark, then pierces). AutoModCharges > 0 = charge-based (one
    // spent per auto, refunded on kill when AutoModResetOnKill, capped at AutoModCharges); 0 =
    // time-windowed by BuffMs.
    string? AutoModKind = null, int AutoModCharges = 0, bool AutoModResetOnKill = false,
    // Signature-trait charge bonus: a direct hit by this cast seeds this many EXTRA charges of the
    // Kaeli's trait (on top of the base 1) — e.g. Eloa's Judging Lance seeds extra Sin to drive the
    // priority target toward Judged faster. Read in ApplyTraitPostDamage. 0 = normal (one per hit).
    int TraitChargeBonus = 0,
    // Low-HP finisher (Velvet's Soul Rend, §4G): a direct hit by this cast deals +LowHpBonus damage
    // when the target sits below LowHpThreshold of its max HP — the same execution family as the Decay
    // trait threshold, but a per-skill knob independent of the passive. 0 = no finisher bonus.
    double LowHpBonus = 0, double LowHpThreshold = 0,
    // Death Orb detonation (Velvet's Reign of Shadows, §4G): on cast, immediately resolve every pending
    // Death Orb on the current floor instead of waiting out its delay. Reuses the orb-burst path, so the
    // anti-cascade guard (_resolvingDeathOrb) and the per-floor cap stay intact. false = no-op.
    bool DetonateDeathOrbs = false,
    // Burn reap (Rin's Wildfire Reckoning, §4F): each hit CONSUMES the target's pending fire burn,
    // dealing ConsumeBurnBonus × the remaining burn as an instant burst (amplified by any active
    // Infernal Ball multiplier) and leaving only a light ember. 0 = no reap. Read in HitMonster.
    double ConsumeBurnBonus = 0,
    // Burn multiplier stacking (Rin's Infernal Ball, §4F): each impact of this barrage adds one stack
    // to the room-wide burn-damage multiplier (does NOT consume — the explicit contrast with Reckoning).
    // Stacks amplify every fire DoT tick and decay one at a time. false = no stack. Read in ResolveStrike.
    bool StackBurnMult = false,
    // Prey ramp cash-out (Gaia's Coup de Grace, §4D): a direct hit against the marked Prey gets an
    // extra multiplier from the current hunt ramp, then consumes that ramp by restarting the hunt timer.
    double ConsumePreyRampBonus = 0,
    // Frost cash-out (Lunara's Absolute Zero, §4C): after the skill hit lands, consume all Frostbite
    // stacks on the target as an immediate shatter burst. The skill's own StunMs is the hard-freeze.
    bool MassShatterFrost = false,
    // Skill lifesteal / brawler riders (Rynna): direct damage by this skill heals for this extra
    // fraction of final damage; PullTiles drags targets toward the player; EchoShieldOnHit grants
    // player shield per caught target; DetonateStaticMarks lets scheduled ult waves cash Static marks.
    double SkillLifesteal = 0, int PullTiles = 0, double EchoShieldOnHit = 0,
    bool DetonateStaticMarks = false);

public sealed record ClassStanceDef(
    string Id, string Name, string Element, string[] Slots, string Ultimate);

public sealed record ClassDef(
    string Id, string Name, string Description, string DefaultStanceId,
    IReadOnlyList<ClassStanceDef> Stances)
{
    public bool CanToggleStance => Stances.Count > 1;

    public ClassStanceDef GetStance(string stanceId) =>
        Stances.FirstOrDefault(s => s.Id == stanceId)
        ?? throw new InvalidOperationException($"unknown stance: {Id}/{stanceId}");

    public ClassStanceDef InitialStance(string affinity) =>
        Stances.FirstOrDefault(s => s.Element == affinity) ?? GetStance(DefaultStanceId);

    public ClassStanceDef NextStance(string stanceId)
    {
        if (!CanToggleStance) return GetStance(DefaultStanceId);
        var index = -1;
        for (var i = 0; i < Stances.Count; i++)
        {
            if (Stances[i].Id == stanceId)
            {
                index = i;
                break;
            }
        }
        return Stances[(index + 1 + Stances.Count) % Stances.Count];
    }
}

/// <summary>
/// Canonical Kaezan World classes. Add a future class by registering its skills and one
/// ClassDef entry; waifus only point at the class id. Each kit uses a different shape per slot
/// (single/area/cone/beam/nova/chain/ring/field/barrage/summon/buff) so that no ability becomes
/// just "the same area with a swapped element".
///
/// Kaelis refoundation (K-03): the 7 roster classes got SIGNATURE kits, one clear archetype per
/// Kaeli, still 100% data-driven by shape (no new dispatch in the engine). The class id is stable and
/// internal (not persisted per account); it's the display name that matches the fantasy of the owning
/// Kaeli. Map id→Kaeli: oracle=Eloa, warrior=Seren, necromancer=Velvet, pyromancer=Rin,
/// stormcaller=Rynna, cryomancer=Lunara, shaman=Gaia. Sentinel and Barbarian stay as reserve classes
/// (no Kaeli yet) because ItemAuthoring maps weapon types → class ids through them.
/// </summary>
public static class Classes
{
    public const string WarriorId     = "warrior";     // Seren — Astral Knight (physical melee)
    public const string SentinelId    = "sentinel";    // reserve (distance/shield) — no Kaeli
    public const string OracleId      = "oracle";      // Eloa — Seraph (holy ranged)
    public const string ShamanId      = "shaman";      // Gaia — Monolith Archer (earth ranged)
    public const string CryomancerId  = "cryomancer";  // Lunara — Ice Archer (ice ranged, bow)
    public const string PyromancerId  = "pyromancer";  // Rin — Pact Succubus (fire ranged)
    public const string StormcallerId = "stormcaller"; // Rynna — Thunder Dragoness (energy melee)
    public const string BarbarianId   = "barbarian";   // reserve (fist) — no Kaeli
    public const string NecromancerId = "necromancer"; // Velvet — Necromancer (death ranged)
    public const string WizardId = PyromancerId;
    public const string MonkId   = BarbarianId;

    public static readonly IReadOnlyDictionary<string, SkillDef> Skills = new[]
    {
        // ============================ ROSTER — SIGNATURE KITS (K-03) ============================

        // Eloa — Seraph (holy ranged / AoE queen, §4E). Pure simultaneous burst: nothing pings, everything
        // explodes. No lingering field — the dash trail (§2.7) covers the residual holy ground, so the old
        // Consecrated Halo is cut. Judging Lance seeds extra Sin; barrage is confined to the ult.
        new SkillDef("skill:eloa:lance", "Judging Lance", "single", "holy",
            1.15, 2000, 5, 0, 38, 40, 0, null, 0,
            "Hurls a lance of judgment at a priority target, searing extra Sin into it.",
            TraitChargeBonus: GameConfig.EloaJudgingLanceSinBonus),
        new SkillDef("skill:eloa:judgment", "Dawn Ring", "ring", "holy",
            1.20, 9000, 6, GameConfig.EloaDawnRingRadius, 38, 40, 0, null, 0,
            "A ring of dawnlight bursts outward around the Seraph, judging everything caught in the band.",
            Strikes: GameConfig.EloaDawnRingExpansionBands,
            StrikeIntervalMs: GameConfig.EloaDawnRingPulseIntervalMs,
            StrikeDelayMs: GameConfig.EloaDawnRingPulseDelayMs,
            RingInner: 1),
        new SkillDef("skill:eloa:zenith", "Zenith Strike", "area", "holy",
            1.60, 7000, 6, 2, 0, 50, 0, null, 0,
            "Calls down an instant pillar of light onto a distant area, detonating it in holy fire."),
        new SkillDef("skill:eloa:radiance", "Sacred Ray", "beam", "holy",
            1.75, 6000, 5, 0, 0, 40, 0, null, 0,
            "Channels a long holy beam in a line."),
        new SkillDef("skill:eloa:absolution", "Absolution", "barrage", "holy",
            2.20, 0, 6, 2, 0, 40, 0, null, 0,
            "Calls down a rapid storm of light pillars all at once — the sky itself passes sentence.",
            Strikes: 7, StrikeIntervalMs: 180, StrikeDelayMs: 150),

        // Seren — Astral Knight (physical melee auto duelist, §4). Discipline stays the identity:
        // Astral Sweep gives pack-cleave windows, War Cadence sustains the box, and Zenith unlocks
        // the ramp so her boss damage can spill through a mob stack.
        new SkillDef("skill:seren:cut", "Astral Sweep", "buff", "physical",
            0, 6500, 0, 0, 0, 10, 0, null, GameConfig.SerenAstralSweepWindowMs,
            "Empowers the next cuts: autos cleave around the target, and a kill refreshes the sweep.",
            AutoModKind: "cleave",
            AutoModCharges: GameConfig.SerenAstralSweepCharges,
            AutoModResetOnKill: true),
        new SkillDef("skill:seren:advance", "Star Lance", "beam", "physical",
            1.70, 7000, 3, 0, 0, 10, 0, null, 0,
            "Thrusts a concentrated astral line through the enemies ahead."),
        new SkillDef("skill:seren:arc", "Duelist's Call", "cone", "physical",
            1.20, 9000, 0, 2, 0, 10, 0, "taunt", GameConfig.MeleeTauntMs,
            "Calls the duel in a blade fan, taunting enemies into the box before two close-range beats land.",
            Strikes: GameConfig.SerenDuelistCallPulseCount,
            StrikeIntervalMs: GameConfig.SerenDuelistCallPulseIntervalMs,
            StrikeDelayMs: GameConfig.SerenDuelistCallPulseDelayMs,
            SummonRadius: GameConfig.SerenDuelistCallPulseRadius,
            SummonPower: GameConfig.SerenDuelistCallPulsePower),
        new SkillDef("skill:seren:stance", "War Cadence", "buff", "support",
            0, 14000, 0, 0, 0, 13, 0, GameConfig.SerenWarCadenceBuff, 6000,
            "Settles into a battle cadence, accelerating her cuts and drinking life back from them."),
        new SkillDef("skill:seren:zenith", "Zenith", "buff", "physical",
            0, 0, 0, 0, 0, 35, 0, GameConfig.UltStateRampUnlocked, GameConfig.SerenZenithBuffMs,
            "Reaches the zenith: Discipline opens to every target, and every hit becomes a Perfect Cut."),

        // Velvet — Necromancer (death ranged / soul-reaping caster, §4G). The Decay trait already is the
        // right engine (hits stack Decay + lower the execute threshold; a kill under Decay drops a Death
        // Orb). The kit only reshapes the slots around it: Soul Rend finishes low-HP targets, Cursed
        // Ground and Abyssal Shade stay as they were, Nightmare (beam) moves to s3 (§2.7), and the ult
        // Reign of Shadows gains the one new piece — it force-detonates every pending Death Orb at once.
        new SkillDef("skill:velvet:strike", "Soul Rend", "single", "death",
            1.30, 2000, 4, 0, 11, 18, 0, null, 0,
            "Tears the soul from a target — a finisher that bites far deeper into the wounded.",
            LowHpBonus: GameConfig.VelvetSoulRendLowHpBonus,
            LowHpThreshold: GameConfig.VelvetSoulRendLowHpThreshold),
        new SkillDef("skill:velvet:curse", "Cursed Ground", "field", "death",
            0, 6000, 5, 0, 11, 18, 0, null, 0,
            "Curses the ground with a creeping rot that spreads tile by tile, devouring whoever lingers.",
            SummonMs: 4000, SummonPulseMs: 700, SummonPower: 0.45, SummonRadius: 1,
            FieldSpreadChance: 30, FieldSpreadGenerations: 2),
        new SkillDef("skill:velvet:shade", "Abyssal Shade", "summon", "death",
            0, 12000, 0, 1, 0, 18, 0, null, 0,
            "Raises an abyssal shade that drifts toward the living, pulsing death and trailing creeping corrosion.",
            SummonMs: 6000, SummonPulseMs: 700, SummonPower: 0.70, SummonRadius: 1,
            FieldSpreadChance: 20, FieldSpreadGenerations: 1,
            SummonRoams: true, SummonLeavesField: true),
        new SkillDef("skill:velvet:nightmare", "Nightmare", "beam", "death",
            1.80, 8000, 7, 0, 0, 18, 0, null, 0,
            "Projects a long nightmare beam in a line."),
        new SkillDef("skill:velvet:plague", "Reign of Shadows", "barrage", "death",
            1.30, 0, 6, 2, 11, 18, 0, null, 0,
            "Calls down a slow rain of shadow and rips every lingering Death Orb open at once — the harvest of the whole run cashed in on command.",
            Strikes: 5, StrikeIntervalMs: 350, StrikeDelayMs: 250,
            DotTicks: 4, DotTickMs: 1000, DotPower: 0.45,
            SummonMs: 3500, SummonPulseMs: 700, SummonPower: 0.35, SummonRadius: 1,
            StrikeLeavesField: true, FieldSpreadChance: 35, FieldSpreadGenerations: 2,
            DetonateDeathOrbs: true),

        // Rin — Pact Succubus (fire ranged / DoT with rhythm, §4F). The Contagion trait is already the
        // engine (hits ignite; burn heals her and jumps between burning enemies on its own). The kit gives
        // her agency over that DoT: Ember Kiss reliably ignites a cold target, Cinder Storm seeds ignite in
        // an area (no slow-jump wait), Wildfire Reckoning REAPS the pending burn as a burst (consume), Ashen
        // Breath is the universal s3 beam (§2.7), and the ult Infernal Ball stacks a burn multiplier per
        // impact (amplify — never consume, the explicit contrast with Reckoning).
        new SkillDef("skill:rin:ember-kiss", "Ember Kiss", "single", "fire",
            1.30, 1700, 5, 0, 4, 7, 0, null, 0,
            "Blows a precise ember kiss that always catches — a reliable starter that ignites even a cold target.",
            DotTicks: GameConfig.RinContagionBurnTicks, DotTickMs: GameConfig.RinContagionBurnTickMs,
            DotPower: GameConfig.RinContagionBurnPower),
        new SkillDef("skill:rin:contract", "Cinder Storm", "area", "fire",
            1.10, 7000, 6, 2, 4, 7, 0, null, 0,
            "Scatters a storm of cinders over a zone, setting everyone caught ablaze at once."),
        new SkillDef("skill:rin:hall", "Wildfire Reckoning", "nova", "fire",
            1.20, 9000, 0, 3, 4, 7, 0, null, 0,
            "Erupts in a ring of wildfire, tearing the pent-up flames out of every burning enemy for a "
            + "massive burst — then leaves only a light ember behind.",
            ConsumeBurnBonus: GameConfig.RinReckoningConsumeMult),
        new SkillDef("skill:rin:ashwings", "Ashen Breath", "beam", "fire",
            1.55, 6000, 5, 0, 0, 7, 0, null, 0,
            "Exhales a searing line of fire that pierces everything directly ahead."),
        new SkillDef("skill:rin:infernal-ball", "Infernal Ball", "barrage", "fire",
            1.50, 0, 7, 2, 4, 7, 400, null, 0,
            "Conducts a ball of meteors that fall in sequence: each impact stuns, ignites the ground and "
            + "stokes every fire in the arena hotter — the burn keeps climbing while the meteors fall.",
            Strikes: 4, StrikeIntervalMs: 480, StrikeDelayMs: 380,
            SummonMs: 3200, SummonPulseMs: 600, SummonPower: 0.30, SummonRadius: 1,
            StrikeLeavesField: true, FieldSpreadChance: 38, FieldSpreadGenerations: 2,
            StackBurnMult: true),

        // Rynna — Thunder Dragoness (energy melee caster / vampiric berserker, §4B). Static Charge
        // marks targets and detonates one marked victim when full; Chain Lightning and Storm Heart
        // provide the vampiric AoE, Bloodlust embraces risk, and Storm Pull gathers the box.
        new SkillDef("skill:rynna:claw", "Voltaic Claw", "single", "energy",
            1.25, 1800, 1, 0, 0, 12, 0, null, 0,
            "Rakes the adjacent target with a charged claw, building Static Charge faster.",
            TraitChargeBonus: GameConfig.RynnaVoltaicClawChargeBonus),
        new SkillDef("skill:rynna:discharge", "Chain Lightning", "chain", "energy",
            1.25, 6500, 2, 0, 0, 12, 0, null, 0,
            "Releases vampiric lightning that jumps through the pack and drinks back health.",
            ChainJumps: GameConfig.RynnaChainLightningJumps,
            ChainRange: GameConfig.RynnaChainLightningRange,
            ChainFalloff: GameConfig.RynnaChainLightningFalloff,
            SkillLifesteal: GameConfig.RynnaChainLightningLifesteal),
        new SkillDef("skill:rynna:scale", "Bloodlust", "buff", "support",
            0, 12000, 0, 0, 0, 41, 0, GameConfig.RynnaBloodlustBuff, GameConfig.RynnaBloodlustMs,
            "Lets the storm run wild: more damage and lifesteal, but incoming hits bite harder."),
        new SkillDef("skill:rynna:tail", "Storm Pull", "nova", "energy",
            0.55, 8500, 0, 3, 0, 12, 0, null, 0,
            "Hooks nearby enemies with lightning, dragging them into the box and raising an echo shield.",
            PullTiles: GameConfig.RynnaStormPullTiles,
            EchoShieldOnHit: GameConfig.RynnaStormPullShieldPerTarget),
        new SkillDef("skill:rynna:storm-heart", "Storm Heart", "nova", "energy",
            1.05, 0, 0, 3, 0, 41, GameConfig.RynnaStaticDetonateStunMs, null, 0,
            "Beats the storm heart in waves, detonating every Static mark caught and drinking the lightning back.",
            Strikes: GameConfig.RynnaStormHeartWaves,
            StrikeIntervalMs: GameConfig.RynnaStormHeartWaveIntervalMs,
            DetonateStaticMarks: true),

        // Lunara — Ice Archer (ice ranged auto / frost marksman, §4C). Frostbite is now the damage
        // engine: autos spread frost and trigger cascaded shatters. Only the kite tools slow/root
        // (Frozen Garden and New Moon); the ult is the sole hard-freeze.
        new SkillDef("skill:lunara:cut", "Moonlight Volley", "buff", "ice",
            0, 6500, 0, 0, 29, 42, 0, null, GameConfig.LunaraMoonlightVolleyWindowMs,
            "Loads the next moonlit shots: autos pierce through the pack and seed extra frost.",
            AutoModKind: "pierce",
            AutoModCharges: GameConfig.LunaraMoonlightVolleyCharges),
        new SkillDef("skill:lunara:garden", "Frozen Garden", "field", "ice",
            0, 10000, 5, 0, 37, 44, 0, null, 0,
            "Blooms a garden of ice on the ground, slowing and wounding whoever stays in it.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.35, SummonRadius: 1,
            SlowFactor: 0.5, SlowMs: 1500),
        new SkillDef("skill:lunara:frost-leap", "Lunar Focus", "buff", "support",
            0, 12000, 0, 0, 0, 42, 0, GameConfig.LunaraLunarFocusBuff, GameConfig.LunaraLunarFocusMs,
            "Draws the bow under a cold moon, quickening autos and extending her firing lane."),
        new SkillDef("skill:lunara:new-moon", "New Moon", "nova", "ice",
            0.85, 8500, 0, 3, 0, 44, 0, null, 0,
            "Drops the new moon around her, peeling a closing pack away with a heavy frost snare.",
            SlowFactor: GameConfig.LunaraNewMoonSlowFactor,
            SlowMs: GameConfig.LunaraNewMoonSlowMs),
        new SkillDef("skill:lunara:crescent", "Absolute Zero", "nova", "ice",
            1.20, 0, 0, 3, 0, 44, GameConfig.LunaraAbsoluteZeroFreezeMs, null, 0,
            "Stops the room at absolute zero, hard-freezing enemies and cashing out every frost stack in one mass shatter.",
            MassShatterFrost: true),

        // Gaia — Monolith Archer (earth ranged serial hunter, §4D). Prey stays as the identity: Hunter's
        // Aim keeps autos locked on the mark, Binding Roots preserves the kite, Coup de Grace cashes the
        // hunt ramp, Monolith Fall is real damage + stun, and Ricochet turns the hunt parallel briefly.
        new SkillDef("skill:gaia:arrow", "Hunter's Aim", "buff", "earth",
            0, 6500, 0, 0, 30, 46, 0, null, 4500,
            "Focuses the hunt: the next shots lock onto the Prey and pierce into a nearby target.",
            AutoModKind: "lock_pierce", AutoModCharges: 4),
        new SkillDef("skill:gaia:monolith", "Monolith Fall", "area", "earth",
            1.75, 7000, 7, 2, 30, 46, 450, null, 0,
            "Drops a heavy monolith onto the target area, crushing and stunning whoever is beneath it."),
        new SkillDef("skill:gaia:roots", "Binding Roots", "field", "earth",
            0, 9000, 6, 0, 30, 46, 0, null, 0,
            "Sprouts stone roots from the ground, binding and wounding whoever steps on them.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.40, SummonRadius: 1,
            SlowFactor: 0.4, SlowMs: 2000),
        new SkillDef("skill:gaia:shards", "Coup de Grace", "single", "earth",
            1.85, 6000, 7, 0, 30, 46, 0, null, 0,
            "Fires the finishing shot, stronger against wounded Prey and cashing out the hunt.",
            LowHpBonus: GameConfig.GaiaCoupLowHpBonus,
            LowHpThreshold: GameConfig.GaiaCoupLowHpThreshold,
            ConsumePreyRampBonus: GameConfig.GaiaCoupPreyRampConsumeBonus),
        new SkillDef("skill:gaia:tectonic", "Ricochet", "buff", "earth",
            0, 0, 0, 0, 30, 46, 0, GameConfig.UltStateAutoChain, 7000,
            "Turns the hunt parallel: shots keep full force on the Prey and ricochet through nearby foes."),

        // ============================ RESERVE — no Kaeli yet (ItemAuthoring) ============================
        // Sentinel: precise physical shooter (distance/shield). Kept for the weapon→class map and
        // as the base for a future ranged physical Kaeli.
        new SkillDef("skill:sentinel:storm-missile", "Storm Missile", "single", "physical",
            1.15, 2000, 4, 0, 12, 10, 0, null, 0,
            "Precise physical shot that staggers the target.",
            SlowFactor: 0.75, SlowMs: 1200),
        new SkillDef("skill:sentinel:storm-flail", "Storm Flail", "chain", "physical",
            1.30, 7000, 6, 0, 12, 10, 0, null, 0,
            "Hurls a flail that ricochets between nearby enemies.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:sentinel:storm-slam", "Storm Slam", "nova", "physical",
            1.45, 4000, 0, 2, 0, 10, 400, null, 0,
            "Slams the ground with force, stunning those nearby."),
        new SkillDef("skill:sentinel:squall-zone", "Squall Zone", "field", "physical",
            0, 9000, 6, 0, 12, 10, 0, null, 0,
            "Creates a zone of cutting wind that slows and hurts whoever stays inside.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.45, SummonRadius: 1,
            SlowFactor: 0.6, SlowMs: 1500),
        new SkillDef("skill:sentinel:aegis", "Sentinel Aegis", "buff", "support",
            0, 0, 0, 0, 0, 13, 0, "aegis", 10000,
            "Increases attack and attack speed for 10s."),

        // Barbarian: melee martial artist. Rampage and the signature combo (jumps between
        // enemies), War Cry for speed — none of the static solid area like the Warrior.
        new SkillDef("skill:barbarian:double-jab", "Double Jab", "single", "physical",
            1.20, 1500, 1, 0, 0, 1, 0, null, 0,
            "Chains quick blows against an adjacent target."),
        new SkillDef("skill:barbarian:rampage", "Rampage", "chain", "physical",
            1.35, 7000, 2, 0, 0, 35, 0, null, 0,
            "Strikes the target and the impact jumps to the nearest enemies.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:barbarian:palm-shockwave", "Palm Shockwave", "cone", "physical",
            1.55, 6000, 0, 2, 0, 1, 350, null, 0,
            "Releases a fan-shaped wave of ki that stuns whoever is hit."),
        new SkillDef("skill:barbarian:war-cry", "War Cry", "buff", "physical",
            0, 11000, 0, 0, 0, 35, 0, "haste", 4000,
            "Roars and quickens its own steps for a few seconds."),
        new SkillDef("skill:barbarian:spiritual-outburst", "Spiritual Outburst", "nova", "physical",
            2.60, 0, 0, 3, 0, 35, 600, null, 0,
            "Unleashes all inner harmony in a burst of ki."),
    }.ToDictionary(s => s.Id);

    public static readonly IReadOnlyList<ClassDef> All =
    [
        new ClassDef(WarriorId, "Astral Knight",
            "Physical melee auto duelist: Astral Sweep cleaves packs, Star Lance pierces a line, War Cadence sustains the box and Duelist's Call pulls enemies in — closing on Zenith, where Discipline is unleashed.",
            "physical",
            [
                new ClassStanceDef("physical", "Physical", "physical",
                    [
                        "skill:seren:cut",
                        "skill:seren:advance",
                        "skill:seren:stance",
                        "skill:seren:arc"
                    ],
                    "skill:seren:zenith")
            ]),
        new ClassDef(OracleId, "Seraph",
            "Ranged seraph of light and AoE queen: a Sin-searing lance, an expanding dawn ring, a distant zenith strike and the universal sacred ray — ends on Absolution, a rapid storm of light.",
            "holy",
            [
                new ClassStanceDef("holy", "Holy", "holy",
                    [
                        "skill:eloa:lance",
                        "skill:eloa:judgment",
                        "skill:eloa:zenith",
                        "skill:eloa:radiance"
                    ],
                    "skill:eloa:absolution")
            ]),
        new ClassDef(SentinelId, "Sentinel",
            "Precise physical shooter: slowing projectiles, chain, slam and a wind zone.",
            "physical",
            [
                new ClassStanceDef("physical", "Physical", "physical",
                    [
                        "skill:sentinel:storm-missile",
                        "skill:sentinel:storm-flail",
                        "skill:sentinel:storm-slam",
                        "skill:sentinel:squall-zone"
                    ],
                    "skill:sentinel:aegis")
            ]),
        new ClassDef(CryomancerId, "Ice Archer",
            "Ranged frost marksman: Moonlight Volley empowers piercing autos, Frozen Garden and New Moon keep the kite clean, Lunar Focus extends the firing lane, and Absolute Zero cashes out the whole frost stack.",
            "ice",
            [
                new ClassStanceDef("ice", "Ice", "ice",
                    [
                        "skill:lunara:cut",
                        "skill:lunara:garden",
                        "skill:lunara:frost-leap",
                        "skill:lunara:new-moon"
                    ],
                    "skill:lunara:crescent")
            ]),
        new ClassDef(ShamanId, "Monolith Archer",
            "Ranged mineral hunter: Hunter's Aim locks autos onto the Prey, roots hold the chase, Coup de Grace executes the mark and Monolith Fall crushes packs — closes on Ricochet.",
            "earth",
            [
                new ClassStanceDef("earth", "Earth", "earth",
                    [
                        "skill:gaia:arrow",
                        "skill:gaia:roots",
                        "skill:gaia:shards",
                        "skill:gaia:monolith"
                    ],
                    "skill:gaia:tectonic")
            ]),
        new ClassDef(PyromancerId, "Pact Succubus",
            "Ranged fire conjurer of spreading burn: a reliable ember kiss, a cinder storm that ignites a whole zone, wildfire reckoning that reaps the flames and the universal ashen breath — ends on the infernal ball, whose meteors stoke every burn hotter.",
            "fire",
            [
                new ClassStanceDef("fire", "Fire", "fire",
                    [
                        "skill:rin:ember-kiss",
                        "skill:rin:contract",
                        "skill:rin:hall",
                        "skill:rin:ashwings"
                    ],
                    "skill:rin:infernal-ball")
            ]),
        new ClassDef(StormcallerId, "Thunder Dragoness",
            "Energy melee berserker: Voltaic Claw builds Static Charge, Chain Lightning sustains the pack, Bloodlust trades safety for hunger and Storm Pull gathers the box — closes on Storm Heart detonating every mark.",
            "energy",
            [
                new ClassStanceDef("energy", "Energy", "energy",
                    [
                        "skill:rynna:claw",
                        "skill:rynna:discharge",
                        "skill:rynna:scale",
                        "skill:rynna:tail"
                    ],
                    "skill:rynna:storm-heart")
            ]),
        new ClassDef(BarbarianId, "Barbarian",
            "Martial artist who chains melee blows and quickens its own steps — combo and mobility, not static area.",
            "physical",
            [
                new ClassStanceDef("physical", "Physical", "physical",
                    [
                        "skill:barbarian:double-jab",
                        "skill:barbarian:rampage",
                        "skill:barbarian:palm-shockwave",
                        "skill:barbarian:war-cry"
                    ],
                    "skill:barbarian:spiritual-outburst")
            ]),
        new ClassDef(NecromancerId, "Necromancer",
            "Ranged soul-reaper: soul rend finishes the wounded, cursed ground rots, an abyssal shade roams and the nightmare beam pierces — reign of shadows harvests every Death Orb at once.",
            "death",
            [
                new ClassStanceDef("death", "Death", "death",
                    [
                        "skill:velvet:strike",
                        "skill:velvet:curse",
                        "skill:velvet:shade",
                        "skill:velvet:nightmare"
                    ],
                    "skill:velvet:plague")
            ])
    ];

    public static readonly IReadOnlyDictionary<string, ClassDef> ById = All.ToDictionary(c => c.Id);

    public static SkillDef[] SkillBar(ClassStanceDef stance) =>
    [
        Skills[stance.Slots[0]],
        Skills[stance.Slots[1]],
        Skills[stance.Slots[2]],
        Skills[stance.Slots[3]],
        Skills[stance.Ultimate]
    ];
}
