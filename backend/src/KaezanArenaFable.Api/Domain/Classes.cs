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
    int FieldSpreadChance = 0, int FieldSpreadGenerations = 0, bool StrikeLeavesField = false);

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

        // Eloa — Seraph (holy ranged). Ranged judgment: precise lance, judgment in sequence,
        // sacred beam, defensive halo and the final absolution in a nova.
        new SkillDef("skill:eloa:lance", "Light Lance", "single", "holy",
            1.15, 2000, 5, 0, 38, 40, 0, null, 0,
            "Hurls a precise lance of light at a target."),
        new SkillDef("skill:eloa:judgment", "Judgment", "barrage", "holy",
            1.20, 9000, 6, 1, 38, 40, 0, null, 0,
            "Summons holy lances that fall in sequence onto the target.",
            Strikes: 3, StrikeIntervalMs: 450, StrikeDelayMs: 300),
        new SkillDef("skill:eloa:radiance", "Sacred Ray", "beam", "holy",
            1.75, 6000, 5, 0, 0, 40, 0, null, 0,
            "Channels a long holy beam in a line."),
        new SkillDef("skill:eloa:halo", "Halo", "ring", "holy",
            1.45, 7000, 0, 2, 0, 50, 0, null, 0,
            "Opens a holy halo around the Seraph, leaving the center untouched.",
            RingInner: 1),
        new SkillDef("skill:eloa:absolution", "Absolution", "nova", "holy",
            2.60, 0, 0, 3, 0, 40, 0, null, 0,
            "Releases all the stored light in a wave of absolution around the Seraph."),

        // Seren — Astral Knight (physical melee). Duelist: precise cut, advance that ricochets
        // between targets, wide arc, offensive stance and the zenith that stuns all around.
        new SkillDef("skill:seren:cut", "Precise Cut", "single", "physical",
            1.35, 1800, 1, 0, 0, 10, 0, null, 0,
            "A clean, decisive cut on the adjacent target."),
        new SkillDef("skill:seren:advance", "Astral Advance", "chain", "physical",
            1.35, 7000, 2, 0, 0, 10, 0, null, 0,
            "Charges the target and the strike carries on to the nearest enemies.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:seren:arc", "Sword Arc", "cone", "physical",
            1.55, 6000, 0, 2, 0, 10, 0, "taunt", GameConfig.MeleeTauntMs,
            "Sweeps a blade arc ahead, cutting everyone in the fan and taunting them: "
            + "ranged enemies drop their retreat and march into melee."),
        new SkillDef("skill:seren:stance", "Zenith Stance", "buff", "support",
            0, 14000, 0, 0, 0, 13, 0, "aegis", 10000,
            "Takes the dueling stance: increases attack and attack speed for 10s."),
        new SkillDef("skill:seren:zenith", "Zenith", "nova", "physical",
            2.60, 0, 0, 3, 0, 35, 500, null, 0,
            "Unleashes the zenith blow in a blast that stuns those nearby."),

        // Velvet — Necromancer (death ranged). Curse and execution: mortal strike, an area curse
        // that rots, nightmare beam, summoned shade and the eternal plague in a nova.
        new SkillDef("skill:velvet:strike", "Mortal Strike", "single", "death",
            1.30, 2000, 4, 0, 11, 18, 0, null, 0,
            "Fires precise deadly energy at a target."),
        new SkillDef("skill:velvet:curse", "Curse", "area", "death",
            0.70, 6000, 5, 1, 11, 18, 0, null, 0,
            "Curses an area; those hit rot over time.",
            DotTicks: 5, DotTickMs: 1000, DotPower: 0.55),
        new SkillDef("skill:velvet:nightmare", "Nightmare", "beam", "death",
            1.80, 8000, 7, 0, 0, 18, 0, null, 0,
            "Projects a long nightmare beam in a line."),
        new SkillDef("skill:velvet:shade", "Abyssal Shade", "summon", "death",
            0, 12000, 0, 1, 0, 18, 0, null, 0,
            "Raises an abyssal shade that pulses death around it for a few seconds.",
            SummonMs: 6000, SummonPulseMs: 800, SummonPower: 0.70, SummonRadius: 1),
        new SkillDef("skill:velvet:plague", "Eternal Plague", "nova", "death",
            1.40, 0, 0, 3, 0, 18, 0, null, 0,
            "Detonates an area plague that keeps corroding those hit.",
            DotTicks: 6, DotTickMs: 1000, DotPower: 0.80),

        // Rin — Pact Succubus (fire ranged). Charm and ember: ember kiss, a contract that ignites
        // in a chain, hall of flames on the ground, ashen wings in a cone and the infernal ball.
        new SkillDef("skill:rin:ember-kiss", "Ember Kiss", "single", "fire",
            1.30, 1700, 5, 0, 4, 7, 0, null, 0,
            "Blows a precise ember kiss at a target — fast, in frantic succession."),
        new SkillDef("skill:rin:contract", "Burning Contract", "chain", "fire",
            1.25, 7000, 6, 0, 4, 7, 0, null, 0,
            "Seals a burning pact: the fire leaps from enemy to enemy, igniting the whole chain.",
            ChainJumps: 5, ChainRange: 4, ChainFalloff: 0.15,
            DotTicks: 4, DotTickMs: 1000, DotPower: 0.30),
        new SkillDef("skill:rin:hall", "Hall of Flames", "field", "fire",
            0, 9000, 6, 1, 4, 7, 0, null, 0,
            "Throws an ember that sets the ground ablaze — and the fire spreads tile by tile across the fight, "
            + "devouring whoever stays in its path.",
            SummonMs: 5000, SummonPulseMs: 700, SummonPower: 0.40, SummonRadius: 1,
            FieldSpreadChance: 45, FieldSpreadGenerations: 3),
        new SkillDef("skill:rin:ashwings", "Ashen Wings", "cone", "fire",
            1.65, 6000, 0, 3, 0, 7, 0, null, 0,
            "Spreads the ashen wings and sweeps a broad wave of fire ahead."),
        new SkillDef("skill:rin:infernal-ball", "Infernal Ball", "barrage", "fire",
            1.50, 0, 7, 2, 4, 7, 400, null, 0,
            "Conducts a ball of meteors that fall in sequence: each impact stuns and ignites the ground, "
            + "and the fire spreads until the whole arena becomes a furnace.",
            Strikes: 4, StrikeIntervalMs: 480, StrikeDelayMs: 380,
            SummonMs: 3200, SummonPulseMs: 600, SummonPower: 0.30, SummonRadius: 1,
            StrikeLeavesField: true, FieldSpreadChance: 38, FieldSpreadGenerations: 2),

        // Rynna — Thunder Dragoness (energy melee). Engages and paralyzes: electric claw that
        // paralyzes, thundering tail in a cone, short discharge in a chain, conductive scale (buff)
        // and the storm heart in a nova. Short range — she's a dragoness of impact, not a ranged mage.
        new SkillDef("skill:rynna:claw", "Electric Claw", "single", "energy",
            1.30, 1800, 1, 0, 0, 12, 300, null, 0,
            "Drives the charged claw into the adjacent target, paralyzing it briefly."),
        new SkillDef("skill:rynna:tail", "Thundering Tail", "cone", "energy",
            1.55, 6000, 0, 2, 0, 12, 0, "taunt", GameConfig.MeleeTauntMs,
            "Spins and lashes with the thundering tail, hitting everyone in the fan and taunting them: "
            + "ranged enemies drop their retreat and march into melee."),
        new SkillDef("skill:rynna:discharge", "Short Discharge", "chain", "energy",
            1.30, 7000, 2, 0, 0, 12, 0, null, 0,
            "Releases a discharge that ricochets between nearby enemies.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:rynna:scale", "Conductive Scale", "buff", "support",
            0, 11000, 0, 0, 0, 41, 0, "atkspeed", 5000,
            "Charges the conductive scale: quickens the cadence of blows for a few seconds."),
        new SkillDef("skill:rynna:storm-heart", "Storm Heart", "nova", "energy",
            2.70, 0, 0, 3, 0, 41, 0, null, 0,
            "Brings the sky down with it: discharges the storm around the dragoness."),

        // Lunara — Ice Archer (ice ranged, bow). Ranged single-target with some AoE: lunar shard that
        // slows, frost leaps in a chain, frozen garden on the ground for kiting, crescent that cuts the
        // target and the new moon. The Lunar Frost trait stacks slow on top of the kit's ice.
        new SkillDef("skill:lunara:cut", "Lunar Cut", "single", "ice",
            1.30, 1800, 5, 0, 29, 42, 0, null, 0,
            "Fires a shard of icy moonlight that wounds and slows the target at range.",
            SlowFactor: 0.7, SlowMs: 1500),
        new SkillDef("skill:lunara:frost-leap", "Frost Leap", "chain", "ice",
            1.30, 7000, 5, 0, 29, 42, 0, null, 0,
            "Leaps between enemies, leaving frost on each one.",
            ChainJumps: 3, ChainRange: 4, ChainFalloff: 0.25,
            SlowFactor: 0.7, SlowMs: 1200),
        new SkillDef("skill:lunara:garden", "Frozen Garden", "field", "ice",
            0, 10000, 5, 0, 37, 44, 0, null, 0,
            "Blooms a garden of ice on the ground, slowing and wounding whoever stays in it.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.35, SummonRadius: 1,
            SlowFactor: 0.5, SlowMs: 1500),
        new SkillDef("skill:lunara:crescent", "Crescent", "area", "ice",
            1.45, 7000, 5, 1, 29, 44, 0, null, 0,
            "Hurls a crescent of ice over the target, slicing and slowing around it.",
            SlowFactor: 0.7, SlowMs: 1200),
        new SkillDef("skill:lunara:new-moon", "New Moon", "nova", "ice",
            2.50, 0, 0, 3, 0, 44, 0, null, 0,
            "Summons the new moon: a wave of absolute cold that freezes the step of everyone around.",
            SlowFactor: 0.6, SlowMs: 2000),

        // Gaia — Monolith Archer (earth ranged, bow). Mineral ranger: stone arrow, monolith fall in
        // an area that stuns, roots that bind to the ground, shards in a cone and the tectonic rain.
        // The Mineral Eye trait rewards keeping distance — roots help with that.
        // MG-06: Gaia was consistently the slowest archer in hunt (Lunara kites faster at every tier;
        // the gap grows T3 +8% → T4 +17%). Her single-target cadence was the outlier (2200ms vs 1800
        // for the rest): arrow 2200→1800 speeds up her hunt without touching effective damage (capped
        // by mob HP) or the `prey` trait.
        new SkillDef("skill:gaia:arrow", "Mineral Arrow", "single", "earth",
            1.30, 1800, 5, 0, 30, 46, 0, null, 0,
            "Fires a petrified stone arrow, true from afar."),
        new SkillDef("skill:gaia:monolith", "Monolith Fall", "area", "earth",
            1.45, 4000, 7, 2, 30, 46, 300, null, 0,
            "Drops a monolith onto the target area, stunning whoever is beneath it."),
        new SkillDef("skill:gaia:roots", "Binding Roots", "field", "earth",
            0, 9000, 6, 0, 30, 46, 0, null, 0,
            "Sprouts stone roots from the ground, binding and wounding whoever steps on them.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.40, SummonRadius: 1,
            SlowFactor: 0.4, SlowMs: 2000),
        new SkillDef("skill:gaia:shards", "Stone Shards", "cone", "earth",
            1.55, 6000, 0, 3, 0, 46, 0, null, 0,
            "Splinters the rock ahead in a broad burst of shards."),
        new SkillDef("skill:gaia:tectonic", "Tectonic Rain", "barrage", "earth",
            1.45, 0, 7, 2, 30, 46, 300, null, 0,
            "Tears the crust and makes tectonic stones fall in sequence, stunning the survivors.",
            Strikes: 3, StrikeIntervalMs: 450, StrikeDelayMs: 400),

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
            "Physical melee duelist: precise cut, advance that ricochets between targets, sword arc and the dueling stance — closes on the zenith that stuns all around.",
            "physical",
            [
                new ClassStanceDef("physical", "Physical", "physical",
                    [
                        "skill:seren:cut",
                        "skill:seren:advance",
                        "skill:seren:arc",
                        "skill:seren:stance"
                    ],
                    "skill:seren:zenith")
            ]),
        new ClassDef(OracleId, "Seraph",
            "Ranged seraph of light: precise lance, judgment in sequence, sacred ray and defensive halo — ends on absolution in a nova.",
            "holy",
            [
                new ClassStanceDef("holy", "Holy", "holy",
                    [
                        "skill:eloa:lance",
                        "skill:eloa:judgment",
                        "skill:eloa:radiance",
                        "skill:eloa:halo"
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
            "Ranged ice archer, all mobility and slow: lunar shard, frost leaps in a chain, frozen garden for kiting and crescent over the target — ends on the new moon that locks everyone around.",
            "ice",
            [
                new ClassStanceDef("ice", "Ice", "ice",
                    [
                        "skill:lunara:cut",
                        "skill:lunara:frost-leap",
                        "skill:lunara:garden",
                        "skill:lunara:crescent"
                    ],
                    "skill:lunara:new-moon")
            ]),
        new ClassDef(ShamanId, "Monolith Archer",
            "Ranged mineral archer: stone arrow, monolith fall that stuns, roots that bind and shards in a cone — closes on the tectonic rain.",
            "earth",
            [
                new ClassStanceDef("earth", "Earth", "earth",
                    [
                        "skill:gaia:arrow",
                        "skill:gaia:monolith",
                        "skill:gaia:roots",
                        "skill:gaia:shards"
                    ],
                    "skill:gaia:tectonic")
            ]),
        new ClassDef(PyromancerId, "Pact Succubus",
            "Ranged fire conjurer: ember kiss, a contract that ignites in a chain, hall of flames on the ground and ashen wings in a cone — ends on the infernal ball.",
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
            "Energy melee dragoness: a claw that paralyzes, thundering tail in a cone, discharge in a chain and the conductive scale that hastes — closes on the storm heart.",
            "energy",
            [
                new ClassStanceDef("energy", "Energy", "energy",
                    [
                        "skill:rynna:claw",
                        "skill:rynna:tail",
                        "skill:rynna:discharge",
                        "skill:rynna:scale"
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
            "Ranged death conjurer: mortal strike, an area curse that rots, nightmare beam and the summoned abyssal shade — ends on the eternal plague.",
            "death",
            [
                new ClassStanceDef("death", "Death", "death",
                    [
                        "skill:velvet:strike",
                        "skill:velvet:curse",
                        "skill:velvet:nightmare",
                        "skill:velvet:shade"
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
