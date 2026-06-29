using System.Text.Json.Serialization;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Signature passive that differentiates a Kaeli inside her class. Only 2 numbers (Value, Param) +
/// Tag; both amplified by mastery (_traitMult). The rest of the tunables live in GameConfig.
///
/// Signature kinds (K-04, one archetype per Kaeli — live state in the tick, see GameWorld):
/// - judgment (Eloa): mark+detonate. Value=burst (fraction of the trigger hit), Param=heal (fraction of the burst).
/// - discipline (Seren): combo on the same target. Value=+damage/hit, Param=ramp cap.
/// - decay (Velvet): DoT stacks that raise the execution threshold. Value=execution bonus, Param=base threshold.
/// - contagion (Rin): fire that spreads. Value=burn lifesteal, Param=jump radius.
/// - static_charge (Rynna): charge bar → discharge. Value=gauge bonus (overcharge style), Param=0.
/// - shatter (Lunara): bonus vs slowed + haste + shatter. Value=bonus vs slowed, Param=slow duration (ms).
/// - prey (Gaia): prey mark with ramp per hunt time. Value=ramp/s, Param=ramp cap.
///
/// Reserve kinds still supported (classes without a Kaeli / legacy): executioner, fortress, bulwark,
/// pack_hunter, deadeye, slayer, skill_lifesteal, chiller, overcharge.
/// </summary>
public sealed record TraitDef(
    string Id, string Name, string Kind, double Value, double Param, string Tag,
    string Description);

/// <summary>
/// One outfit a Kaeli can wear (the in-game "skin"). Unlock: "default" (always),
/// "affinity" (UnlockValue = level), "gold" / "kaeros" (UnlockValue = price).
/// Addons (bitmask 0..3) and MountLookType are optional: custom skins (Outfit Studio) can pin
/// addons/mount; when 0 the game falls back to the default behavior (addons by ascension, mount by
/// equipment). Defaults keep the positional constructors of the static roster.
/// </summary>
public sealed record SkinDef(
    string Id, string Name, string Description,
    int LookType, int Head, int Body, int Legs, int Feet,
    string Unlock, int UnlockValue,
    int Addons = 0, int MountLookType = 0);

/// <summary>
/// MG-02: role is the PRIMARY axis of mechanical identity (auto vs skill damage, speed, range, AoE —
/// see <see cref="GameConfig.Roles"/>). The old melee/ranged dichotomy died as a design concept:
/// <see cref="WaifuDef.Weapon"/> is now purely cosmetic (sprite/missile/auto visual).
/// Map: Mage = Eloa/Velvet/Rin; Archer = Gaia/Lunara; Knight = Rynna/Seren.
/// </summary>
public enum KaeliRole { Mage, Archer, Knight }

/// <summary>
/// The Kaeli: identity (lore/personality), signature trait, skins and gift tastes.
/// The combat kit comes from the class (ClassId); Skins[0] is the default look.
/// Lore has 4 fragments, unlocked by affinity (GameConfig.AffinityLoreLevels).
/// </summary>
public sealed record WaifuDef(
    string Id, string Name, string Title, int Rarity, string Element, string Weapon,
    [property: JsonIgnore] KaeliRole Role,
    double BaseAtk, int BaseHp, string ClassId, string Description, string Personality,
    TraitDef Trait,
    IReadOnlyList<string> Lore,
    IReadOnlyList<int> FavoriteGiftItemIds,
    IReadOnlyList<SkinDef> Skins)
{
    [JsonIgnore] public SkinDef DefaultSkin => Skins[0];

    // Default outfit flattened for catalog compatibility (frontend uses lookType/head/...).
    public int LookType => DefaultSkin.LookType;
    public int Head => DefaultSkin.Head;
    public int Body => DefaultSkin.Body;
    public int Legs => DefaultSkin.Legs;
    public int Feet => DefaultSkin.Feet;
}

