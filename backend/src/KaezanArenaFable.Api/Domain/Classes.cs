namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Skill shapes stay generic so adding a class only requires data, not a new engine dispatch.
/// FX ids are Tibia CONST_ME_* / CONST_ANI_* values.
/// </summary>
public sealed record SkillDef(
    string Id, string Name, string Shape, string Element, double Power, int CooldownMs,
    int Range, int Radius, int MissileId, int EffectId, int StunMs, string? Buff,
    int BuffMs, string Description);

public sealed record ClassStanceDef(
    string Id, string Name, string Element, string[] Slots, string Ultimate);

public sealed record ClassDef(
    string Id, string Name, string Description, string DefaultStanceId,
    IReadOnlyList<ClassStanceDef> Stances)
{
    public bool CanToggleStance => Stances.Count > 1;

    public ClassStanceDef GetStance(string stanceId) =>
        Stances.FirstOrDefault(s => s.Id == stanceId)
        ?? throw new InvalidOperationException($"stance desconhecida: {Id}/{stanceId}");

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
/// Canonical Kaezan World classes. Add a future fifth class by registering its skills and
/// one ClassDef entry; waifus only point at the class id.
/// </summary>
public static class Classes
{
    public const string WarriorId = "warrior";
    public const string SentinelId = "sentinel";
    public const string ShamanId = "shaman";
    public const string WizardId = "wizard";

    public static readonly IReadOnlyDictionary<string, SkillDef> Skills = new[]
    {
        // Warrior: Physical, fixed stance.
        new SkillDef("skill:warrior:groundshaker", "Groundshaker", "nova", "physical",
            1.40, 8000, 0, 1, 0, 35, 0, null, 0,
            "Abala o chao ao redor com dano fisico."),
        new SkillDef("skill:warrior:challenge", "Chivalrous Challenge", "nova", "physical",
            0, 6000, 0, 3, 0, 10, 0, "taunt", 8000,
            "Provoca os inimigos proximos e os forca a enfrentar a Kaeli."),
        new SkillDef("skill:warrior:front-sweep", "Front Sweep", "cone", "physical",
            1.55, 6000, 0, 6, 0, 10, 0, null, 0,
            "Varre uma onda fisica a frente."),
        new SkillDef("skill:warrior:fierce-berserk", "Fierce Berserk", "nova", "physical",
            2.00, 6000, 0, 1, 0, 10, 0, null, 0,
            "Explode em um ataque fisico curto e brutal."),
        new SkillDef("skill:warrior:blood-rage", "Blood Rage", "buff", "physical",
            0, 0, 0, 0, 0, 15, 0, "bloodrage", 10000,
            "Aumenta o ataque por 10s."),

        // Sentinel: Holy <-> Physical. Mirrored slots keep geometry and cooldown.
        new SkillDef("skill:sentinel:divine-missile", "Divine Missile", "single", "holy",
            1.15, 2000, 4, 0, 38, 40, 0, null, 0,
            "Disparo sagrado preciso contra um alvo."),
        new SkillDef("skill:sentinel:divine-grenade", "Divine Grenade", "area", "holy",
            1.65, 8000, 7, 2, 38, 50, 0, null, 0,
            "Detona energia sagrada em uma area."),
        new SkillDef("skill:sentinel:divine-caldera", "Divine Caldera", "nova", "holy",
            1.45, 4000, 0, 1, 0, 50, 0, null, 0,
            "Cria uma caldeira sagrada ao redor da Kaeli."),
        new SkillDef("skill:sentinel:divine-barrage", "Divine Barrage", "beam", "holy",
            1.75, 6000, 5, 0, 0, 40, 0, null, 0,
            "Dispara uma barragem sagrada em linha."),
        new SkillDef("skill:sentinel:storm-missile", "Storm Missile", "single", "physical",
            1.15, 2000, 4, 0, 12, 10, 0, null, 0,
            "Disparo fisico preciso contra um alvo."),
        new SkillDef("skill:sentinel:storm-grenade", "Storm Grenade", "area", "physical",
            1.65, 8000, 7, 2, 12, 10, 0, null, 0,
            "Detona impacto fisico em uma area."),
        new SkillDef("skill:sentinel:storm-caldera", "Storm Caldera", "nova", "physical",
            1.45, 4000, 0, 1, 0, 10, 0, null, 0,
            "Cria uma caldeira de impacto ao redor da Kaeli."),
        new SkillDef("skill:sentinel:storm-barrage", "Storm Barrage", "beam", "physical",
            1.75, 6000, 5, 0, 0, 10, 0, null, 0,
            "Dispara uma barragem fisica em linha."),
        new SkillDef("skill:sentinel:aegis", "Sentinel Aegis", "buff", "support",
            0, 0, 0, 0, 0, 13, 0, "aegis", 10000,
            "Aumenta ataque e velocidade de ataque por 10s."),

        // Shaman: Ice <-> Earth.
        new SkillDef("skill:shaman:avalanche", "Avalanche", "area", "ice",
            1.45, 4000, 7, 1, 37, 42, 0, null, 0,
            "Faz uma avalanche cair sobre a area alvo."),
        new SkillDef("skill:shaman:forked-glacier", "Forked Glacier", "cone", "ice",
            1.55, 6000, 0, 5, 0, 44, 0, null, 0,
            "Projeta uma onda larga de gelo."),
        new SkillDef("skill:shaman:ice-burst", "Ice Burst", "nova", "ice",
            1.80, 8000, 0, 2, 0, 42, 0, null, 0,
            "Rompe um anel de gelo ao redor da Kaeli."),
        new SkillDef("skill:shaman:strong-ice-wave", "Strong Ice Wave", "cone", "ice",
            1.90, 8000, 0, 3, 0, 44, 0, null, 0,
            "Lanca uma onda curta e intensa de gelo."),
        new SkillDef("skill:shaman:stone-shower", "Stone Shower", "area", "earth",
            1.45, 4000, 7, 1, 39, 46, 0, null, 0,
            "Faz uma chuva de pedras cair sobre a area alvo."),
        new SkillDef("skill:shaman:earth-wave", "Earth Wave", "cone", "earth",
            1.55, 6000, 0, 5, 0, 46, 0, null, 0,
            "Projeta uma onda larga de terra."),
        new SkillDef("skill:shaman:terra-burst", "Terra Burst", "nova", "earth",
            1.80, 8000, 0, 2, 0, 46, 0, null, 0,
            "Rompe um anel de terra ao redor da Kaeli."),
        new SkillDef("skill:shaman:earth-storm", "Earth Storm", "cone", "earth",
            1.90, 8000, 0, 3, 0, 46, 0, null, 0,
            "Lanca uma onda curta e intensa de terra."),
        new SkillDef("skill:shaman:natures-embrace", "Nature's Embrace", "buff", "support",
            0, 0, 0, 0, 0, 15, 0, "heal", 0,
            "Restaura uma grande parte da vida da Kaeli."),

        // Wizard: Energy <-> Fire.
        new SkillDef("skill:wizard:thunderstorm", "Thunderstorm", "area", "energy",
            1.45, 4000, 7, 1, 5, 38, 0, null, 0,
            "Invoca uma tempestade de energia na area alvo."),
        new SkillDef("skill:wizard:energy-wave", "Energy Wave", "cone", "energy",
            1.65, 6000, 0, 5, 0, 38, 0, null, 0,
            "Projeta uma onda larga de energia."),
        new SkillDef("skill:wizard:great-energy-beam", "Great Energy Beam", "beam", "energy",
            1.80, 6000, 8, 0, 0, 12, 0, null, 0,
            "Canaliza um longo feixe de energia."),
        new SkillDef("skill:wizard:rage-of-the-skies", "Rage of the Skies", "nova", "energy",
            2.70, 20000, 0, 6, 0, 41, 0, null, 0,
            "Faz os ceus descarregarem energia ao redor da Kaeli."),
        new SkillDef("skill:wizard:great-fireball", "Great Fireball", "area", "fire",
            1.45, 4000, 7, 1, 4, 7, 0, null, 0,
            "Explode uma grande bola de fogo na area alvo."),
        new SkillDef("skill:wizard:great-fire-wave", "Great Fire Wave", "cone", "fire",
            1.65, 6000, 0, 5, 0, 7, 0, null, 0,
            "Projeta uma onda larga de fogo."),
        new SkillDef("skill:wizard:fire-beam", "Fire Beam", "beam", "fire",
            1.80, 6000, 8, 0, 0, 16, 0, null, 0,
            "Canaliza um longo feixe de fogo."),
        new SkillDef("skill:wizard:hells-core", "Hell's Core", "nova", "fire",
            2.80, 20000, 0, 5, 0, 7, 0, null, 0,
            "Incendeia uma vasta area ao redor da Kaeli."),
        new SkillDef("skill:wizard:aura-exposed", "Aura of Exposed Weakness", "nova", "energy",
            0, 0, 0, 3, 0, 18, 0, "exposed", 16000,
            "Inimigos proximos recebem mais dano por 16s."),
        new SkillDef("skill:wizard:aura-sapped", "Aura of Sapped Strength", "nova", "fire",
            0, 0, 0, 3, 0, 18, 0, "sapped", 16000,
            "Inimigos proximos causam menos dano por 16s."),
    }.ToDictionary(s => s.Id);

    public static readonly IReadOnlyList<ClassDef> All =
    [
        new ClassDef(WarriorId, "Warrior",
            "Combatente fisico de linha de frente, com provocacao e explosoes de curto alcance.",
            "physical",
            [
                new ClassStanceDef("physical", "Physical", "physical",
                    [
                        "skill:warrior:groundshaker",
                        "skill:warrior:challenge",
                        "skill:warrior:front-sweep",
                        "skill:warrior:fierce-berserk"
                    ],
                    "skill:warrior:blood-rage")
            ]),
        new ClassDef(SentinelId, "Sentinel",
            "Atiradora versatil que alterna entre poder sagrado e impacto fisico.",
            "holy",
            [
                new ClassStanceDef("holy", "Holy", "holy",
                    [
                        "skill:sentinel:divine-missile",
                        "skill:sentinel:divine-grenade",
                        "skill:sentinel:divine-caldera",
                        "skill:sentinel:divine-barrage"
                    ],
                    "skill:sentinel:aegis"),
                new ClassStanceDef("physical", "Physical", "physical",
                    [
                        "skill:sentinel:storm-missile",
                        "skill:sentinel:storm-grenade",
                        "skill:sentinel:storm-caldera",
                        "skill:sentinel:storm-barrage"
                    ],
                    "skill:sentinel:aegis")
            ]),
        new ClassDef(ShamanId, "Shaman",
            "Conjuradora natural que alterna gelo e terra e sustenta a propria vida.",
            "ice",
            [
                new ClassStanceDef("ice", "Ice", "ice",
                    [
                        "skill:shaman:avalanche",
                        "skill:shaman:forked-glacier",
                        "skill:shaman:ice-burst",
                        "skill:shaman:strong-ice-wave"
                    ],
                    "skill:shaman:natures-embrace"),
                new ClassStanceDef("earth", "Earth", "earth",
                    [
                        "skill:shaman:stone-shower",
                        "skill:shaman:earth-wave",
                        "skill:shaman:terra-burst",
                        "skill:shaman:earth-storm"
                    ],
                    "skill:shaman:natures-embrace")
            ]),
        new ClassDef(WizardId, "Wizard",
            "Maga de alto impacto que alterna energia e fogo e enfraquece grupos inteiros.",
            "energy",
            [
                new ClassStanceDef("energy", "Energy", "energy",
                    [
                        "skill:wizard:thunderstorm",
                        "skill:wizard:energy-wave",
                        "skill:wizard:great-energy-beam",
                        "skill:wizard:rage-of-the-skies"
                    ],
                    "skill:wizard:aura-exposed"),
                new ClassStanceDef("fire", "Fire", "fire",
                    [
                        "skill:wizard:great-fireball",
                        "skill:wizard:great-fire-wave",
                        "skill:wizard:fire-beam",
                        "skill:wizard:hells-core"
                    ],
                    "skill:wizard:aura-sapped")
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
