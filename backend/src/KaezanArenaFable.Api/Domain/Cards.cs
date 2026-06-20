namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// G-04: carta de build em 3 tiers. <c>Rarity</c> (common|rare|echo) define peso de oferta e cap de
/// stacks; <c>Tags</c> são as keywords de sinergia (sin/combo/curse/burn/charge/frost/prey/posture/
/// echo/spectre) que ligam a carta à mecânica do engine e aos traits das Kaelis. <c>Kind</c> é o
/// efeito-mecânica (raro/eco) lido pelos hooks de carta no GameWorld — <c>null</c> = carta comum,
/// puramente um multiplicador de stat (<c>Stat</c>/<c>Value</c>, retrocompat com CardValue). Eco é
/// filtrado por <c>WaifuId</c> (null = universal).
/// </summary>
public sealed record CardDef(
    string Id, string Name, string Description, string Stat, double Value,
    string Rarity = Cards.Common,
    IReadOnlyList<string>? Tags = null,
    string? Kind = null,
    string? WaifuId = null)
{
    /// <summary>Tags nunca-nulas para DTO/consulta.</summary>
    public IReadOnlyList<string> TagList => Tags ?? [];
}

/// <summary>Passive run cards (kaezan-arena card system). G-04: comuns (status) + raros (mecânica)
/// + ecos (define a run, por Kaeli). Cap de stacks por raridade (GameConfig.MaxStacksForRarity).</summary>
public static class Cards
{
    public const string Common = "common";
    public const string Rare = "rare";
    public const string Echo = "echo";

