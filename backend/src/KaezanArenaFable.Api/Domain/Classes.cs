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
    // Optional slow rider (qualquer hit aplica slow ao mob, reusa o slow do chiller).
    double SlowFactor = 1, int SlowMs = 0,
    // Chain (shape "chain"): salta entre alvos, dano *= (1-ChainFalloff) por salto.
    int ChainJumps = 0, int ChainRange = 0, double ChainFalloff = 0,
    // Multi-tempo (shape "barrage"): Strikes golpes no ponto alvo, espacados StrikeIntervalMs,
    // primeiro apos StrikeDelayMs (um unico golpe atrasado = Strikes 1 + StrikeDelayMs).
    int Strikes = 0, int StrikeIntervalMs = 0, int StrikeDelayMs = 0,
    // Anel oco (shape "ring"): buraco central de raio RingInner dentro do raio Radius.
    int RingInner = 0);

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
/// Canonical Kaezan World classes. Add a future class by registering its skills and one
/// ClassDef entry; waifus only point at the class id. Cada kit usa um shape diferente por slot
/// (single/area/cone/beam/nova/chain/ring/field/barrage/summon/buff) para que nenhuma habilidade
/// vire so "a mesma area com elemento trocado".
/// </summary>
public static class Classes
{
    public const string WarriorId     = "warrior";
    public const string SentinelId    = "sentinel";
    public const string OracleId      = "oracle";
    public const string ShamanId      = "shaman";
    public const string CryomancerId  = "cryomancer";
    public const string PyromancerId  = "pyromancer";
    public const string StormcallerId = "stormcaller";
    public const string BarbarianId   = "barbarian";
    public const string NecromancerId = "necromancer";
    public const string WizardId = PyromancerId;
    public const string MonkId   = BarbarianId;

