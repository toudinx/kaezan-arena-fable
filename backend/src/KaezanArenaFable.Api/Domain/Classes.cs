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
///
/// Refundação Kaelis (K-03): as 7 classes do roster ganharam kits AUTORAIS, um arquétipo claro por
/// Kaeli, ainda 100% data-driven por shape (nenhum dispatch novo no engine). O id da classe é
/// estável e interno (não persistido por conta); o nome de exibição é que casa com a fantasia da
/// Kaeli dona. Mapa id→Kaeli: oracle=Eloa, warrior=Seren, necromancer=Velvet, pyromancer=Rin,
/// stormcaller=Rynna, cryomancer=Lunara, shaman=Gaia. Sentinel e Barbarian ficam como classes de
/// reserva (sem Kaeli ainda) porque ItemAuthoring mapeia tipos de arma → ids de classe por elas.
/// </summary>
public static class Classes
{
    public const string WarriorId     = "warrior";     // Seren — Cavaleira Astral (physical melee)
    public const string SentinelId    = "sentinel";    // reserva (distance/shield) — sem Kaeli
    public const string OracleId      = "oracle";      // Eloa — Serafim (holy ranged)
    public const string ShamanId      = "shaman";      // Gaia — Arqueira dos Monólitos (earth ranged)
    public const string CryomancerId  = "cryomancer";  // Lunara — Arqueira de Gelo (ice ranged, arco)
    public const string PyromancerId  = "pyromancer";  // Rin — Súcubus do Pacto (fire ranged)
    public const string StormcallerId = "stormcaller"; // Rynna — Dragoa do Trovão (energy melee)
    public const string BarbarianId   = "barbarian";   // reserva (fist) — sem Kaeli
    public const string NecromancerId = "necromancer"; // Velvet — Necromancer (death ranged)
    public const string WizardId = PyromancerId;
    public const string MonkId   = BarbarianId;