    public static readonly IReadOnlyList<CardDef> All =
    [
        // --- comuns (status): retrocompatíveis, só Stat/Value. Tag onde há sinergia real. ---
        new("card:atk", "Lâmina Afiada", "+12% de ataque", "atkPercent", 0.12),
        new("card:atkspeed", "Reflexos", "+10% de velocidade de ataque", "atkSpeedPercent", 0.10,
            Tags: ["combo"]),
        new("card:maxhp", "Vitalidade", "+15% de vida máxima (cura o valor ganho)", "maxHpPercent", 0.15),
        new("card:regen", "Eco Restaurador", "+2 de vida por segundo", "regenPerSec", 2,
            Tags: ["echo"]),
        new("card:movespeed", "Passos Rápidos", "+8% de velocidade de movimento", "moveSpeedPercent", 0.08),
        new("card:crit", "Olhar Mortal", "+6% de chance crítica", "critChance", 0.06),
        new("card:element", "Afinidade Elemental", "+15% de dano do seu elemento", "elementPercent", 0.15),
        new("card:xp", "Eco do Saber", "+15% de XP da run", "xpPercent", 0.15,
            Tags: ["echo"]),
        new("card:gauge", "Fluxo de Eco", "+25% de carga de ultimate", "gaugePercent", 0.25,
            Tags: ["echo"]),
        new("card:lifesteal", "Sede Vampírica", "3% do dano vira vida", "lifesteal", 0.03),
        new("card:armor", "Pele de Pedra", "-8% de dano recebido", "damageReduction", 0.08),
        new("card:gold", "Faro de Tesouro", "+20% de ouro saqueado", "goldPercent", 0.20),
        new("card:antidote", "Antídoto", "-50% de dano de condições (veneno, queimadura...)", "conditionResist", 0.50),

        // --- raros (mecânica): provam o seam de efeito-por-carta (Kind lido pelos hooks). ---
        new("card:echo-surge", "Eco Sobrecarregado",
            "Cada acerto direto enche a ultimate um pouco mais.", "", 0,
            Rarity: Rare, Tags: ["echo"], Kind: "echo_surge"),
        new("card:double-strike", "Golpe Duplo",
            "A cada 3 acertos, desfere um golpe extra no alvo.", "", 0,
            Rarity: Rare, Tags: ["combo"], Kind: "double_strike"),
        new("card:detonate", "Detonação",
            "Quando uma condição (queimadura, maldição...) expira, ela explode em área.", "", 0,
            Rarity: Rare, Tags: ["burn", "curse"], Kind: "detonate"),

        // --- eco (define a run, por Kaeli): 3 por Kaeli (G-04B), cada um uma win-condition distinta
        // ancorada no trait real (sin/combo/curse/burn/charge/frost/prey). Sem dispatch novo: cada
        // Kind ramifica nos hooks de trait/carta do GameWorld. Filtrados pela Kaeli ativa via WaifuId. ---

        // Eloa — Selo de Julgamento (judgment/sin).
        new("echo:eloa:chain-judgment", "Julgamento em Cadeia",
            "Ao Julgar, o estouro sacro semeia 1 Pecado em todos os inimigos atingidos — a sentença se espalha.",
            "", 0, Rarity: Echo, Tags: ["sin"], Kind: "chain_judgment", WaifuId: "waifu:eloa"),
        new("echo:eloa:martyr", "Mártir",
            "A cura do Julgamento vira escudo sagrado acima da vida máxima, em vez de curar.",
            "", 0, Rarity: Echo, Tags: ["sin", "echo"], Kind: "martyr", WaifuId: "waifu:eloa"),
        new("echo:eloa:sentence", "Sentença Suprema",
            "Julga com apenas 2 Pecados; cada Julgamento amplia o estouro do próximo (acumula).",
            "", 0, Rarity: Echo, Tags: ["sin"], Kind: "sentence", WaifuId: "waifu:eloa"),

        // Seren — Disciplina (discipline/combo).
        new("echo:seren:endless-cadence", "Cadência Sem Fim",
            "A Disciplina não tem mais teto de dano — mas o combo zera num piscar se você parar de bater.",
            "", 0, Rarity: Echo, Tags: ["combo"], Kind: "endless_cadence", WaifuId: "waifu:seren"),
        new("echo:seren:perfect-execution", "Execução Perfeita",
            "Corte Perfeito a cada 2º acerto; o crítico garantido executa alvos com pouca vida.",
            "", 0, Rarity: Echo, Tags: ["combo"], Kind: "perfect_execution", WaifuId: "waifu:seren"),
        new("echo:seren:immortal-stance", "Postura Imortal",
            "Enquanto o combo estiver alto, a Postura do Zênite reduz fortemente o dano recebido.",
            "", 0, Rarity: Echo, Tags: ["combo", "posture"], Kind: "immortal_stance", WaifuId: "waifu:seren"),

        // Velvet — Maldição Acumulada (decay/curse/spectre).
        new("echo:velvet:harvest", "Colheita do Pesadelo",
            "Inimigo morto sob Decadência ergue um espectro vingativo que pulsa dano (máx 5).", "", 0,
            Rarity: Echo, Tags: ["curse", "spectre"], Kind: "harvest", WaifuId: "waifu:velvet"),
        new("echo:velvet:blood-pact", "Pacto de Sangue",
            "Velvet não cura: cada carga de Decadência aplicada ergue um escudo de fração do dano de Maldição.",
            "", 0, Rarity: Echo, Tags: ["curse", "echo"], Kind: "blood_pact", WaifuId: "waifu:velvet"),
        new("echo:velvet:viral-plague", "Praga Viral",
            "Ao morrer, a Decadência salta com todos os seus stacks para o inimigo vivo mais próximo.",
            "", 0, Rarity: Echo, Tags: ["curse"], Kind: "viral_plague", WaifuId: "waifu:velvet"),

        // Rin — Contágio (contagion/burn).
        new("echo:rin:wildfire", "Fogo Selvagem",
            "Todo acerto direto incendeia, de qualquer elemento; a queimadura não expira enquanto houver alvo em chamas.",
            "", 0, Rarity: Echo, Tags: ["burn"], Kind: "wildfire", WaifuId: "waifu:rin"),
        new("echo:rin:pyre", "Pira",
            "O dano de Rin cresce com o número de inimigos queimando ao mesmo tempo.",
            "", 0, Rarity: Echo, Tags: ["burn"], Kind: "pyre", WaifuId: "waifu:rin"),
        new("echo:rin:holocaust", "Holocausto",
            "Inimigo que morre em chamas explode num estouro de fogo em área.",
            "", 0, Rarity: Echo, Tags: ["burn"], Kind: "holocaust", WaifuId: "waifu:rin"),

        // Rynna — Carga Estática (static_charge/charge).
        new("echo:rynna:perpetual-storm", "Tempestade Perpétua",
            "A Descarga consome só metade da Carga, e a Carga enche o dobro de rápido.",
            "", 0, Rarity: Echo, Tags: ["charge"], Kind: "perpetual_storm", WaifuId: "waifu:rynna"),
        new("echo:rynna:overload", "Sobrecarga",
            "Alvos paralisados pela Descarga sofrem dano contínuo de eletrocussão.",
            "", 0, Rarity: Echo, Tags: ["charge"], Kind: "overload", WaifuId: "waifu:rynna"),
        new("echo:rynna:thunder-core", "Núcleo de Trovão",
            "A Descarga enche a ultimate muito mais rápido; usar a ultimate devolve a Carga cheia.",
            "", 0, Rarity: Echo, Tags: ["charge", "echo"], Kind: "thunder_core", WaifuId: "waifu:rynna"),

        // Lunara — Estilhaçar (shatter/frost).
        new("echo:lunara:eternal-winter", "Inverno Eterno",
            "Inimigos já entram lentos ao ver Lunara, e o gelo desacelera sem piso.",
            "", 0, Rarity: Echo, Tags: ["frost"], Kind: "eternal_winter", WaifuId: "waifu:lunara"),
        new("echo:lunara:chain-shatter", "Estilhaço em Cadeia",
            "O Estilhaço salta para os inimigos lentos próximos, repassando o estouro de gelo.",
            "", 0, Rarity: Echo, Tags: ["frost"], Kind: "chain_shatter", WaifuId: "waifu:lunara"),
        new("echo:lunara:moon-dance", "Dança da Lua",
            "O Estilhaço dispara já no 2º acerto, e a haste do trait não expira em combate.",
            "", 0, Rarity: Echo, Tags: ["frost"], Kind: "moon_dance", WaifuId: "waifu:lunara"),

        // Gaia — Presa (prey).
        new("echo:gaia:eternal-hunt", "Caça Eterna",
            "A marca de Presa rampa muito mais rápido e o teto de dano de caça é bem maior.",
            "", 0, Rarity: Echo, Tags: ["prey"], Kind: "eternal_hunt", WaifuId: "waifu:gaia"),
        new("echo:gaia:pack", "Matilha",
            "Gaia marca duas Presas ao mesmo tempo, e o bônus de caça ao executar é maior.",
            "", 0, Rarity: Echo, Tags: ["prey"], Kind: "pack", WaifuId: "waifu:gaia"),
        new("echo:gaia:deep-roots", "Raízes Profundas",
            "Cada acerto na Presa a enraíza (lentidão pesada) e crava um veneno de terra que a corrói.",
            "", 0, Rarity: Echo, Tags: ["prey"], Kind: "deep_roots", WaifuId: "waifu:gaia"),
    ];

    public static readonly IReadOnlyDictionary<string, CardDef> ById = All.ToDictionary(c => c.Id);
}
