namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// All simulation/meta constants live here (kaezan-arena ArenaConfig convention).
/// Never hardcode a gameplay value anywhere else.
/// </summary>
public static class GameConfig
{
    // ---- simulation ----
    public const int TickMs = 100;
    // G-01: 250→340 (passo 400ms→~294ms/tile) pro ritmo de hunt; mantém folga acima de MinStepMs.
    public const int PlayerBaseSpeed = 340;
    // G-01: revisado e mantido em 2 — mobs já andam a 430–625ms vs player ~294ms, kita bem.
    public const int MonsterSpeedMultiplier = 2;
    public const int GroundFriction = 100;
    public const double DiagonalStepFactor = 1.4;
    // G-01: 160→140, mais teto pra haste/move-speed sem cruzar o piso no passo base.
    public const int MinStepMs = 140;
    public const int MaxStepMs = 1400;
    // G-01: 80→130, suaviza virar/parar (buffer em SetMoveDirection + chain em TickPlayerMovement).
    public const int StepGraceMs = 130;
    public const int MonsterAggroRange = 8;
    public const int AggroDropRange = 12;
    public const int AggroDropOutOfRangeMs = 4000;
    public const int AggroDropNoLosMs = 6000;
    public const int MonsterWanderIntervalMs = 1600;
    // MG-02: supersedido por RoleTuning.BaseAutoAttackMs (velocidade de auto agora é por papel).
    // Mantido como referência histórica do baseline pré-papéis; não usar no tick.
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

    // G-10: automações do HELPER (estilo autoplay de gacha). Tudo determinístico no tick.
    // Auto-heal: usa a poção da run quando a vida cai abaixo deste percentual (configurável na UI;
    // a poção respeita seus próprios cargas/cooldown). 50% é o default; faixa 10..90.
    public const int AutoHelperHealPctDefault = 50;
    public const int AutoHelperHealPctMin = 10;
    public const int AutoHelperHealPctMax = 90;
    public static int ClampHealPct(int pct) => Math.Clamp(pct, AutoHelperHealPctMin, AutoHelperHealPctMax);
    // Auto-pick de carta: na oferta, pega sozinho a de maior raridade (echo>rare>common; desempate
    // por ordem estável). Pequeno atraso pra a oferta piscar na tela antes de resolver. Ligado por default.
    public const int AutoHelperAutoCardsFlag = 16;
    public const int AutoCardPickDelayMs = 700;
    // Auto-loot ("cavebot" do helper): caminha sozinho até o baú/altar ativo mais próximo, abre e
    // repete; sem mais nada pra coletar, segue pra saída — sempre lutando no caminho. Ligado por
    // default. Não há modo "rush/skip" de propósito (não incentivar pular o mapa).
    //   off  = pathing desligado (combate normal: stand/follow/avoid governam o movimento)
    //   loot = explora coletando, depois sai
    public const string AutoHelperNavOff = "off";
    public const string AutoHelperNavLoot = "loot";
    // Espera ~1s no início de cada andar antes de começar a andar sozinho (a tela carrega primeiro).
    public const int AutoLootStartDelayMs = 1000;
    // bit do auto-heal no bitmask de flags do comando ToggleAutoHelper (1=target,2=skills,4=ult).
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
    // MVP/dificuldade: baixado de 0.35 — recebíamos dano demais. Sobe pra punir mais.
    public const double MonsterDamageTuning = 0.26;
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

    // ---- authored monsters (admin content) ----
    public const double AuthoredModifierMin = 0.65;
    public const double AuthoredModifierMax = 1.50;
    public const int AuthoredResistanceMin = -100;
    public const int AuthoredResistanceMax = 100;