/// <summary>
/// Refounded roster (Kaelis refoundation, K-02): 7 Kaelis, all 5★ premium, closing the elemental
/// matrix Holy/Physical/Death/Fire/Energy/Ice/Earth. Eloa, Seren, Velvet, Rin, Rynna, Lunara, Gaia.
/// The old Kaelis (Mira, Wren, Sage, Mirai, Neva, Ember, Kaela, Aurora) left the playable pool — old
/// accounts are migrated by the AccountSanitizer (refund in Kaeros + starter). The signature kits per
/// Kaeli come in K-03; here each Kaeli points to the existing class whose element matches her
/// affinity. The traits are provisional (kinds supported by the engine) and will be rewritten in
/// K-04. IDs `waifu:*` and `skin:*` are stable — never rename.
/// </summary>
public static class Waifus
{
    public static readonly IReadOnlyList<WaifuDef> All =
    [
        new("waifu:eloa", "Eloa", "Seraph of Judgment", 5, "holy", "wand", KaeliRole.Mage,
            22, 150, Classes.OracleId,
            "An angel of light who doesn't pray for others' salvation: she enacts it. Where Eloa " +
            "spreads her wings, the night shrinks and whatever hid in it loses the right to stay hidden.",
            "Solemn, kind without being soft, patient like someone who has seen the end and come back. Judges without hatred.",
            new TraitDef("trait:eloa", "Seal of Judgment", "judgment", 1.2, 0.25, "sin",
                "Each hit marks the target with Sin. On reaching 3 stacks it is Judged: the next hit " +
                "consumes the mark in a sacred area burst and heals the Seraph. Spread marks to " +
                "sustain, or focus one target to detonate fast."),
            [
                "They say Eloa was not born: she was summoned, on the first morning after the Long " +
                "Night, by an entire city that sang together without arranging to. She neither " +
                "confirms nor denies it. 'I came when I was needed,' is all she says. 'Like every dawn.'",
                "Her first sentence was on a dead knight who refused to move on. Eloa did not " +
                "destroy him — she sat beside him the whole night and heard what was left to say. " +
                "At dawn, he thanked her and went. 'To judge,' she explains, 'is to let something " +
                "end. The sword is only for those who won't accept the full stop.'",
                "She carries a scale of light where most carry a weapon. On one pan, what a person " +
                "has done; on the other, what they still might do. Almost always the second weighs " +
                "more. 'That's why I almost always grant one more day,' she says. 'Almost.'",
                "When her light goes out for an instant — and sometimes it does — Eloa becomes " +
                "human again, and it is the only time she looks tired. 'The light does not rest,' " +
                "she murmurs. 'But the one who carries it does. Will you stay with me until the sun returns?'"
            ],
            [2917, 3054], // candlestick, silver amulet
            [
                new SkinDef("skin:eloa:default", "Seraph of Judgment",
                    "Robes that take on the color of the day's first light. In the morning, they're hard to look at straight on.",
                    141, 0, 1, 9, 86, "default", 0),
                new SkinDef("skin:eloa:absolution", "Mantle of Absolution",
                    "White as a pre-dawn shift. Eloa only wears it for those she has decided to forgive — or to spare.",
                    140, 1, 1, 0, 9, "gold", 4000),
                new SkinDef("skin:eloa:vigil", "Twilight Vigil",
                    "What she wears when the sentence is harsh. The light comes too, but low, like a candle keeping vigil.",
                    141, 114, 90, 88, 95, "affinity", 6),
            ]),

        new("waifu:seren", "Seren", "Astral Knight", 5, "physical", "melee", KaeliRole.Knight,
            21, 240, Classes.WarriorId,
            "She learned swordsmanship under a sky she swears answered her blows. Every duel, for " +
            "Seren, is a one-sentence conversation — and she always has the last word.",
            "Disciplined to the point of stubbornness, formal in manner, warm only when she lowers her guard.",
            new TraitDef("trait:seren", "Discipline", "discipline", 0.08, 0.40, "combo",
                "Consecutive hits on the same target scale the damage (+8% per hit, up to +40%). " +
                "Switching targets or stopping resets the ramp. Every 3rd hit is a Perfect Cut: a " +
                "guaranteed crit. Commit to a duel — or lose the momentum clearing adds."),
            [
                "The school where she trained had a single rule: the student dueled their own " +
                "shadow until it stopped missing. Seren spent three winters against hers. When the " +
                "shadow finally matched her timing, the master said: 'Now you have a rival who " +
                "never abandons you. Treat her well.'",
                "She refused a post in the honor guard in writing, in one line: 'Honor you put on " +
                "in the morning isn't honor, it's a costume.' They sent the letter back framed. She " +
                "still uses it as a training target — says it improves her aim and her mood.",
                "She has a cut she trains a thousand times a day and has never used in combat. They " +
                "ask why. 'Because the day I need it, I won't have time to think,' she answers. " +
                "'Discipline is memorizing the answer before the question arrives.'",
                "She sleeps with her sword within reach, but far enough that she has to get up for " +
                "it. 'If I wake with it already in my hand, I'll have become someone else,' she " +
                "explains. 'I want to choose to pick up the sword every morning. The day it becomes " +
                "a reflex, I stop.'"
            ],
            [3017, 2920], // silver brooch, torch
            [
                new SkinDef("skin:seren:default", "Astral Knight",
                    "Armor polished to the point of mirroring the constellation she calls her master.",
                    139, 95, 130, 130, 131, "default", 0),
                new SkinDef("skin:seren:vanguard", "Zenith Vanguard",
                    "The formal regalia of ceremonial duels. Seren thinks pomp is a waste — but no one marches as straight.",
                    142, 114, 94, 94, 114, "gold", 4000),
                new SkinDef("skin:seren:eclipse", "Eclipse Blade",
                    "Black as a starless sky. She only wears it against those who deserve the duel taken seriously.",
                    156, 0, 0, 19, 114, "affinity", 6),
            ]),

        new("waifu:velvet", "Velvet", "Herald of the Nightmare", 5, "death", "wand", KaeliRole.Mage,
            22, 150, Classes.NecromancerId,
            "They say she came back from the abyss. The abyss disagrees: it never let her go. Velvet " +
            "walks with a foot in each world — and both worlds pretend it's none of their concern.",
            "Low voice, old-fashioned courtesy, graveyard humor. She knows your name before you say it.",
            new TraitDef("trait:velvet", "Accumulated Curse", "decay", 0.25, 0.15, "curse",
                "Each ability stacks Decay (DoT) on the target and raises its execution threshold " +
                "(executes <15%, +2% per stack up to <25%). The more curse you invest, the sooner " +
                "the target bursts — patience, and then execution."),
            [
                "The convent records say the novice Velvet drowned in the black lake, at twenty-one, " +
                "and was buried the next day. The same records, three pages later, note in trembling " +
                "ink: 'She came to dinner.'",
                "What she saw down there has no name in the tongues above — so Velvet calls it the " +
                "Nightmare, out of politeness. The Nightmare followed her back the way an enormous " +
                "dog follows whoever fed it once. She doesn't drive it off. 'It would be rude. And " +
                "useless. Mostly useless.'",
                "The voices in the velvet: she hears the recently dead, faintly, like conversation " +
                "in the next room. Most just want to finish a sentence left half-said. Velvet writes " +
                "them all in a little black notebook and, when she can, delivers the messages. It is " +
                "the work she chose. No one thanks her. She prefers it that way.",
                "What the abyss wants back is not Velvet — it's what she brought back hidden: the " +
                "last thing she held in her hand as she sank, the thing that brought her back. She " +
                "never opens that hand in public. Anyone paying attention notices: the left glove never comes off."
            ],
            [3114, 3027, 8040], // skull, black pearl, velvet mantle
            [
                new SkinDef("skin:velvet:default", "Black Lake Robes",
                    "The dress she came back from the lake in. It never fully dries; no one has had the nerve to mention it.",
                    269, 114, 90, 90, 112, "default", 0),
                new SkinDef("skin:velvet:crimson", "Crimson Nightmare",
                    "When the Nightmare dreams, it dreams in red. She wakes with the dress like this and returns it to its usual color by noon. Sometimes she doesn't.",
                    269, 94, 113, 94, 94, "kaeros", 1500),
                new SkinDef("skin:velvet:brotherhood", "Brotherhood of the Abyss",
                    "The habit of the order that studies the side below. They granted her the highest rank. Velvet accepted out of politeness — and because the hood is comfortable.",
                    279, 114, 90, 90, 112, "affinity", 8),
            ]),

        new("waifu:rin", "Rin", "Pact Succubus", 5, "fire", "wand", KaeliRole.Mage,
            22, 150, Classes.PyromancerId,
            "She doesn't seduce to deceive: she seduces because it's the most honest way she knows " +
            "to strike a deal. Rin offers fire, warmth and company — and charges exactly what was agreed.",
            "Teasing, witty, loyal in a way no one expects of a demon. She keeps her word.",
            new TraitDef("trait:rin", "Contagion", "contagion", 0.06, 3, "burn",
                "Rin's fire hits ignite the target, and the blaze spreads: the burn jumps to the " +
                "nearest non-burning enemy (when a burning target dies or every 2s). Each burn tick " +
                "heals Rin a little (the pact). Position to chain the fire."),
            [
                "The first pact she sealed was with a sick child who asked for just one more winter " +
                "of life for their grandmother. Rin charged her usual price — a future favor — and " +
                "waited. The grandmother lived six winters. The favor was never collected. 'Some " +
                "deals,' Rin says, 'you close knowing you'll lose. Those are the good ones.'",
                "She keeps a ledger bound in red leather where she records every agreement, every " +
                "clause, every face. No one has ever seen her cheat on a single line. 'A succubus " +
                "who lies once,' she explains, 'spends eternity with no one believing her. Too " +
                "expensive. I prefer clean fire.'",
                "Her charm is real, which is exactly why she warns you before using it. 'I'm turning " +
                "the heat on now,' she says, like someone lighting a candle. 'If you'd rather not, " +
                "just say so. Consent is the one clause I don't negotiate.'",
                "She was exiled from her own plane on a technicality: she refused to break a pact " +
                "the lord of hell ordered annulled. 'It was his signature too,' she shrugged. 'I " +
                "won't burn my name to save his. The fire is mine.'"
            ],
            [2828, 3033], // book, small amethyst
            [
                new SkinDef("skin:rin:default", "Pact Succubus",
                    "Live-ember red, the ledger always a gesture away.",
                    149, 113, 94, 78, 79, "default", 0),
                new SkinDef("skin:rin:contract", "Contract Seal",
                    "The formal attire of the great negotiations. Every buckle is a clause. Rin closes the coat the way she closes a deal.",
                    138, 94, 113, 94, 94, "gold", 4000),
                new SkinDef("skin:rin:ashwing", "Ashen Wings",
                    "What's left when the pact charges dearly: ash and heat. She only shows her true wings to those she trusts enough not to run.",
                    288, 113, 94, 94, 114, "affinity", 6),
            ]),

        new("waifu:rynna", "Rynna", "Thunder Dragoness", 5, "energy", "melee", KaeliRole.Knight,
            21, 220, Classes.StormcallerId,
            "Half dragoness, half storm, wholly impatient. Rynna doesn't wait for the lightning to " +
            "fall from the sky: she is the point where the sky decides to come down and strike first.",
            "Impetuous, loud, generous with loyalty and stingy with patience. Engages first, thinks on the way.",
            new TraitDef("trait:rynna", "Static Charge", "static_charge", 0.30, 0, "charge",
                "Hits fill a Charge bar; when full, the blow that completes it becomes a Discharge — " +
                "a short current that paralyzes nearby targets. Each paralyze speeds up the ultimate, " +
                "and the storm already fills the gauge 30% faster. Time your blows to release at the peak."),
            [
                "She hatched from an egg that fell out of a storm cloud — literally, according to " +
                "the village that found it smoking in a crater. The elders wanted it gone. A " +
                "blacksmith took it home: 'Thunder that lands close,' she said, 'is thunder that " +
                "chose to stay.' She raised Rynna between anvil and spark.",
                "The conductive scale on her back builds charge as she fights. When it cracks, it " +
                "cracks loud. 'I learned to count early,' she laughs. 'One, two, three scales " +
                "glowing — by then the enemy had better already be down, because the fourth burst is on me.'",
                "She hates waiting. The only time she stood still on purpose was three days outside " +
                "a lair, without eating, to make sure the beast inside didn't come out before the " +
                "villagers evacuated. 'I have patience,' she grumbles. 'I just save all of it for " +
                "one thing at a time. Don't ask for two.'",
                "She flies low on purpose, skimming the rooftops, and the residents complain about " +
                "the noise — but they leave the window open. 'It's their way of saying hi without " +
                "admitting it,' she says, banking around again. 'Thunder, too, is just a sky announcing it's arrived.'"
            ],
            [7408, 3029], // wyvern fang, small sapphire
            [
                new SkinDef("skin:rynna:default", "Thunder Dragoness",
                    "Scales that spark in the dark and the stance of someone who's already decided to advance.",
                    156, 86, 38, 38, 39, "default", 0),
                new SkinDef("skin:rynna:tempest", "Tempest Fury",
                    "The war armor of the great hunts. Every plate is a lightning rod. Rynna puts it on and the air grows heavy with charge.",
                    158, 38, 86, 12, 5, "gold", 4000),
                new SkinDef("skin:rynna:skyforged", "Skyforged",
                    "Made by the blacksmith who raised her, from lightning-struck metal. Rynna only wears it on dates that matter — and never says which.",
                    150, 86, 5, 38, 86, "affinity", 6),
            ]),

        new("waifu:lunara", "Lunara", "Lunar Hare", 5, "ice", "bow", KaeliRole.Archer,
            20, 205, Classes.CryomancerId,
            "Fast as a decision and cold as the night that made her. Lunara dances across the ice she " +
            "herself makes, and by the time you notice she passed, you're already slower than she is.",
            "Playful, evasive, melancholy in the quiet hours. She dodges the question and comes back with the answer.",
            new TraitDef("trait:lunara", "Shatter", "shatter", 0.25, 2000, "frost",
                "Lunara's ice slows. Hitting an already-slowed target deals bonus damage and grants " +
                "brief haste; the 3rd hit on the slowed target shatters it in a burst and consumes " +
                "the slow. Apply slow, dive in with haste, shatter and reposition: hit-and-run rewards mobility."),
            [
                "It's said the moon was lonely and made herself a companion of light and frost to " +
                "run with her across the sky. Lunara slipped down to earth on a night of eclipse and " +
                "never found the way back. 'I'm not lost,' she insists. 'I'm exploring. The moon " +
                "knows where I am. She sees everything, remember?'",
                "She hops instead of walking — always has. They measured her leap once: from the " +
                "temple roof to the highest branch in the grove, without touching the ground. 'The " +
                "ground is slow,' she explains. 'And I'm in a hurry to get nowhere. It's the best kind of hurry.'",
                "Where she passes, the ice stays — thin, bright, treacherous. Her pursuers slip; she " +
                "doesn't. 'It's just a matter of respecting the ice,' she says with a wink. 'It lets " +
                "me through because I ask nicely. You lot come stomping. Of course it strikes back.'",
                "On moonless nights, Lunara goes quiet, watching the empty sky. It's the only time " +
                "she stops hopping. 'She comes back tomorrow,' she murmurs, mostly to herself. " +
                "'Always comes back. I just stay here keeping the cold until then. Someone has to keep it.'"
            ],
            [3029, 3027], // small sapphire, black pearl
            [
                new SkinDef("skin:lunara:default", "Lunar Hare",
                    "Moonlight white and blue over snow. Light enough never to sink into her own ice.",
                    252, 9, 86, 87, 94, "default", 0),
                new SkinDef("skin:lunara:crescent", "Crescent Dance",
                    "The attire of festival nights, with ice bells that ring with every leap. Lunara loves it — no one can follow her even so.",
                    150, 9, 28, 90, 115, "kaeros", 1500),
                new SkinDef("skin:lunara:newmoon", "New Moon Veil",
                    "What she wears on dark nights, when the moon vanishes. Silver-gray, quiet. Only those who stay until the moon returns ever see it.",
                    156, 9, 19, 19, 94, "affinity", 6),
            ]),

        new("waifu:gaia", "Gaia", "Monolith Archer", 5, "earth", "bow", KaeliRole.Archer,
            21, 170, Classes.ShamanId,
            "The earth doesn't bloom for Gaia: it rises. Where others see still stone, she sees a " +
            "waiting arrow, a ready root, a monolith that hasn't yet decided to fall.",
            "Patient as rock, sparing with words, unerring as a sentence. She'll wait a lifetime for the right shot if she has to.",
            new TraitDef("trait:gaia", "Prey", "prey", 0.05, 0.30, "prey",
                "Gaia marks a target as Prey; her damage against the Prey grows the longer the hunt " +
                "lasts (+5% per second, up to +30%). When the Prey dies, the mark jumps to the next " +
                "target and Gaia gains attack speed for a few seconds. Pick the priority and pursue."),
            [
                "She was raised by a hermit who carved menhirs atop a plateau. She didn't learn to " +
                "speak before she learned to listen to stone: 'Every monolith has a note,' he would " +
                "say, tapping the rock. 'Whoever hears the note knows where the stone wants to crack.' " +
                "Gaia listened. Today her arrows crack it at the exact point.",
                "Her bow is of a petrified wood that weighs like iron. No one else can draw it. 'It " +
                "isn't strength,' she corrects, rarely. 'It's agreement. The bow bends for whoever it " +
                "thinks will aim true. The others it simply ignores.'",
                "She hunted beasts no one else would face — not for glory, but because they were " +
                "crushing the stone paths the hermit spent his life raising. 'He planted rock where " +
                "no one plants anything,' she says. 'The least I owe is to not let them be torn down.'",
                "When she needs to think, Gaia stacks stones — one on another, in impossible balance, " +
                "no glue, no trick. She leaves the towers behind her. 'So whoever comes after knows " +
                "someone passed and had patience,' she explains. 'The earth remembers. So do I. It's " +
                "what we do best: stay.'"
            ],
            [5897, 5879], // wolf paw, spider silk
            [
                new SkinDef("skin:gaia:default", "Monolith Archer",
                    "Beaten leather the color of clay and the petrified-wood bow on her back, heavy as a sentence.",
                    137, 97, 121, 121, 95, "default", 0),
                new SkinDef("skin:gaia:bedrock", "Bedrock Guard",
                    "The ceremonial garb of the plateau's guardians, with plates of chipped menhir. Gaia rarely takes it out of the chest — but when she does, it's war.",
                    148, 121, 102, 102, 121, "gold", 2500),
                new SkinDef("skin:gaia:quartz", "Quartz Vein",
                    "Pale lines of crystal run through the leather like veins in rock. An affinity gift: she only shows it to those who have learned to wait alongside her.",
                    157, 121, 19, 86, 119, "affinity", 6),
            ]),
    ];

    public static readonly IReadOnlyDictionary<string, WaifuDef> ById = All.ToDictionary(w => w.Id);

    public static readonly IReadOnlyDictionary<string, SkinDef> SkinById =
        All.SelectMany(w => w.Skins).ToDictionary(s => s.Id);

    /// <summary>Skin owner (skin id → waifu).</summary>
    public static readonly IReadOnlyDictionary<string, string> SkinOwner =
        All.SelectMany(w => w.Skins.Select(s => (s.Id, w.Id))).ToDictionary(p => p.Item1, p => p.Item2);

    public const string StarterWaifuId = "waifu:seren";
    public const string FeaturedFiveStarId = "waifu:velvet";

    public static int WeaponRange(string weapon) => weapon switch
    {
        "bow" => GameConfig.BowRange,
        "wand" => GameConfig.WandRange,
        _ => GameConfig.MeleeRange
    };

    public static int WeaponMissile(string weapon, string element) => weapon switch
    {
        "bow" => 3,
        "wand" => element switch
        {
            "fire" => 4, "ice" => 29, "energy" => 5, "earth" => 30, "holy" => 31, "death" => 11,
            _ => 5
        },
        _ => 0
    };
}