    public static readonly IReadOnlyDictionary<string, SkillDef> Skills = new[]
    {
        // ============================ ROSTER — KITS AUTORAIS (K-03) ============================

        // Eloa — Serafim (holy ranged). Julgamento à distância: lança precisa, julgamento em
        // sequência, feixe sacro, halo defensivo e a absolvição final em nova.
        new SkillDef("skill:eloa:lance", "Lança de Luz", "single", "holy",
            1.15, 2000, 5, 0, 38, 40, 0, null, 0,
            "Arremessa uma lança de luz precisa contra um alvo."),
        new SkillDef("skill:eloa:judgment", "Julgamento", "barrage", "holy",
            1.20, 9000, 6, 1, 38, 40, 0, null, 0,
            "Convoca lanças sagradas que caem em sequência sobre o alvo.",
            Strikes: 3, StrikeIntervalMs: 450, StrikeDelayMs: 300),
        new SkillDef("skill:eloa:radiance", "Raio Sacro", "beam", "holy",
            1.75, 6000, 5, 0, 0, 40, 0, null, 0,
            "Canaliza um longo feixe sagrado em linha."),
        new SkillDef("skill:eloa:halo", "Halo", "ring", "holy",
            1.45, 7000, 0, 2, 0, 50, 0, null, 0,
            "Abre um halo sagrado ao redor da Serafim, deixando o centro intocado.",
            RingInner: 1),
        new SkillDef("skill:eloa:absolution", "Absolvição", "nova", "holy",
            2.60, 0, 0, 3, 0, 40, 0, null, 0,
            "Libera toda a luz acumulada numa onda de absolvição ao redor da Serafim."),

        // Seren — Cavaleira Astral (physical melee). Duelista: corte preciso, avanço que rica
        // entre alvos, arco amplo, postura ofensiva e o zênite que atordoa em volta.
        new SkillDef("skill:seren:cut", "Corte Preciso", "single", "physical",
            1.35, 1800, 1, 0, 0, 10, 0, null, 0,
            "Um corte limpo e decisivo no alvo adjacente."),
        new SkillDef("skill:seren:advance", "Avanço Astral", "chain", "physical",
            1.35, 7000, 2, 0, 0, 10, 0, null, 0,
            "Investe contra o alvo e o golpe segue para os inimigos mais próximos.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:seren:arc", "Arco de Espada", "cone", "physical",
            1.55, 6000, 0, 2, 0, 10, 0, null, 0,
            "Desenha um arco de lâmina à frente, cortando todos no leque."),
        new SkillDef("skill:seren:stance", "Postura do Zênite", "buff", "support",
            0, 14000, 0, 0, 0, 13, 0, "aegis", 10000,
            "Assume a postura de duelo: aumenta ataque e velocidade de ataque por 10s."),
        new SkillDef("skill:seren:zenith", "Zênite", "nova", "physical",
            2.60, 0, 0, 3, 0, 35, 500, null, 0,
            "Descarrega o golpe do zênite numa explosão que atordoa quem estiver perto."),

        // Velvet — Necromancer (death ranged). Maldição e execução: golpe mortal, maldição
        // em área que apodrece, feixe-pesadelo, sombra invocada e a praga eterna em nova.
        new SkillDef("skill:velvet:strike", "Golpe Mortal", "single", "death",
            1.30, 2000, 4, 0, 11, 18, 0, null, 0,
            "Dispara energia mortal precisa contra um alvo."),
        new SkillDef("skill:velvet:curse", "Maldição", "area", "death",
            0.70, 6000, 5, 1, 11, 18, 0, null, 0,
            "Amaldiçoa uma área; os atingidos apodrecem ao longo do tempo.",
            DotTicks: 5, DotTickMs: 1000, DotPower: 0.55),
        new SkillDef("skill:velvet:nightmare", "Pesadelo", "beam", "death",
            1.80, 8000, 7, 0, 0, 18, 0, null, 0,
            "Projeta um longo feixe de pesadelo em linha."),
        new SkillDef("skill:velvet:shade", "Sombra do Abismo", "summon", "death",
            0, 12000, 0, 1, 0, 18, 0, null, 0,
            "Ergue uma sombra do abismo que pulsa morte ao redor por alguns segundos.",
            SummonMs: 6000, SummonPulseMs: 800, SummonPower: 0.70, SummonRadius: 1),
        new SkillDef("skill:velvet:plague", "Praga Eterna", "nova", "death",
            1.40, 0, 0, 3, 0, 18, 0, null, 0,
            "Detona uma praga em área que continua corroendo os atingidos.",
            DotTicks: 6, DotTickMs: 1000, DotPower: 0.80),

        // Rin — Súcubus do Pacto (fire ranged). Charme e brasa: beijo de brasa, contrato que
        // incendeia em cadeia, salão em chamas no chão, asas de cinza em cone e o baile infernal.
        new SkillDef("skill:rin:ember-kiss", "Beijo de Brasa", "single", "fire",
            1.30, 2200, 5, 0, 4, 7, 0, null, 0,
            "Sopra um beijo de brasa preciso contra um alvo."),
        new SkillDef("skill:rin:contract", "Contrato Ardente", "chain", "fire",
            1.25, 8000, 6, 0, 4, 7, 0, null, 0,
            "Sela um pacto ardente: o fogo salta entre os inimigos, incendiando cada um.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.20,
            DotTicks: 3, DotTickMs: 1000, DotPower: 0.30),
        new SkillDef("skill:rin:hall", "Salão em Chamas", "field", "fire",
            0, 10000, 6, 0, 4, 7, 0, null, 0,
            "Lança uma chama que abre um salão ardente no chão.",
            SummonMs: 6000, SummonPulseMs: 1000, SummonPower: 0.45, SummonRadius: 1),
        new SkillDef("skill:rin:ashwings", "Asas de Cinza", "cone", "fire",
            1.65, 6000, 0, 3, 0, 7, 0, null, 0,
            "Abre as asas de cinza e varre uma onda larga de fogo à frente."),
        new SkillDef("skill:rin:infernal-ball", "Baile Infernal", "barrage", "fire",
            1.50, 0, 7, 2, 4, 7, 400, null, 0,
            "Conduz um baile de meteoros que caem em sequência, atordoando quem sobreviver.",
            Strikes: 3, StrikeIntervalMs: 500, StrikeDelayMs: 400),

        // Rynna — Dragoa do Trovão (energy melee). Engaja e paralisa: garra elétrica que paralisa,
        // cauda trovejante em cone, descarga curta em cadeia, escama condutora (buff) e o coração
        // da tempestade em nova. Curta distância — é uma dragoa de impacto, não maga de longe.
        new SkillDef("skill:rynna:claw", "Garra Elétrica", "single", "energy",
            1.30, 1800, 1, 0, 0, 12, 300, null, 0,
            "Crava a garra carregada no alvo adjacente, paralisando-o brevemente."),
        new SkillDef("skill:rynna:tail", "Cauda Trovejante", "cone", "energy",
            1.55, 6000, 0, 2, 0, 12, 0, null, 0,
            "Gira e açoita com a cauda trovejante, atingindo todos no leque."),
        new SkillDef("skill:rynna:discharge", "Descarga Curta", "chain", "energy",
            1.30, 7000, 2, 0, 0, 12, 0, null, 0,
            "Libera uma descarga que ricocheteia entre os inimigos próximos.",
            ChainJumps: 3, ChainRange: 3, ChainFalloff: 0.25),
        new SkillDef("skill:rynna:scale", "Escama Condutora", "buff", "support",
            0, 11000, 0, 0, 0, 41, 0, "atkspeed", 5000,
            "Carrega a escama condutora: acelera a cadência dos golpes por alguns segundos."),
        new SkillDef("skill:rynna:storm-heart", "Coração da Tempestade", "nova", "energy",
            2.70, 0, 0, 3, 0, 41, 0, null, 0,
            "Faz o céu descer junto: descarrega a tempestade ao redor da dragoa."),

        // Lunara — Arqueira de Gelo (ice ranged, arco). Single-target à distância com algum AOE:
        // lasca lunar que desacelera, saltos de geada em cadeia, jardim congelado no chão para kite,
        // crescente que corta o alvo e a lua nova. O trait Geada Lunar empilha slow em cima do gelo do kit.
        new SkillDef("skill:lunara:cut", "Corte Lunar", "single", "ice",
            1.30, 1800, 5, 0, 29, 42, 0, null, 0,
            "Dispara uma lasca de luar gelado que fere e desacelera o alvo à distância.",
            SlowFactor: 0.7, SlowMs: 1500),
        new SkillDef("skill:lunara:frost-leap", "Saltos de Geada", "chain", "ice",
            1.30, 7000, 5, 0, 29, 42, 0, null, 0,
            "Salta entre os inimigos deixando geada em cada um.",
            ChainJumps: 3, ChainRange: 4, ChainFalloff: 0.25,
            SlowFactor: 0.7, SlowMs: 1200),
        new SkillDef("skill:lunara:garden", "Jardim Congelado", "field", "ice",
            0, 10000, 5, 0, 37, 44, 0, null, 0,
            "Faz florescer um jardim de gelo no chão, desacelerando e ferindo quem permanece nele.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.35, SummonRadius: 1,
            SlowFactor: 0.5, SlowMs: 1500),
        new SkillDef("skill:lunara:crescent", "Crescente", "area", "ice",
            1.45, 7000, 5, 1, 29, 44, 0, null, 0,
            "Arremessa um crescente de gelo sobre o alvo, fatiando e desacelerando em volta dele.",
            SlowFactor: 0.7, SlowMs: 1200),
        new SkillDef("skill:lunara:new-moon", "Lua Nova", "nova", "ice",
            2.50, 0, 0, 3, 0, 44, 0, null, 0,
            "Invoca a lua nova: uma onda de frio absoluto que congela o passo de todos em volta.",
            SlowFactor: 0.6, SlowMs: 2000),

        // Gaia — Arqueira dos Monólitos (earth ranged, arco). Ranger mineral: flecha de pedra,
        // queda de monólito em área que atordoa, raízes que prendem no chão, estilhaços em cone e
        // a chuva tectônica. O trait Olho Mineral premia manter distância — raízes ajudam nisso.
        // MG-06: Gaia era a archer consistentemente mais lenta em hunt (Lunara kita mais rápido em
        // todo tier; gap cresce T3 +8% → T4 +17%). Sua cadência single-target era a fora-do-padrão
        // (2200ms vs 1800 do resto): arrow 2200→1800 acelera a hunt dela sem mexer no dano efetivo
        // (capado pela vida do mob) nem na trait `prey`.
        new SkillDef("skill:gaia:arrow", "Flecha Mineral", "single", "earth",
            1.30, 1800, 5, 0, 30, 46, 0, null, 0,
            "Dispara uma flecha de pedra petrificada, certeira de longe."),
        new SkillDef("skill:gaia:monolith", "Queda de Monólito", "area", "earth",
            1.45, 4000, 7, 2, 30, 46, 300, null, 0,
            "Despenca um monólito sobre a área alvo, atordoando quem estiver embaixo."),
        new SkillDef("skill:gaia:roots", "Raízes Aprisionantes", "field", "earth",
            0, 9000, 6, 0, 30, 46, 0, null, 0,
            "Faz raízes de pedra brotarem do chão, prendendo e ferindo quem pisa nelas.",
            SummonMs: 5000, SummonPulseMs: 1000, SummonPower: 0.40, SummonRadius: 1,
            SlowFactor: 0.4, SlowMs: 2000),
        new SkillDef("skill:gaia:shards", "Estilhaços de Pedra", "cone", "earth",
            1.55, 6000, 0, 3, 0, 46, 0, null, 0,
            "Lasca a rocha à frente numa rajada larga de estilhaços."),
        new SkillDef("skill:gaia:tectonic", "Chuva Tectônica", "barrage", "earth",
            1.45, 0, 7, 2, 30, 46, 300, null, 0,
            "Rasga a crosta e faz pedras tectônicas caírem em sequência, atordoando os sobreviventes.",
            Strikes: 3, StrikeIntervalMs: 450, StrikeDelayMs: 400),

        // ============================ RESERVA — sem Kaeli ainda (ItemAuthoring) ============================
        // Sentinel: atiradora física de precisão (distance/shield). Mantida para o mapa arma→classe
        // e como base de uma futura Kaeli física ranged.
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
    }.ToDictionary(s => s.Id);