    /// <summary>
    /// Direct arena-scale baselines (Health, Damage, Armor, Speed, Experience). Authored bosses use
    /// these values and never receive the legacy BossHpScale multiplier; <see cref="MonsterDamageTuning"/>
    /// is inert for them (eles têm <c>StatMult=1</c>), então a alavanca real de dano de mob é a coluna
    /// <c>Damage</c> aqui.
    /// MG-08 (calibração por simulador, tools/BalanceSim): a coluna <c>Health</c> foi reescalada para
    /// bater os alvos de TTK em ciclos de ação (comum ~3 · elite ~6 · boss ~12) com gear do mesmo tier,
    /// e <c>Damage</c> foi baixado para deaths ~0 (mage/archer) — boss nunca &lt; 8 ciclos, sem one-shot
    /// de boss/elite. Cada número justificado pelo sweep (docs/balance/mg08_before.csv→mg08_after.csv).
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
        new("balanced", "Equilibrado", "Sem desvio do baseline.", 1, 1, 1, 1),
        new("tank", "Resistente", "Mais vida, menos dano e mobilidade.", 1.25, 0.85, 0.90, 0.90),
        new("glass", "Canhao de vidro", "Mais dano, menos vida.", 0.80, 1.20, 1, 1.05),
        new("swift", "Veloz", "Move e ataca mais rapido, com menos impacto.", 0.90, 0.90, 1.20, 1.15),
        new("caster", "Conjurador", "Ataques fortes e ritmo deliberado.", 0.90, 1.10, 0.95, 0.95),
    ];

    public static readonly MonsterElementProfile[] MonsterElementProfiles =
    [
        new("physical", "Fisico", 1, 0, null),
        new("fire", "Fogo", 16, 4, "fire"),
        new("ice", "Gelo", 44, 29, "freeze"),
        new("energy", "Energia", 12, 5, "energy"),
        new("earth", "Terra", 17, 30, "poison"),
        new("holy", "Sagrado", 40, 31, "dazzle"),
        new("death", "Morte", 18, 11, "curse"),
    ];

    public static readonly MonsterBehaviorProfile[] MonsterBehaviorProfiles =
    [
        new("bruiser", "Brutamontes", "Pressao corpo a corpo com golpes de pesos diferentes.", 1, 85,
        [
            new("melee", 1, 0, 0, 0, false, 1700, 100, 0.72, 1.08, false, 0),
            new("spell", 1, 1, 0, 0, false, 3200, 28, 0.48, 0.78, true, 0),
        ]),
        new("skirmisher", "Escaramucador", "Ataques curtos, rapidos e menos previsiveis.", 1, 65,
        [
            new("melee", 1, 0, 0, 0, false, 1150, 82, 0.52, 0.88, false, 0),
            new("melee", 1, 0, 0, 0, false, 2300, 45, 0.75, 1.12, true, 0),
        ]),
        new("ranger", "Atirador", "Mantem distancia e alterna disparos fisicos e elementais.", 5, 90,
        [
            new("spell", 5, 0, 0, 0, true, 1750, 88, 0.62, 1.02, false, 0),
            new("spell", 5, 0, 0, 0, true, 2850, 52, 0.78, 1.18, true, 0),
        ]),
        new("artillery", "Artilharia", "Ataques lentos em area com alto impacto.", 6, 95,
        [
            new("spell", 6, 2, 0, 0, true, 2700, 78, 0.78, 1.22, true, 0),
            new("spell", 6, 3, 0, 0, true, 4400, 35, 0.95, 1.38, true, 0),
        ]),
        new("breather", "Soprador", "Combina mordida e cone elemental.", 2, 85,
        [
            new("melee", 1, 0, 0, 0, false, 1750, 92, 0.60, 0.96, false, 0),
            new("spell", 0, 0, 4, 2, false, 3100, 62, 0.72, 1.18, true, 0),
        ]),
        new("controller", "Controlador", "Dano moderado com condicao elemental ocasional.", 4, 88,
        [
            new("spell", 4, 0, 0, 0, true, 1850, 86, 0.52, 0.86, true, 0),
            new("spell", 4, 1, 0, 0, true, 3600, 42, 0.40, 0.72, true, 0.75),
        ]),
        new("support", "Sustentacao", "Pressao leve com cura propria intermitente.", 4, 90,
        [
            new("spell", 4, 0, 0, 0, true, 2050, 88, 0.55, 0.90, true, 0),
        ], 0.07, 4200),
        new("juggernaut", "Juggernaut", "Lento, resistente e perigoso quando conecta.", 1, 95,
        [
            new("melee", 1, 0, 0, 0, false, 2300, 100, 0.82, 1.28, false, 0),
            new("spell", 1, 2, 0, 0, false, 4200, 32, 0.60, 1.02, true, 0),
        ]),

        // ---- G-08B: arquétipos novos ----
        // swarm: instâncias baratas e rápidas; custo de spawn 1 (dobra a contagem por sala) = pressão numérica.
        new("swarm", "Enxame", "Lacaios baratos e velozes que pressionam pelo número.", 1, 60,
        [
            new("melee", 1, 0, 0, 0, false, 850, 90, 0.28, 0.50, false, 0),
        ], SpawnCost: 1),
        // summoner: mantém distância e conjura Ecoídes sem parar (reusa TickMonsterSummons).
        new("summoner", "Invocador", "Recua e conjura lacaios de eco sem descanso.", 5, 88,
        [
            new("spell", 5, 0, 0, 0, true, 2300, 70, 0.46, 0.82, true, 0),
        ], SummonSpecies: "monster:t1-echoides", SummonCount: 2, SummonChance: 70, SummonIntervalMs: 5200, SummonMax: 4),
        // tanque-de-postura: couraça que só cede dano útil depois do Echo Break (PostureScale alimenta a barra).
        new("posture-tank", "Sentinela de Postura", "Couraça impassível: só abre brecha de dano no Echo Break.", 1, 95,
        [
            new("melee", 1, 0, 0, 0, false, 2100, 100, 0.70, 1.12, false, 0),
            new("spell", 1, 1, 0, 0, false, 4000, 30, 0.55, 0.95, true, 0),
        ], PostureScale: 0.55),
        // charger: recua e investe num dash brutal (MonsterAttackPattern.Kind = "charge").
        new("charger", "Investida", "Ronda à distância e investe com um avanço brutal.", 2, 65,
        [
            new("melee", 1, 0, 0, 0, false, 1600, 80, 0.52, 0.84, false, 0),
            new("charge", 6, 0, 0, 0, false, 4200, 85, 1.05, 1.55, false, 0),
        ]),
        // bomber/suicida: corre até o alvo e explode em área ao morrer perto dele.
        new("bomber", "Detonador", "Corre até o alvo e se desfaz numa explosão de eco.", 1, 45,
        [
            new("melee", 1, 0, 0, 0, false, 1200, 70, 0.22, 0.42, false, 0),
        ], ExplodeRadius: 1, ExplodeDamageScale: 1.7),
        // escudeiro: blinda o aliado mais ferido por perto → força o helper a focá-lo primeiro.
        new("shielder", "Portador de Eco", "Ergue barreiras em aliados e força o foco sobre si.", 5, 90,
        [
            new("spell", 5, 0, 0, 0, true, 2600, 55, 0.40, 0.72, true, 0),
        ], ShieldRadius: 4, ShieldFraction: 0.35, ShieldIntervalMs: 3500),
    ];

    /// <summary>G-08B: lookup O(1) por id para o tick ler campos de comportamento (summon/posture/explode/shield).</summary>
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
    public const int CardRerollsPerRun = 2;

    // ---- G-04: framework de cartas (raridade + mecânica) ----
    /// <summary>Eco define a run; não empilha como os status.</summary>
    public const int EchoMaxStacks = 1;
    public static int MaxStacksForRarity(string rarity) =>
        rarity == Cards.Echo ? EchoMaxStacks : MaxCardStacks;

    // ---- G-06: cadência (beats fixos) ----
    // Level-up dá um status pequeno automático (drip de dopamina, sem abrir tela); as escolhas
    // pesadas ficam em beats antecipáveis: derrotar um elite, limpar um andar e a sala Santuário de
    // Eco. O teto abaixo mantém ~6-9 escolhas por run, e a raridade escala com o progresso.
    /// <summary>Teto de escolhas de carta por run (alvo ~6-9). Throttle dos beats.</summary>
    public const int MaxCardChoicesPerRun = 9;
    /// <summary>Salas Santuário de Eco por andar (beat garantido, sinalizado no minimapa).</summary>
    public const int SanctuariesPerFloor = 1;
    /// <summary>Limpar um andar (descer a escada) também concede um beat de escolha.</summary>
    public const bool OfferChoiceOnFloorClear = true;

    /// <summary>
    /// Peso de amostragem da oferta por raridade, escalado pelo progresso da run em [0,1]
    /// (fração de escolhas já concedidas). Começo favorece comum/raro (monta a engine); fim
    /// favorece raro/eco (define a run). Determinístico: só interpola pesos fixos.
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

    // Eco Sobrecarregado (echo_surge): carga de ult por acerto direto, por stack.
    public const double CardEchoSurgeGaugePerHit = 4;
    // Golpe Duplo (double_strike): a cada N acertos, um golpe extra (fração do ataque, por stack).
    public const int CardDoubleStrikeEvery = 3;
    public const double CardDoubleStrikeDamageMult = 0.60;
    // Detonação (detonate): condição expira → estouro em área (fração do ataque, por stack).
    public const int CardDetonateRadius = 1;
    public const double CardDetonateDamageMult = 0.80;
    // Colheita do Pesadelo (harvest, Velvet): espectro que pulsa dano ao matar sob Decadência.
    public const int CardHarvestMaxSpectres = 5;
    public const double CardHarvestSpectreDamageMult = 0.50;
    public const int CardHarvestSpectreRadius = 1;
    public const int CardHarvestSpectrePulseMs = 1000;
    public const int CardHarvestSpectreDurationMs = 6000;
    public const int CardHarvestSpectreFx = 18; // mort area fx
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
    public const int SpawnBudgetBase = 14;
    public const double SpawnBudgetTierGrowth = 0.55;

    // ---- G-07: tipos de sala (grafo + bifurcação risco/recompensa) ----
    /// <summary>Sala de elite (detour de risco) força elites até este teto; o resto do orçamento vira comum.</summary>
    public const int EliteRoomMaxElites = 2;
    /// <summary>Sala de evento/risco (hazard): orçamento de spawn ampliado por este fator (swarm = o risco).</summary>
    public const double HazardBudgetMult = 1.35;
    /// <summary>Miniboss só aparece em andares com pelo menos este número de salas (evita lotar mapas pequenos).</summary>
    public const int MiniBossMinRooms = 6;
    /// <summary>HP do miniboss = HP do elite × este fator (mini-clímax antes do boss).</summary>
    public const double MiniBossHpScale = 2.4;
    /// <summary>Comuns que escoltam o miniboss.</summary>
    public const int MiniBossEscort = 2;

    // ---- MG-02: papéis (Knight · Mage · Archer) — eixo primário de identidade ----
    // Cada papel dirige dano de auto vs skill, velocidade de auto, range e tamanho de AOE. Estes são
    // os valores SEED (refinados por MG-06/MG-07 via simulador); em MG-05 viram editáveis no admin.
    // Ordens-alvo: auto archer/knight > mage; skill mage > archer > knight; spd archer > knight > mage;
    // range archer > mage > knight; aoe mage > knight > archer.
    public static readonly IReadOnlyDictionary<KaeliRole, RoleTuning> Roles =
        new Dictionary<KaeliRole, RoleTuning>
        {
            //                       AutoDmg SkillDmg AutoMs Range Aoe
            [KaeliRole.Mage]   = new(0.75,   1.15,    2000,  4,    1.00),
            [KaeliRole.Archer] = new(1.15,   0.95,    1400,  5,    0.65),
            [KaeliRole.Knight] = new(1.05,   0.80,    1700,  1,    0.80),
        };

    // ---- MG-04: resize de AOE ----
    // Footprint dos shapes de área. Antes CircleTiles/RingTiles cortavam em raio*1.5 (quase um
    // quadrado); 1.25 dá um diamante mais honesto. Aplicado dentro dos helpers, sem tocar call sites.
    public const double AoeRoundingFactor = 1.25;
    // Raio máximo de uma ultimate ANTES do AoeScale do papel: a ult ainda "estoura" (piso 2 garantido
    // em GameWorld.SkillRadius), mas nenhuma vira tela inteira. Mage ult fica 3, archer/knight ~2.
    public const int UltimateRadiusCap = 3;

    // ---- player damage ----
    /// <summary>Multiplicador global do dano do player (autos + skills). MVP/dificuldade: dávamos pouco dano.</summary>
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
        new("low", "Baixa"),
        new("moderate", "Moderada"),
        new("high", "Alta"),
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

    // ---- sustain baseline (ponte até os sets: todo Kaeli tem um mínimo, sem depender de card) ----
    /// <summary>Regen passivo por segundo como fração da vida máxima, mesmo sem o card de regen.</summary>
    public const double BaselineRegenPctPerSec = 0.006;
    /// <summary>+regen por run-level (recompensa quem sobe de nível dentro da run).</summary>
    public const double BaselineRegenPctPerRunLevel = 0.0006;
    /// <summary>Life leech mínimo: fração do dano causado que volta como vida, mesmo sem card.</summary>
    public const double BaselineLifesteal = 0.02;

    // ---- drops com função (consumíveis curam ao pegar; lixo vira ouro) ----
    /// <summary>Comida cura esta fração da vida máxima ao ser pega.</summary>
    public const double FoodHealPct = 0.05;
    /// <summary>Poções de vida curam por fração da vida máxima — mais forte a poção, mais cura.</summary>
    public const double PotionHealBasic = 0.15;
    public const double PotionHealStrong = 0.25;
    public const double PotionHealGreat = 0.40;
    /// <summary>Sprite da moeda de ouro (Tibia) usado na animação de loot voando até o player.</summary>
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

    // ---- G-09: baú = altar de Eco / loja da run + amaldiçoados + mímicos + material de gear ----
    /// <summary>Chance de um baú ser, na verdade, um mímico (elite corrompido). Surpresa — oculto do cliente.</summary>
    public const double ChestMimicChance = 0.12;
    /// <summary>Chance de um baú (não-mímico) ser amaldiçoado: Eco forte + emboscada/maldição. Telegrafado.</summary>
    public const double ChestCursedChance = 0.22;
    /// <summary>HP do mímico = HP do elite × este fator (baú-Eco corrompido = mini-clímax).</summary>
    public const double MimicHpScale = 2.0;
    /// <summary>Comuns que emboscam ao abrir um baú amaldiçoado (a maldição em si).</summary>
    public const int CursedChestAmbush = 3;
    /// <summary>Maldição no jogador ao abrir um baú amaldiçoado: lentidão temporária.</summary>
    public const int CursedChestSlowMs = 6000;
    public const double CursedChestSlowFactor = 0.6;
    /// <summary>Custo em ouro de um reroll quando os grátis acabam (a "loja" do altar da run).</summary>
    public const int CardRerollGoldCost = 150;
    /// <summary>Oferta abençoada (baú amaldiçoado): pondera como o fim da run (favorece raro/eco).</summary>
    public const double BlessedOfferProgress = 1.0;

    /// <summary>Material de Eco: ids sintéticos (1 por tier) que fluem pelo inventário da conta para
    /// a tela de equipamento. Fora do catálogo de itens (não-equipável, não-vendável).</summary>
    public const int GearMaterialItemIdBase = 950000;
    public static int GearMaterialItemId(int tier) => GearMaterialItemIdBase + Math.Clamp(tier, 1, 5);
    public static bool IsGearMaterial(int itemId) =>
        itemId > GearMaterialItemIdBase && itemId <= GearMaterialItemIdBase + 5;
    public static string GearMaterialName(int tier) => $"Estilhaço de Eco · T{Math.Clamp(tier, 1, 5)}";
    /// <summary>Chance de um baú comum dropar 1 material de Eco; amaldiçoado/mímico garantem N.</summary>
    public const double ChestMaterialDropChance = 0.45;
    public const int CursedChestMaterialDrops = 2;
    /// <summary>Sprite (gema preciosa) usado na animação do material voando até o jogador.</summary>
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

    // ---- poção de slot (recurso da run, independente do loot dos mobs) ----
    /// <summary>Cargas de poção com que o jogador começa cada run (slot 5).</summary>
    public const int PotionChargesPerRun = 2;
    /// <summary>Cooldown entre usos da poção do slot.</summary>
    public const int PotionCooldownMs = 1500;

    /// <summary>Fração de cura da poção do slot conforme o tier da run (escala como o equipamento).</summary>
    public static double PotionSlotHealFraction(int tier) => tier switch
    {
        <= 2 => PotionHealBasic,
        <= 4 => PotionHealStrong,
        _ => PotionHealGreat,
    };

    /// <summary>Sprite/ícone da poção do slot conforme o tier — casa com a cura.</summary>
    public static int PotionSlotItemId(int tier) => tier switch
    {
        <= 2 => 266, // health potion
        <= 4 => 236, // strong health potion
        _ => 239,    // great health potion
    };
    /// <summary>Palavras que marcam um item como comida (resolvidas para ids em GameData).</summary>
    public static readonly string[] FoodNameWords =
    [
        "meat", "ham", "cheese", "fish", "cookie", "cherry", "corncob", "mushroom", "worm",
        "egg", "bread", "carrot", "apple", "banana", "grape", "melon", "salmon", "mango",
        "pear", "tomato", "strawberry", "blueberry", "bun", "candy", "roll", "rye"
    ];

    // ---- death / rewards ----
    public const double DefeatGoldKeptFraction = 0.5;
    // MVP/teste: recompensa por run inflada. Produção: Base 120, PerTier 40.
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
            "Orc Warlord", 2, 1.35),
        new(3, "Cripta Sombria", "Catacumbas onde os mortos não descansam.",
            ["Skeleton", "Ghoul", "Ghost", "Mummy", "Bonelord"],
            ["Crypt Shambler", "Demon Skeleton", "Witch", "Vampire", "Necromancer", "Banshee"],
            "Black Knight", 3, 1.8),
        new(4, "Covil Escamado", "Ninhos de dragões nas profundezas vulcânicas.",
            ["Minotaur", "Minotaur Archer", "Minotaur Mage", "Minotaur Guard", "Fire Elemental", "Dragon Hatchling"],
            ["Cyclops", "Earth Elemental", "Dragon", "Dragon Lord Hatchling", "Frost Dragon Hatchling"],
            "Dragon Lord", 4, 2.4),
        new(5, "Abismo Ecoante", "Onde os ecos do abismo ganham forma.",
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
    // MVP/teste: economia generosa pra testar conteúdo. Produção: StartingKaeros 4000, Gold 500.
    public const int StartingKaeros = 20000;
    public const int StartingGold = 3000;
    public const int ItemFallbackSalePrice = 5;
    public static readonly Dictionary<int, int> DupeShards = new() { [5] = 50 };
    public static readonly int[] AscensionShardCost = [10, 15, 25, 40, 60, 80]; // A1..A6
    public const int AddonOneAscension = 2;
    public const int AddonTwoAscension = 4;

    // ---- kaeli depth: afinidade / presentes / skins / maestria (refundação 2026-06-12) ----
    public const int AffinityMaxLevel = 10;
    /// <summary>XP necessário para ir do nível N para o N+1.</summary>
    public static long XpForAffinityLevel(int level) => (long)(40 * Math.Pow(level, 1.35));
    /// <summary>+1% ATK e HP por nível de afinidade, aplicado no início da run.</summary>
    public const double AffinityStatBonusPerLevel = 0.01;
    public const long AffinityXpVictory = 50;
    public const long AffinityXpDefeat = 20;
    public const long AffinityXpPerRunLevel = 2;
    /// <summary>Níveis de afinidade que destravam os fragmentos de lore 1..4.</summary>
    public static readonly int[] AffinityLoreLevels = [2, 4, 6, 8];
    /// <summary>Kaeros entregues ao alcançar o nível (marcos de afinidade).</summary>
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

    /// <summary>Kaeros devolvidos por Kaeli removida do roster encontrada numa conta antiga.</summary>
    public const int CutKaeliRefundKaeros = 600;

    // ---- dailies ----
    public const int DailyContractCount = 3;
    // MVP/teste: dailies generosas. Produção: Kaeros 100, Gold 150.
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
    /// <summary>Raw-damage multiplier during stagger, one entry per cycle (caps at the last).</summary>
    public static readonly double[] PostureDamageMultipliers = [2.5, 3.5, 5.0, 6.5];
    /// <summary>Each valid hit during stagger also adds this fraction of the boss max HP...</summary>
    public const double PostureMaxHpBonusPct = 0.015;
    /// <summary>...but no more than once per this internal cooldown (anti multi-hit exploit).</summary>
    public const int PostureMaxHpBonusCooldownMs = 600;

    // ---- F-E: elemental reactions ----
    /// <summary>How long an element "mark" lingers on a target waiting for a second element.</summary>
    public const int ElementMarkDurationMs = 4000;

    // ---- K-04: traits assinatura (uma por Kaeli, estado vivo no tick) ----
    // Uma família mecânica distinta por Kaeli. Os tunáveis principais ficam na TraitDef
    // (Value/Param, amplificados pela maestria via _traitMult); todo o resto (limiares, stacks,
    // durações, raios) mora aqui. Seleção de alvo é sempre determinística (menor distância,
    // desempate por menor id).

    // Eloa — Selo de Julgamento (marca + detonar). Acertos aplicam Pecado; ao chegar a N o alvo
    // fica Julgado e o próximo acerto detona um burst sacro em área pequena e cura a Serafim.
    public const int EloaSinStacksToJudge = 3;
    public const int EloaJudgmentRadius = 1;
    public const int EloaSinDurationMs = 4000;

    // Seren — Disciplina (combo cadence). Acertos consecutivos no MESMO alvo escalam o dano;
    // trocar de alvo ou ficar parada zera. Cada N-ésimo acerto é um Corte Perfeito (crit garantido).
    public const int SerenDisciplineResetMs = 3000;
    public const int SerenPerfectCutEvery = 3;

    // Velvet — Maldição Acumulada (stacks + execução). Cada acerto empilha Decadência (DoT) e
    // sobe o limiar de execução; quanto mais investiu, mais cedo o alvo estoura.
    public const double VelvetThresholdPerStack = 0.02; // +2% de limiar por stack
    public const double VelvetThresholdCap = 0.25;      // limiar máximo (executa < 25% de HP)
    public const int VelvetDecayMaxStacks = 5;
    public const int VelvetDecayTicks = 3;              // duração (em ticks) de cada DoT de decadência
    public const int VelvetDecayTickMs = 2000;
    public const double VelvetDecayDamagePerStack = 0.10; // dano/tick por stack = fração do ataque
    public const int VelvetDecayDurationMs = 5000;      // janela de expiração dos stacks sem refresh

    // Rin — Contágio (incêndio que se propaga). Acertos de fogo incendeiam; o burn salta entre
    // inimigos e cada tick de queimadura cura Rin um pouco (pacto). Value=cura, Param=raio do salto.
    public const int RinContagionIntervalMs = 2000;
    public const double RinContagionBurnPower = 0.30;   // dano/tick do burn = fração do ataque
    public const int RinContagionBurnTicks = 4;
    public const int RinContagionBurnTickMs = 1000;

    // Rynna — Carga Estática (barra de carga). Acertos enchem a carga; cheia, o acerto que a
    // completa vira Descarga (chain curto + paralyze) e o paralyze acelera a ultimate.
    public const double RynnaChargeMax = 100;
    public const double RynnaChargePerHit = 20;         // 5 acertos enchem
    public const double RynnaDischargeDamageMult = 1.5; // ~150% do ataque por alvo
    public const int RynnaDischargeChainJumps = 3;
    public const int RynnaDischargeChainRange = 3;
    public const int RynnaParalyzeMs = 800;
    public const double RynnaParalyzeGaugeBonus = 8;

    // Lunara — Estilhaçar (hit-and-run). Bater em alvo lento dá dano bônus + haste breve; o
    // N-ésimo acerto no lento estilhaça (burst e consome o slow). Value=bônus, Param=dur. do slow.
    public const double LunaraSlowFactor = 0.65;        // 35% de lentidão aplicado pelo gelo
    public const double LunaraHasteFactor = 1.2;
    public const int LunaraHasteMs = 2000;
    public const int LunaraShatterHits = 3;
    public const double LunaraShatterDamageMult = 1.5;

    // Gaia — Presa (perseguir e executar). Marca um alvo; o dano contra a Presa cresce quanto mais
    // a caça dura; quando a Presa morre a marca salta e Gaia ganha cadência. Value=ramp/s, Param=cap.
    public const double GaiaHuntAtkSpeedBonus = 0.20;
    public const int GaiaHuntBonusMs = 3000;

    // ================= G-04B: Ecos por Kaeli (3 × 7) =================
    // Cada Eco (cap de 1 stack) ramifica nos hooks de trait/carta do GameWorld — sem dispatch novo.
    // Win-conditions ancoradas no campo/constante real do trait (SinStacks, _comboHits, DecayStacks,
    // burn DoTs, _staticCharge, SlowUntilMs, _preyId). Determinístico: só _rng/NowMs + desempate por id.

    /// <summary>Escudo de Eco (Eloa Mártir / Velvet Pacto): teto da sobre-vida absorvida, fração da vida máx.</summary>
    public const double EchoShieldCapFraction = 0.60;

    // Eloa — chain-judgment: o estouro semeia Pecado nos atingidos. sentence: Julga mais cedo e
    // cada Julgamento amplia o próximo estouro (acumula até um teto).
    public const int EchoEloaChainSinSeed = 1;
    public const int EchoSentenceStacksToJudge = 2;
    public const double EchoSentenceBurstPerStack = 0.15; // +15% no estouro por Julgamento acumulado
    public const int EchoSentenceMaxStacks = 6;

    // Seren — endless-cadence: ramp sem teto, reset mais severo. perfect-execution: Corte a cada 2º,
    // crit garantido executa alvos fracos. immortal-stance: redução de dano com combo alto.
    public const int EchoEndlessCadenceResetMs = 1200;
    public const int EchoPerfectCutEvery = 2;
    public const double EchoPerfectExecuteHpFraction = 0.15;
    public const int EchoImmortalComboThreshold = 4;
    public const double EchoImmortalDamageReduction = 0.40;

    // Velvet — blood-pact: cada carga de Decadência aplicada vira escudo (fração do dano de Maldição).
    // viral-plague: ao morrer, a Decadência salta com os stacks ao vivo mais próximo (reusa o DoT base).
    public const double EchoBloodPactShieldFraction = 0.50;

    // Rin — pyre: dano cresce com nº de alvos queimando. holocaust: morte em chamas explode em área.
    // wildfire reusa o Contágio (incêndio de qualquer elemento + refresh) sem constante nova.
    public const double EchoPyreDamagePerBurning = 0.08;
    public const double EchoPyreMaxBonus = 0.60;
    public const double EchoHolocaustDamageMult = 1.2;
    public const int EchoHolocaustRadius = 1;

    // Rynna — perpetual-storm: Descarga retém metade da Carga e a Carga enche o dobro. overload:
    // paralyze vira DoT de eletrocussão. thunder-core: gauge da Descarga turbinado + ult devolve Carga.
    public const double EchoPerpetualDischargeRetain = 0.50;
    public const double EchoPerpetualChargeMult = 2.0;
    public const double EchoOverloadDotPower = 0.20;
    public const int EchoOverloadDotTicks = 3;
    public const int EchoOverloadDotTickMs = 400;
    public const double EchoThunderCoreGaugeMult = 3.0;

    // Lunara — eternal-winter: inimigos entram lentos ao ver Lunara, slow mais forte (sem piso).
    // chain-shatter: o Estilhaço salta para lentos próximos. moon-dance: estilhaça já no 2º acerto.
    public const double EchoEternalWinterSlowFactor = 0.45;
    public const int EchoEternalWinterAggroSlowMs = 1500;
    public const int EchoChainShatterRange = 3;
    public const double EchoChainShatterDamageMult = 0.70;
    public const int EchoMoonDanceShatterHits = 2;

    // Gaia — eternal-hunt: ramp e teto da Presa dobrados. pack: 2 Presas + bônus de caça maior.
    // deep-roots: cada acerto na Presa enraíza (slow pesado) e crava um veneno de terra.
    public const double EchoEternalHuntRampMult = 2.0;
    public const double EchoEternalHuntCapMult = 2.0;
    public const int EchoPackHuntBonusMs = 5000;
    public const double EchoDeepRootsSlowFactor = 0.40;
    public const int EchoDeepRootsSlowMs = 1200;
    public const double EchoDeepRootsDotPower = 0.15;
    public const int EchoDeepRootsDotTicks = 3;
    public const int EchoDeepRootsDotTickMs = 1000;

    // ---- G-08B: arquétipos novos + keyword interaction ----
    // charger: o dash anda em linha reta até o alvo (parando a 1 tile), com janela de animação no cliente.
    public const int ChargeMaxTiles = 4;
    public const int ChargeDashMs = 220;
    public const int ChargeFx = 11; // poof de teleporte como rastro do avanço
    // bomber/suicida: estouro em área ao morrer; dano = maior ataque do kit × escala.
    public const int BomberExplodeFx = 35; // grande explosão
    // escudeiro: a barreira em aliado nunca passa desta fração da vida máx do aliado.
    public const double MonsterShieldCapFraction = 0.50;
    public const int MonsterShieldFx = 49; // brilho de proteção
    // Keyword interaction: tags de G-04 que um mob pode resistir/amplificar (% 0-100, negativo = amplifica).
    public static readonly string[] MonsterKeywordTags =
        ["sin", "combo", "curse", "burn", "charge", "frost", "prey", "posture"];
    public const int KeywordResistMin = -100;
    public const int KeywordResistMax = 100;
}