    public static readonly IReadOnlyDictionary<string, SkillDef> Skills = new[]
    {
        // Warrior: linha de frente fisica. Controle (taunt/stun/shield) > dano em area —
        // so 1 skill de dano em area no kit (front-sweep).
        new SkillDef("skill:warrior:shield-bash", "Shield Bash", "single", "physical",
            1.30, 2200, 1, 0, 0, 10, 500, null, 0,
            "Golpeia o alvo adjacente com o escudo, atordoando-o."),
        new SkillDef("skill:warrior:challenge", "Chivalrous Challenge", "nova", "physical",
            0, 6000, 0, 3, 0, 10, 0, "taunt", 8000,
            "Provoca os inimigos proximos e os forca a enfrentar a Kaeli."),
        new SkillDef("skill:warrior:front-sweep", "Front Sweep", "cone", "physical",
            1.55, 6000, 0, 2, 0, 10, 0, null, 0,
            "Varre uma onda fisica a frente."),
        new SkillDef("skill:warrior:shield-wall", "Shield Wall", "buff", "physical",
            0, 16000, 0, 0, 0, 35, 0, "shield", 5000,
            "Ergue a guarda, reduzindo a metade o dano recebido por 5s."),
        new SkillDef("skill:warrior:blood-rage", "Blood Rage", "buff", "physical",
            0, 0, 0, 0, 0, 15, 0, "bloodrage", 10000,
            "Aumenta o ataque por 10s."),

        // Sentinel: Holy <-> Physical, mas cada slot da stance usa um shape diferente — alternar
        // a postura troca o JEITO de lutar, nao so o elemento.
        new SkillDef("skill:sentinel:divine-missile", "Divine Missile", "single", "holy",
            1.15, 2000, 4, 0, 38, 40, 0, null, 0,
            "Disparo sagrado preciso contra um alvo."),
        new SkillDef("skill:sentinel:divine-judgment", "Divine Judgment", "barrage", "holy",
            1.20, 9000, 6, 1, 38, 40, 0, null, 0,
            "Convoca lancas sagradas que caem em sequencia sobre o alvo.",
            Strikes: 3, StrikeIntervalMs: 450, StrikeDelayMs: 300),
        new SkillDef("skill:sentinel:divine-beam", "Divine Beam", "beam", "holy",
            1.75, 6000, 5, 0, 0, 40, 0, null, 0,
            "Canaliza um longo feixe sagrado em linha."),
        new SkillDef("skill:sentinel:divine-caldera", "Divine Caldera", "ring", "holy",
            1.45, 7000, 0, 2, 0, 50, 0, null, 0,
            "Cria um halo sagrado ao redor da Kaeli, deixando o centro intocado.",
            RingInner: 1),
        new SkillDef("skill:sentinel:storm-missile", "Storm Missile", "single", "physical",
            1.15, 2000, 4, 0, 12, 10, 0, null, 0,
            "Disparo fisico preciso que desequilibra o alvo.",
            SlowFactor: 0.75, SlowMs: 1200),
        new SkillDef("skill:sentinel:storm-flail", "Storm Flail", "chain", "physical",
            1.30, 7000, 6, 0, 12, 10, 0, null, 0,
            "Arremessa um mangual que ricocheteia entre os inimigos proximos.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:sentinel:storm-slam", "Storm Slam", "nova", "physical",
            1.45, 4000, 0, 2, 0, 10, 400, null, 0,
            "Bate o chao com forca, atordoando quem estiver perto."),
        new SkillDef("skill:sentinel:squall-zone", "Squall Zone", "field", "physical",
            0, 9000, 6, 0, 12, 10, 0, null, 0,
            "Cria uma zona de vento cortante que desacelera e fere quem permanecer dentro.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.45, SummonRadius: 1,
            SlowFactor: 0.6, SlowMs: 1500),
        new SkillDef("skill:sentinel:aegis", "Sentinel Aegis", "buff", "support",
            0, 0, 0, 0, 0, 13, 0, "aegis", 10000,
            "Aumenta ataque e velocidade de ataque por 10s."),

        // Shaman: Ice <-> Earth. Ice controla com lentidao, Earth controla com DoT/armadilha —
        // so o "cone" se repete entre as duas posturas.
        new SkillDef("skill:shaman:frost-shard", "Frost Shard", "single", "ice",
            1.25, 2200, 5, 0, 37, 42, 0, null, 0,
            "Arremessa um estilhaco de gelo que desacelera o alvo.",
            SlowFactor: 0.7, SlowMs: 1500),
        new SkillDef("skill:shaman:avalanche", "Avalanche", "area", "ice",
            1.45, 4000, 7, 2, 37, 42, 0, null, 0,
            "Faz uma avalanche cair sobre a area alvo."),
        new SkillDef("skill:shaman:forked-glacier", "Forked Glacier", "cone", "ice",
            1.55, 6000, 0, 3, 0, 44, 0, null, 0,
            "Projeta uma onda larga de gelo."),
        new SkillDef("skill:shaman:glacial-prison", "Glacial Prison", "field", "ice",
            0, 10000, 6, 0, 37, 44, 0, null, 0,
            "Congela o solo numa area, desacelerando e ferindo quem pisar nela.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.35, SummonRadius: 1,
            SlowFactor: 0.5, SlowMs: 1500),
        new SkillDef("skill:shaman:stone-spikes", "Stone Spikes", "single", "earth",
            1.30, 2200, 5, 0, 39, 46, 300, null, 0,
            "Faz espinhos de pedra emergirem sob o alvo."),
        new SkillDef("skill:shaman:stone-shower", "Stone Shower", "barrage", "earth",
            1.05, 9000, 6, 1, 39, 46, 0, null, 0,
            "Pedras caem em sequencia sobre a area alvo.",
            Strikes: 3, StrikeIntervalMs: 450, StrikeDelayMs: 300),
        new SkillDef("skill:shaman:earth-wave", "Earth Wave", "cone", "earth",
            1.55, 6000, 0, 3, 0, 46, 0, null, 0,
            "Projeta uma onda larga de terra."),
        new SkillDef("skill:shaman:quicksand-trap", "Quicksand Trap", "ring", "earth",
            1.40, 9000, 0, 2, 0, 46, 0, null, 0,
            "Cria um anel de areia movedica ao redor da Kaeli, prendendo quem entrar nele.",
            SlowFactor: 0.55, SlowMs: 2000, RingInner: 1),
        new SkillDef("skill:shaman:natures-embrace", "Nature's Embrace", "buff", "support",
            0, 0, 0, 0, 0, 15, 0, "heal", 0,
            "Restaura uma grande parte da vida da Kaeli."),

        // Pyromancer: fogo puro. Combustion encadeia e incendeia; Inferno Pool deixa poca no
        // chao; o ultimate chove meteoros em sequencia.
        new SkillDef("skill:pyromancer:fireball", "Fireball", "single", "fire",
            1.30, 2200, 5, 0, 4, 7, 0, null, 0,
            "Arremessa uma bola de fogo precisa contra um alvo."),
        new SkillDef("skill:pyromancer:combustion", "Combustion", "chain", "fire",
            1.25, 8000, 6, 0, 4, 7, 0, null, 0,
            "O fogo salta entre os inimigos, incendiando cada um deles.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.20,
            DotTicks: 3, DotTickMs: 1000, DotPower: 0.30),
        new SkillDef("skill:pyromancer:inferno-pool", "Inferno Pool", "field", "fire",
            0, 10000, 6, 0, 4, 7, 0, null, 0,
            "Lanca uma bola de fogo que deixa uma poca em chamas no chao.",
            SummonMs: 6000, SummonPulseMs: 1000, SummonPower: 0.45, SummonRadius: 1),
        new SkillDef("skill:pyromancer:fire-wave", "Great Fire Wave", "cone", "fire",
            1.65, 6000, 0, 3, 0, 7, 0, null, 0,
            "Projeta uma onda larga de fogo."),
        new SkillDef("skill:pyromancer:meteor-barrage", "Meteor Barrage", "barrage", "fire",
            1.50, 0, 7, 2, 4, 7, 400, null, 0,
            "Chama meteoros que caem em sequencia sobre a area alvo, atordoando quem sobreviver.",
            Strikes: 3, StrikeIntervalMs: 500, StrikeDelayMs: 400),

        // Stormcaller: energia pura. Energy Strike paraliza, Static Ring estoura ao redor da
        // Kaeli, Storm Cloud descarrega na area, o ultimate e a tempestade classica.
        new SkillDef("skill:stormcaller:energy-strike", "Energy Strike", "single", "energy",
            1.30, 2200, 5, 0, 5, 38, 300, null, 0,
            "Choque preciso que paralisa brevemente o alvo."),
        new SkillDef("skill:stormcaller:energy-beam", "Great Energy Beam", "beam", "energy",
            1.80, 6000, 8, 0, 0, 12, 0, null, 0,
            "Canaliza um longo feixe de energia."),
        new SkillDef("skill:stormcaller:static-ring", "Static Ring", "ring", "energy",
            1.45, 7000, 0, 2, 0, 38, 0, null, 0,
            "Solta um anel de eletricidade estatica ao redor da Kaeli.",
            RingInner: 1),
        new SkillDef("skill:stormcaller:storm-cloud", "Storm Cloud", "area", "energy",
            1.45, 4000, 7, 2, 5, 38, 0, null, 0,
            "Convoca uma nuvem de tempestade que descarrega sobre a area alvo."),
        new SkillDef("skill:stormcaller:rage-of-the-skies", "Rage of the Skies", "nova", "energy",
            2.70, 0, 0, 3, 0, 41, 0, null, 0,
            "Faz os ceus descarregarem energia ao redor da Kaeli."),

        // Barbarian: marcial corpo-a-corpo. Rampage e o combo de assinatura (salta entre
        // inimigos), War Cry da velocidade — nada de area solida parada como o Warrior.
        new SkillDef("skill:barbarian:double-jab", "Double Jab", "single", "physical",
            1.20, 1500, 1, 0, 0, 1, 0, null, 0,
            "Encadeia golpes rapidos contra um alvo adjacente."),
        new SkillDef("skill:barbarian:rampage", "Rampage", "chain", "physical",
            1.35, 7000, 2, 0, 0, 35, 0, null, 0,
            "Golpeia o alvo e o impacto salta para os inimigos mais proximos.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:barbarian:palm-shockwave", "Palm Shockwave", "cone", "physical",
            1.55, 6000, 0, 2, 0, 1, 350, null, 0,
            "Libera uma onda de ki em leque que atordoa quem for atingido."),
        new SkillDef("skill:barbarian:war-cry", "War Cry", "buff", "physical",
            0, 11000, 0, 0, 0, 35, 0, "haste", 4000,
            "Ruge e acelera os proprios passos por alguns segundos."),
        new SkillDef("skill:barbarian:spiritual-outburst", "Spiritual Outburst", "nova", "physical",
            2.60, 0, 0, 3, 0, 35, 600, null, 0,
            "Libera toda a harmonia interior numa explosao de ki."),

        // Necromancer: Death magic, fixed stance. DoT (Wither/Eternal Suffering), um construto
        // (summon) e um feixe de morte. Cobre single + dot + summon + beam + nova de praga.
        new SkillDef("skill:necromancer:death-strike", "Death Strike", "single", "death",
            1.30, 2000, 4, 0, 11, 18, 0, null, 0,
            "Dispara energia mortal precisa contra um alvo."),
        new SkillDef("skill:necromancer:wither", "Wither", "area", "death",
            0.70, 6000, 5, 1, 11, 18, 0, null, 0,
            "Amaldicoa uma area; os atingidos apodrecem ao longo do tempo.",
            DotTicks: 5, DotTickMs: 1000, DotPower: 0.55),
        new SkillDef("skill:necromancer:death-beam", "Great Death Beam", "beam", "death",
            1.80, 8000, 7, 0, 0, 18, 0, null, 0,
            "Canaliza um longo feixe de morte em linha."),
        new SkillDef("skill:necromancer:bone-construct", "Raise Bone Construct", "summon", "death",
            0, 12000, 0, 1, 0, 18, 0, null, 0,
            "Ergue um construto osseo que pulsa morte ao redor por alguns segundos.",
            SummonMs: 6000, SummonPulseMs: 800, SummonPower: 0.70, SummonRadius: 1),
        new SkillDef("skill:necromancer:eternal-suffering", "Eternal Suffering", "nova", "death",
            1.40, 0, 0, 3, 0, 18, 0, null, 0,
            "Detona uma praga em area que continua corroendo os atingidos.",
            DotTicks: 6, DotTickMs: 1000, DotPower: 0.80),
    }.ToDictionary(s => s.Id);