    public static readonly IReadOnlyList<ClassDef> All =
    [
        new ClassDef(WarriorId, "Cavaleira Astral",
            "Duelista física corpo a corpo: corte preciso, avanço que rica entre alvos, arco de espada e a postura de duelo — fecha no zênite que atordoa em volta.",
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
        new ClassDef(OracleId, "Serafim",
            "Serafim de luz à distância: lança precisa, julgamento em sequência, raio sacro e halo defensivo — encerra na absolvição em nova.",
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
        new ClassDef(CryomancerId, "Arqueira de Gelo",
            "Arqueira de gelo à distância, toda mobilidade e slow: lasca lunar, saltos de geada em cadeia, jardim congelado para kite e crescente sobre o alvo — encerra na lua nova que prende todos em volta.",
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
        new ClassDef(ShamanId, "Arqueira dos Monólitos",
            "Arqueira mineral à distância: flecha de pedra, queda de monólito que atordoa, raízes que prendem e estilhaços em cone — fecha na chuva tectônica.",
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
        new ClassDef(PyromancerId, "Súcubus do Pacto",
            "Conjuradora de fogo à distância: beijo de brasa, contrato que incendeia em cadeia, salão em chamas no chão e asas de cinza em cone — encerra no baile infernal.",
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
        new ClassDef(StormcallerId, "Dragoa do Trovão",
            "Dragoa de energia corpo a corpo: garra que paralisa, cauda trovejante em cone, descarga em cadeia e a escama condutora que acelera — fecha no coração da tempestade.",
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
            "Conjuradora de morte à distância: golpe mortal, maldição em área que apodrece, feixe-pesadelo e a sombra do abismo invocada — encerra na praga eterna.",
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