/// <summary>
/// MG-02: tuning de combate por papel. AoeScale é consumido em MG-04 (resize de AOE); aqui em MG-02
/// só AutoDmgMult/SkillDmgMult/BaseAutoAttackMs/AutoRange entram em vigor.
/// </summary>
public sealed record RoleTuning(
    double AutoDmgMult, double SkillDmgMult, int BaseAutoAttackMs, int AutoRange, double AoeScale);

/// <summary>
/// MG-05: linha serializável de <see cref="RoleTuning"/> para persistência/edição no admin. O papel
/// vai como string (legível no JSON e no front); o ContentStore converte de/para o dicionário tipado.
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
    // G-08B: arquétipos novos. Campos data-only — o tick lê o perfil por BehaviorId (sem dispatch novo).
    int SpawnCost = 2,                       // custo no orçamento de spawn da sala (swarm = 1, pressão numérica)
    double PostureScale = 0,                 // >0: mob comum/elite ganha Postura (tanque de postura) escalada por este fator
    string SummonSpecies = "",               // summoner: id/nome autoral conjurado (liga em MonsterAuthoring.Resolve)
    int SummonCount = 1, int SummonChance = 100, int SummonIntervalMs = 0, int SummonMax = 0,
    int ExplodeRadius = 0, double ExplodeDamageScale = 0,  // bomber/suicida: estouro em área ao morrer perto do player
    int ShieldRadius = 0, double ShieldFraction = 0, int ShieldIntervalMs = 0); // escudeiro: barreira em aliado próximo
