namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// G-04: build card in 3 tiers. <c>Rarity</c> (common|rare|echo) sets offer weight and stack cap;
/// <c>Tags</c> are the synergy keywords (sin/combo/curse/burn/charge/frost/prey/posture/echo/spectre)
/// that tie the card to the engine mechanic and to the Kaelis' traits. <c>Kind</c> is the mechanical
/// effect (rare/echo) read by the card hooks in GameWorld — <c>null</c> = common card, purely a stat
/// multiplier (<c>Stat</c>/<c>Value</c>, backward-compatible with CardValue). Echo is filtered by
/// <c>WaifuId</c> (null = universal).
/// </summary>
public sealed record CardDef(
    string Id, string Name, string Description, string Stat, double Value,
    string Rarity = Cards.Common,
    IReadOnlyList<string>? Tags = null,
    string? Kind = null,
    string? WaifuId = null)
{
    /// <summary>Never-null tags for DTO/query.</summary>
    public IReadOnlyList<string> TagList => Tags ?? [];
}

/// <summary>Passive run cards (kaezan-arena card system). G-04: commons (status) + rares (mechanic)
/// + echoes (defines the run, per Kaeli). Stack cap per rarity (GameConfig.MaxStacksForRarity).</summary>
public static class Cards
{
    public const string Common = "common";
    public const string Rare = "rare";
    public const string Echo = "echo";