    public static readonly IReadOnlyList<ClassDef> All =
    [
        new ClassDef(WarriorId, "Warrior",
            "Combatente fisico de linha de frente: provoca, atordoa e se defende mais do que espalha dano em area.",
            "physical",
            [
                new ClassStanceDef("physical", "Physical", "physical",
                    [
                        "skill:warrior:shield-bash",
                        "skill:warrior:challenge",
                        "skill:warrior:front-sweep",
                        "skill:warrior:shield-wall"
                    ],
                    "skill:warrior:blood-rage")
            ]),
        new ClassDef(OracleId, "Oracle",
            "Invocadora sagrada de alcance: julgamentos em sequencia, feixe divino e halo sagrado.",
            "holy",
            [
                new ClassStanceDef("holy", "Holy", "holy",
                    [
                        "skill:sentinel:divine-missile",
                        "skill:sentinel:divine-judgment",
                        "skill:sentinel:divine-beam",
                        "skill:sentinel:divine-caldera"
                    ],
                    "skill:sentinel:aegis")
            ]),
        new ClassDef(SentinelId, "Sentinel",
            "Atiradora fisica de precisao: projeteis que desaceleram, chain, slam e zona de vento.",
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
        new ClassDef(CryomancerId, "Cryomancer",
            "Maga do gelo: desacelera com cada hit, congela o terreno e lanca avalanches.",
            "ice",
            [
                new ClassStanceDef("ice", "Ice", "ice",
                    [
                        "skill:shaman:frost-shard",
                        "skill:shaman:avalanche",
                        "skill:shaman:forked-glacier",
                        "skill:shaman:glacial-prison"
                    ],
                    "skill:shaman:natures-embrace")
            ]),
        new ClassDef(ShamanId, "Shaman",
            "Conjuradora da terra: espinhos que atordoam, chuva de pedras em sequencia e armadilha de areia.",
            "earth",
            [
                new ClassStanceDef("earth", "Earth", "earth",
                    [
                        "skill:shaman:stone-spikes",
                        "skill:shaman:stone-shower",
                        "skill:shaman:earth-wave",
                        "skill:shaman:quicksand-trap"
                    ],
                    "skill:shaman:natures-embrace")
            ]),
        new ClassDef(PyromancerId, "Pyromancer",
            "Maga do fogo: incendeia em cadeia, deixa pocas em chamas no chao e chove meteoros.",
            "fire",
            [
                new ClassStanceDef("fire", "Fire", "fire",
                    [
                        "skill:pyromancer:fireball",
                        "skill:pyromancer:combustion",
                        "skill:pyromancer:inferno-pool",
                        "skill:pyromancer:fire-wave"
                    ],
                    "skill:pyromancer:meteor-barrage")
            ]),
        new ClassDef(StormcallerId, "Stormcaller",
            "Maga da energia: paralisa, estoura aneis eletricos e descarrega tempestades.",
            "energy",
            [
                new ClassStanceDef("energy", "Energy", "energy",
                    [
                        "skill:stormcaller:energy-strike",
                        "skill:stormcaller:energy-beam",
                        "skill:stormcaller:static-ring",
                        "skill:stormcaller:storm-cloud"
                    ],
                    "skill:stormcaller:rage-of-the-skies")
            ]),
        new ClassDef(BarbarianId, "Barbarian",
            "Artista marcial que encadeia golpes corpo a corpo e acelera os proprios passos — combo e mobilidade, nao area parada.",
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
            "Conjuradora da morte: corroi com DoT, ergue construtos e dispara feixes mortais.",
            "death",
            [
                new ClassStanceDef("death", "Death", "death",
                    [
                        "skill:necromancer:death-strike",
                        "skill:necromancer:wither",
                        "skill:necromancer:death-beam",
                        "skill:necromancer:bone-construct"
                    ],
                    "skill:necromancer:eternal-suffering")
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