    public static readonly IReadOnlyList<CardDef> All =
    [
        // --- commons (status): backward-compatible, only Stat/Value. Tag where there is real synergy. ---
        new("card:atk", "Sharpened Blade", "+12% attack", "atkPercent", 0.12),
        new("card:atkspeed", "Reflexes", "+10% attack speed", "atkSpeedPercent", 0.10,
            Tags: ["combo"]),
        new("card:maxhp", "Vitality", "+15% max HP (heals the gained amount)", "maxHpPercent", 0.15),
        new("card:regen", "Restoring Echo", "+2 HP per second", "regenPerSec", 2,
            Tags: ["echo"]),
        new("card:movespeed", "Quick Steps", "+8% movement speed", "moveSpeedPercent", 0.08),
        new("card:crit", "Deadly Gaze", "+6% crit chance", "critChance", 0.06),
        new("card:element", "Elemental Affinity", "+15% damage of your element", "elementPercent", 0.15),
        new("card:xp", "Echo of Knowledge", "+15% run XP", "xpPercent", 0.15,
            Tags: ["echo"]),
        new("card:gauge", "Echo Flow", "+25% ultimate charge", "gaugePercent", 0.25,
            Tags: ["echo"]),
        new("card:lifesteal", "Vampiric Thirst", "3% of damage becomes HP", "lifesteal", 0.03),
        new("card:armor", "Stone Skin", "-8% damage taken", "damageReduction", 0.08),
        new("card:gold", "Treasure Scent", "+20% gold looted", "goldPercent", 0.20),
        new("card:antidote", "Antidote", "-50% condition damage (poison, burn...)", "conditionResist", 0.50),

        // --- rares (mechanic): prove the per-card effect seam (Kind read by the hooks). ---
        new("card:echo-surge", "Overcharged Echo",
            "Each direct hit fills the ultimate a little more.", "", 0,
            Rarity: Rare, Tags: ["echo"], Kind: "echo_surge"),
        new("card:double-strike", "Double Strike",
            "Every 3 hits, lands an extra strike on the target.", "", 0,
            Rarity: Rare, Tags: ["combo"], Kind: "double_strike"),
        new("card:detonate", "Detonation",
            "When a condition (burn, curse...) expires, it explodes in an area.", "", 0,
            Rarity: Rare, Tags: ["burn", "curse"], Kind: "detonate"),

        // --- echo (defines the run, per Kaeli): 3 per Kaeli (G-04B), each a distinct win condition
        // anchored to the real trait (sin/combo/curse/burn/charge/frost/prey). No new dispatch: each
        // Kind branches in the trait/card hooks of GameWorld. Filtered by the active Kaeli via WaifuId. ---

        // Eloa — Seal of Judgment (judgment/sin).
        new("echo:eloa:chain-judgment", "Chain Judgment",
            "On Judgment, the sacred burst seeds 1 Sin on every enemy hit — the sentence spreads.",
            "", 0, Rarity: Echo, Tags: ["sin"], Kind: "chain_judgment", WaifuId: "waifu:eloa"),
        new("echo:eloa:martyr", "Martyr",
            "Judgment's healing becomes a holy shield above max HP, instead of healing.",
            "", 0, Rarity: Echo, Tags: ["sin", "echo"], Kind: "martyr", WaifuId: "waifu:eloa"),
        new("echo:eloa:sentence", "Supreme Sentence",
            "Judges with only 2 Sins; each Judgment amplifies the next burst (stacks).",
            "", 0, Rarity: Echo, Tags: ["sin"], Kind: "sentence", WaifuId: "waifu:eloa"),

        // Seren — Discipline (discipline/combo).
        new("echo:seren:endless-cadence", "Endless Cadence",
            "Discipline no longer has a damage cap — but the combo resets in a blink if you stop hitting.",
            "", 0, Rarity: Echo, Tags: ["combo"], Kind: "endless_cadence", WaifuId: "waifu:seren"),
        new("echo:seren:perfect-execution", "Perfect Execution",
            "Perfect Cut every 2nd hit; the guaranteed crit executes low-HP targets.",
            "", 0, Rarity: Echo, Tags: ["combo"], Kind: "perfect_execution", WaifuId: "waifu:seren"),
        new("echo:seren:immortal-stance", "Immortal Stance",
            "While the combo is high, Zenith Stance heavily reduces damage taken.",
            "", 0, Rarity: Echo, Tags: ["combo", "posture"], Kind: "immortal_stance", WaifuId: "waifu:seren"),

        // Velvet — Accumulated Curse (decay/curse/spectre).
        new("echo:velvet:harvest", "Nightmare Harvest",
            "An enemy killed under Decay raises a vengeful spectre that pulses damage (max 5).", "", 0,
            Rarity: Echo, Tags: ["curse", "spectre"], Kind: "harvest", WaifuId: "waifu:velvet"),
        new("echo:velvet:blood-pact", "Blood Pact",
            "Velvet does not heal: each Decay stack applied raises a shield from a fraction of Curse damage.",
            "", 0, Rarity: Echo, Tags: ["curse", "echo"], Kind: "blood_pact", WaifuId: "waifu:velvet"),
        new("echo:velvet:viral-plague", "Viral Plague",
            "On death, Decay jumps with all its stacks to the nearest living enemy.",
            "", 0, Rarity: Echo, Tags: ["curse"], Kind: "viral_plague", WaifuId: "waifu:velvet"),

        // Rin — Contagion (contagion/burn).
        new("echo:rin:wildfire", "Wildfire",
            "Every direct hit ignites, of any element; the burn does not expire while any target is aflame.",
            "", 0, Rarity: Echo, Tags: ["burn"], Kind: "wildfire", WaifuId: "waifu:rin"),
        new("echo:rin:pyre", "Pyre",
            "Rin's damage grows with the number of enemies burning at once.",
            "", 0, Rarity: Echo, Tags: ["burn"], Kind: "pyre", WaifuId: "waifu:rin"),
        new("echo:rin:holocaust", "Holocaust",
            "An enemy that dies aflame explodes in a fiery area burst.",
            "", 0, Rarity: Echo, Tags: ["burn"], Kind: "holocaust", WaifuId: "waifu:rin"),

        // Rynna — Static Charge (static_charge/charge).
        new("echo:rynna:perpetual-storm", "Perpetual Storm",
            "Discharge consumes only half the Charge, and the Charge fills twice as fast.",
            "", 0, Rarity: Echo, Tags: ["charge"], Kind: "perpetual_storm", WaifuId: "waifu:rynna"),
        new("echo:rynna:overload", "Overload",
            "Targets paralyzed by Discharge take continuous shock damage.",
            "", 0, Rarity: Echo, Tags: ["charge"], Kind: "overload", WaifuId: "waifu:rynna"),
        new("echo:rynna:thunder-core", "Thunder Core",
            "Discharge fills the ultimate much faster; using the ultimate refunds the Charge in full.",
            "", 0, Rarity: Echo, Tags: ["charge", "echo"], Kind: "thunder_core", WaifuId: "waifu:rynna"),

        // Lunara — Shatter (shatter/frost).
        new("echo:lunara:eternal-winter", "Eternal Winter",
            "Enemies enter already slowed at the sight of Lunara, and the ice slows with no floor.",
            "", 0, Rarity: Echo, Tags: ["frost"], Kind: "eternal_winter", WaifuId: "waifu:lunara"),
        new("echo:lunara:chain-shatter", "Chain Shatter",
            "The Shatter jumps to nearby slowed enemies, passing on the ice burst.",
            "", 0, Rarity: Echo, Tags: ["frost"], Kind: "chain_shatter", WaifuId: "waifu:lunara"),
        new("echo:lunara:moon-dance", "Moon Dance",
            "The Shatter triggers on the 2nd hit, and the trait's haste does not expire in combat.",
            "", 0, Rarity: Echo, Tags: ["frost"], Kind: "moon_dance", WaifuId: "waifu:lunara"),

        // Gaia — Prey (prey).
        new("echo:gaia:eternal-hunt", "Eternal Hunt",
            "The Prey mark ramps much faster and the hunt damage cap is far higher.",
            "", 0, Rarity: Echo, Tags: ["prey"], Kind: "eternal_hunt", WaifuId: "waifu:gaia"),
        new("echo:gaia:pack", "Pack",
            "Gaia marks two Prey at once, and the hunt bonus on execution is greater.",
            "", 0, Rarity: Echo, Tags: ["prey"], Kind: "pack", WaifuId: "waifu:gaia"),
        new("echo:gaia:deep-roots", "Deep Roots",
            "Each hit on the Prey roots it (heavy slow) and drives in an earth poison that corrodes it.",
            "", 0, Rarity: Echo, Tags: ["prey"], Kind: "deep_roots", WaifuId: "waifu:gaia"),
    ];

    public static readonly IReadOnlyDictionary<string, CardDef> ById = All.ToDictionary(c => c.Id);
}
