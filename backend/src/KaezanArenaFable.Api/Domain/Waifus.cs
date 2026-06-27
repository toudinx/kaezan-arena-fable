using System.Text.Json.Serialization;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Signature passive that differentiates a Kaeli inside her class. Só 2 números (Value, Param) +
/// Tag; ambos amplificados pela maestria (_traitMult). O resto dos tunáveis mora em GameConfig.
///
/// Kinds assinatura (K-04, um arquétipo por Kaeli — estado vivo no tick, ver GameWorld):
/// - judgment (Eloa): marca+detona. Value=burst (fração do acerto-gatilho), Param=cura (fração do burst).
/// - discipline (Seren): combo no mesmo alvo. Value=+dano/acerto, Param=cap do ramp.
/// - decay (Velvet): stacks de DoT que sobem o limiar de execução. Value=bônus de execução, Param=limiar base.
/// - contagion (Rin): incêndio que se propaga. Value=lifesteal do burn, Param=raio do salto.
/// - static_charge (Rynna): barra de carga → descarga. Value=bônus de gauge (estilo overcharge), Param=0.
/// - shatter (Lunara): bônus vs lento + haste + estilhaço. Value=bônus vs lento, Param=duração do slow (ms).
/// - prey (Gaia): marca de presa com ramp por tempo de caça. Value=ramp/s, Param=cap do ramp.
///
/// Kinds de reserva ainda suportados (classes sem Kaeli / legado): executioner, fortress, bulwark,
/// pack_hunter, deadeye, slayer, skill_lifesteal, chiller, overcharge.
/// </summary>
public sealed record TraitDef(
    string Id, string Name, string Kind, double Value, double Param, string Tag,
    string Description);

/// <summary>
/// One outfit a Kaeli can wear (the in-game "skin"). Unlock: "default" (sempre),
/// "affinity" (UnlockValue = nível), "gold" / "kaeros" (UnlockValue = preço).
/// Addons (bitmask 0..3) e MountLookType são opcionais: skins autorais (Outfit Studio) podem
/// fixar addons/montaria; quando 0 o jogo cai no comportamento padrão (addons por ascensão,
/// montaria por equipamento). Defaults mantêm os construtores posicionais do roster estático.
/// </summary>
public sealed record SkinDef(
    string Id, string Name, string Description,
    int LookType, int Head, int Body, int Legs, int Feet,
    string Unlock, int UnlockValue,
    int Addons = 0, int MountLookType = 0);

/// <summary>
/// MG-02: papel é o eixo PRIMÁRIO de identidade mecânica (dano de auto vs skill, velocidade, range,
/// AOE — ver <see cref="GameConfig.Roles"/>). A velha dicotomia melee/ranged morreu como conceito de
/// design: <see cref="WaifuDef.Weapon"/> agora é só cosmético (sprite/missile/visual de auto).
/// Mapa: Mage = Eloa/Velvet/Rin; Archer = Gaia/Lunara; Knight = Rynna/Seren.
/// </summary>
public enum KaeliRole { Mage, Archer, Knight }

/// <summary>
/// A Kaeli: identidade (lore/personalidade), trait de assinatura, skins e gostos de presente.
/// O kit de combate vem da classe (ClassId); Skins[0] é o visual padrão.
/// Lore tem 4 fragmentos, destravados por afinidade (GameConfig.AffinityLoreLevels).
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

    // Outfit padrão achatado para compatibilidade do catálogo (frontend usa lookType/head/...).
    public int LookType => DefaultSkin.LookType;
    public int Head => DefaultSkin.Head;
    public int Body => DefaultSkin.Body;
    public int Legs => DefaultSkin.Legs;
    public int Feet => DefaultSkin.Feet;
}

/// <summary>
/// Roster refundado (refundação Kaelis, K-02): 7 Kaelis, todas 5★ premium, fechando a matriz
/// elemental Holy/Physical/Death/Fire/Energy/Ice/Earth. Eloa, Seren, Velvet, Rin, Rynna, Lunara,
/// Gaia. As Kaelis antigas (Mira, Wren, Sage, Mirai, Neva, Ember, Kaela, Aurora) saíram do pool
/// jogável — contas antigas são migradas pelo AccountSanitizer (refund em Kaeros + starter).
/// Os kits autorais por Kaeli vêm em K-03; aqui cada Kaeli aponta para a classe existente cujo
/// elemento bate com sua afinidade. As traits são provisórias (kinds suportados pelo engine) e
/// serão reescritas em K-04. IDs `waifu:*` e `skin:*` são estáveis — nunca renomear.
/// </summary>
public static class Waifus
{
    public static readonly IReadOnlyList<WaifuDef> All =
    [
        new("waifu:eloa", "Eloa", "Serafim do Julgamento", 5, "holy", "wand", KaeliRole.Mage,
            22, 150, Classes.OracleId,
            "Anjo de luz que não reza pela salvação alheia: ela a aplica. Onde Eloa abre as " +
            "asas, a noite encolhe e o que se escondia nela perde o direito de continuar escondido.",
            "Solene, gentil sem ser mole, paciente como quem já viu o fim e voltou. Julga sem ódio.",
            new TraitDef("trait:eloa", "Selo de Julgamento", "judgment", 1.2, 0.25, "sin",
                "Cada acerto marca o alvo com Pecado. Ao chegar a 3 stacks ele é Julgado: o próximo " +
                "acerto consome a marca num estouro sacro em área e cura a Serafim. Espalhe marcas " +
                "para sustentar, ou foque um alvo para detonar rápido."),
            [
                "Dizem que Eloa não nasceu: foi convocada, na primeira manhã depois da Longa " +
                "Noite, por uma cidade inteira que cantou junto sem combinar. Ela não confirma " +
                "nem desmente. 'Cheguei quando precisaram', é tudo que diz. 'Como toda alvorada.'",
                "Sua primeira sentença foi sobre um cavaleiro morto que se recusava a descer. " +
                "Eloa não o destruiu — sentou ao lado dele a noite toda e ouviu o que faltava " +
                "dizer. Ao amanhecer, ele agradeceu e foi. 'Julgar', ela explica, 'é deixar " +
                "terminar. A espada é só para quem não aceita o ponto final.'",
                "Carrega uma balança de luz onde a maioria carrega arma. Num prato, o que a " +
                "pessoa fez; no outro, o que ainda pode fazer. Quase sempre o segundo pesa mais. " +
                "'Por isso quase sempre dou mais um dia', diz. 'Quase.'",
                "Quando a luz dela se apaga por um instante — e às vezes apaga — Eloa fica " +
                "humana de novo, e é a única hora em que parece cansada. 'A luz não descansa', " +
                "murmura. 'Mas quem a carrega, sim. Fica comigo até o sol voltar?'"
            ],
            [2917, 3054], // candlestick, silver amulet
            [
                new SkinDef("skin:eloa:default", "Serafim do Julgamento",
                    "Vestes que pegam a cor da primeira luz do dia. De manhã, custam a olhar de frente.",
                    141, 0, 1, 9, 86, "default", 0),
                new SkinDef("skin:eloa:absolution", "Manto da Absolvição",
                    "Branco de turno de madrugada. Eloa só o veste para quem ela decidiu perdoar — ou poupar.",
                    140, 1, 1, 0, 9, "gold", 4000),
                new SkinDef("skin:eloa:vigil", "Vigília do Crepúsculo",
                    "O que ela veste quando a sentença é dura. A luz vai junto, mas baixa, como vela velando.",
                    141, 114, 90, 88, 95, "affinity", 6),
            ]),

        new("waifu:seren", "Seren", "Cavaleira Astral", 5, "physical", "melee", KaeliRole.Knight,
            21, 240, Classes.WarriorId,
            "Aprendeu a esgrima sob um céu que ela jura que respondia aos golpes. Cada duelo, " +
            "para Seren, é uma conversa de uma frase só — e ela sempre tem a última palavra.",
            "Disciplinada ao ponto da teimosia, formal no trato, calorosa só quando baixa a guarda.",
            new TraitDef("trait:seren", "Disciplina", "discipline", 0.08, 0.40, "combo",
                "Acertos consecutivos no mesmo alvo escalam o dano (+8% por acerto, até +40%). " +
                "Trocar de alvo ou parar de bater zera o ramp. Cada 3º acerto é um Corte Perfeito: " +
                "crítico garantido. Comprometa-se com um duelo — ou perca o embalo limpando adds."),
            [
                "A escola onde treinou tinha uma regra única: o aluno duelava com a própria " +
                "sombra até ela parar de errar. Seren passou três invernos contra a dela. " +
                "Quando a sombra finalmente acertou o tempo dela, o mestre disse: 'Agora você " +
                "tem uma rival que nunca te abandona. Trate-a bem.'",
                "Recusou um posto na guarda de honra por escrito, em uma linha: 'Honra que se " +
                "veste de manhã não é honra, é fantasia.' Mandaram a carta de volta emoldurada. " +
                "Ela a usa como alvo de treino até hoje — diz que melhora a mira e o humor.",
                "Tem um corte que treina mil vezes por dia e nunca usou em combate. Perguntam " +
                "por quê. 'Porque no dia em que eu precisar dele, não vou ter tempo de pensar', " +
                "responde. 'Disciplina é decorar a resposta antes da pergunta chegar.'",
                "Dorme com a espada ao alcance, mas longe o bastante para ter que se levantar. " +
                "'Se eu acordar e ela estiver na minha mão, virei outra pessoa', explica. 'Quero " +
                "escolher pegar a espada toda manhã. No dia em que for reflexo, eu paro.'"
            ],
            [3017, 2920], // silver brooch, torch
            [
                new SkinDef("skin:seren:default", "Cavaleira Astral",
                    "Armadura polida ao ponto de espelhar a constelação que ela diz ser sua mestra.",
                    139, 95, 130, 130, 131, "default", 0),
                new SkinDef("skin:seren:vanguard", "Vanguarda do Zênite",
                    "O traje de gala dos duelos formais. Seren acha pompa um desperdício — mas ninguém marcha tão reto.",
                    142, 114, 94, 94, 114, "gold", 4000),
                new SkinDef("skin:seren:eclipse", "Lâmina do Eclipse",
                    "Negra como o céu sem estrelas. Ela só a veste contra quem merece o duelo levado a sério.",
                    156, 0, 0, 19, 114, "affinity", 6),
            ]),

        new("waifu:velvet", "Velvet", "Arauto do Pesadelo", 5, "death", "wand", KaeliRole.Mage,
            22, 150, Classes.NecromancerId,
            "Dizem que ela voltou do abismo. O abismo discorda: nunca a deixou ir. Velvet " +
            "caminha com um pé em cada mundo — e os dois mundos fingem que não é com eles.",
            "Voz baixa, cortesia antiga, humor de lápide. Sabe o seu nome antes de você dizer.",
            new TraitDef("trait:velvet", "Maldição Acumulada", "decay", 0.25, 0.15, "curse",
                "Cada habilidade empilha Decadência (DoT) no alvo e eleva seu limiar de execução " +
                "(executa <15%, +2% por stack até <25%). Quanto mais maldição você investe, mais " +
                "cedo o alvo estoura — paciência e então execução."),
            [
                "Os registros do convento dizem que a noviça Velvet morreu afogada no lago " +
                "negro, aos vinte e um anos, e foi enterrada no dia seguinte. Os mesmos " +
                "registros, três páginas depois, anotam em tinta trêmula: 'Ela veio jantar.'",
                "O que ela viu lá embaixo não tem nome nas línguas de cima — então Velvet o " +
                "chama de Pesadelo, por educação. O Pesadelo a seguiu de volta como um cão " +
                "imenso segue quem o alimentou uma vez. Ela não o expulsa. 'Seria indelicado. " +
                "E inútil. Principalmente inútil.'",
                "As vozes no veludo: ela ouve os mortos recentes, baixinho, como conversa no " +
                "quarto ao lado. A maioria só quer terminar uma frase que ficou pela metade. " +
                "Velvet anota todas num caderninho preto e, quando pode, entrega os recados. É " +
                "o trabalho que ela escolheu. Ninguém agradece. Ela prefere assim.",
                "O que o abismo quer de volta não é Velvet — é o que ela trouxe escondido: a " +
                "última coisa que segurou na mão quando afundou, e que a fez voltar. Ela nunca " +
                "abre essa mão em público. Quem presta atenção repara: a luva esquerda nunca sai."
            ],
            [3114, 3027, 8040], // skull, black pearl, velvet mantle
            [
                new SkinDef("skin:velvet:default", "Vestes do Lago Negro",
                    "O vestido com que voltou do lago. Nunca seca por completo; ninguém teve coragem de comentar.",
                    269, 114, 90, 90, 112, "default", 0),
                new SkinDef("skin:velvet:kaezan", "Velvet Kaezan V1",
                    "Primeira sprite autoral in-game da Arauto: lookType 900003, adaptada de uma skin-base escolhida como referência visual.",
                    900003, 0, 0, 0, 0, "default", 0),
                new SkinDef("skin:velvet:crimson", "Pesadelo Carmesim",
                    "Quando o Pesadelo sonha, sonha em vermelho. Ela acorda com o vestido assim e devolve à cor de sempre até o meio-dia. Às vezes não devolve.",
                    269, 94, 113, 94, 94, "kaeros", 1500),
                new SkinDef("skin:velvet:brotherhood", "Irmandade do Abismo",
                    "O hábito da ordem que estuda o lado de baixo. Conferiram a ela o posto mais alto. Velvet aceitou por educação — e porque o capuz é confortável.",
                    279, 114, 90, 90, 112, "affinity", 8),
            ]),

        new("waifu:rin", "Rin", "Súcubus do Pacto", 5, "fire", "wand", KaeliRole.Mage,
            22, 150, Classes.PyromancerId,
            "Não seduz para enganar: seduz porque é a forma mais honesta que conhece de fazer " +
            "um trato. Rin oferece fogo, calor e companhia — e cobra exatamente o combinado.",
            "Provocadora, espirituosa, leal de um jeito que ninguém espera de um demônio. Cumpre a palavra.",
            new TraitDef("trait:rin", "Contágio", "contagion", 0.06, 3, "burn",
                "Os acertos de fogo de Rin incendeiam o alvo, e o incêndio se propaga: o burn salta " +
                "para o inimigo não-queimando mais próximo (ao morrer um alvo em chamas ou a cada 2s). " +
                "Cada tick de queimadura cura Rin um pouco (pacto). Posicione para encadear o fogo."),
            [
                "O primeiro pacto que selou foi com uma criança doente que pediu só mais um " +
                "inverno de vida para a avó. Rin cobrou o preço de sempre — um favor futuro — e " +
                "esperou. A avó viveu seis invernos. O favor nunca foi cobrado. 'Alguns tratos', " +
                "diz Rin, 'a gente fecha sabendo que vai perder. Esses são os bons.'",
                "Tem um livro-razão encadernado em couro vermelho onde anota cada acordo, cada " +
                "cláusula, cada cara. Ninguém nunca a viu trapacear numa linha. 'Súcubus que " +
                "mente uma vez', explica, 'passa a eternidade sem ninguém acreditar nela. Caro " +
                "demais. Prefiro o fogo limpo.'",
                "O charme dela é real, e é por isso que ela avisa antes de usá-lo. 'Vou ligar o " +
                "calor agora', diz, como quem acende uma vela. 'Se não quiser, é só dizer. " +
                "Consentimento é a única cláusula que eu não negocio.'",
                "Foi expulsa do próprio plano por um detalhe técnico: recusou-se a quebrar um " +
                "pacto que o senhorio do inferno mandou anular. 'Era a assinatura dele também', " +
                "ela deu de ombros. 'Não vou queimar meu nome para salvar o dele. O fogo é meu.'"
            ],
            [2828, 3033], // book, small amethyst
            [
                new SkinDef("skin:rin:default", "Súcubus do Pacto",
                    "Vermelho de brasa viva, com o livro-razão sempre a um gesto da mão.",
                    149, 113, 94, 78, 79, "default", 0),
                new SkinDef("skin:rin:contract", "Selo do Contrato",
                    "O traje formal das grandes negociações. Cada fivela é uma cláusula. Rin fecha o casaco como quem fecha um trato.",
                    138, 94, 113, 94, 94, "gold", 4000),
                new SkinDef("skin:rin:ashwing", "Asas de Cinza",
                    "O que sobra quando o pacto cobra caro: cinza e calor. Ela só mostra as asas verdadeiras a quem confia o suficiente para não fugir.",
                    288, 113, 94, 94, 114, "affinity", 6),
            ]),

        new("waifu:rynna", "Rynna", "Dragoa do Trovão", 5, "energy", "melee", KaeliRole.Knight,
            21, 220, Classes.StormcallerId,
            "Metade dragoa, metade tempestade, inteira impaciente. Rynna não espera o raio cair " +
            "do céu: ela é o ponto onde o céu decide descer e bater primeiro.",
            "Impetuosa, barulhenta, generosa com a lealdade e mesquinha com a paciência. Engaja primeiro, pensa no caminho.",
            new TraitDef("trait:rynna", "Carga Estática", "static_charge", 0.30, 0, "charge",
                "Os acertos enchem uma barra de Carga; cheia, o golpe que a completa vira Descarga — " +
                "uma corrente curta que paralisa os alvos próximos. Cada paralyze acelera a ultimate, " +
                "e a tempestade já enche o gauge 30% mais rápido. Ritme os golpes pra soltar no pico."),
            [
                "Nasceu de um ovo que caiu de uma nuvem de tempestade — literalmente, segundo a " +
                "vila que o encontrou fumegando numa cratera. Os anciãos quiseram afastá-lo. Uma " +
                "ferreira o levou para casa: 'Trovão que cai perto', disse, 'é trovão que " +
                "escolheu ficar.' Criou Rynna entre a bigorna e a faísca.",
                "A escama condutora das costas dela acumula carga conforme luta. Quando estala, " +
                "estala alto. 'Aprendi cedo a contar', ela ri. 'Um, dois, três escamas brilhando " +
                "— aí é melhor o inimigo já ter caído, porque o quarto estouro é por minha conta.'",
                "Detesta esperar. A única vez que ficou parada de propósito foi três dias na " +
                "frente de um covil, sem comer, para garantir que a fera lá dentro não saísse " +
                "antes dos aldeões evacuarem. 'Paciência eu tenho', resmunga. 'Só guardo toda " +
                "para uma coisa de cada vez. Não peça duas.'",
                "Voa baixo de propósito, raspando os telhados, e os moradores reclamam do " +
                "barulho — mas deixam a janela aberta. 'É o jeito deles de dizer oi sem admitir', " +
                "ela diz, fazendo a curva de novo. 'Trovão também é só um céu dizendo que chegou.'"
            ],
            [7408, 3029], // wyvern fang, small sapphire
            [
                new SkinDef("skin:rynna:default", "Dragoa do Trovão",
                    "Escamas que faíscam no escuro e uma postura de quem já decidiu avançar.",
                    156, 86, 38, 38, 39, "default", 0),
                new SkinDef("skin:rynna:tempest", "Fúria da Tempestade",
                    "A armadura de guerra das grandes caçadas. Cada placa é um para-raios. Rynna a veste e o ar fica pesado de carga.",
                    158, 38, 86, 12, 5, "gold", 4000),
                new SkinDef("skin:rynna:skyforged", "Forjada no Céu",
                    "Feita pela ferreira que a criou, com metal atingido por raio. Rynna só a veste em datas que importam — e nunca diz quais.",
                    150, 86, 5, 38, 86, "affinity", 6),
            ]),

        new("waifu:lunara", "Lunara", "Lebre Lunar", 5, "ice", "bow", KaeliRole.Archer,
            20, 205, Classes.CryomancerId,
            "Rápida como uma decisão e fria como a noite que a fez. Lunara dança pelo gelo que " +
            "ela mesma cria, e quando você percebe que ela passou, já está mais lento que ela.",
            "Brincalhona, esquiva, melancólica nas horas quietas. Foge da pergunta e volta com a resposta.",
            new TraitDef("trait:lunara", "Estilhaçar", "shatter", 0.25, 2000, "frost",
                "O gelo de Lunara desacelera. Bater num alvo já lento dá dano bônus e concede haste " +
                "breve; o 3º acerto no lento o estilhaça num estouro e consome a lentidão. Aplique " +
                "slow, mergulhe com haste, estilhace e reposicione: hit-and-run premia mobilidade."),
            [
                "Conta-se que a lua estava sozinha e fez uma companheira de luz e geada para " +
                "correr com ela pelo céu. Lunara escorregou para a terra numa noite de eclipse e " +
                "não achou o caminho de volta. 'Não estou perdida', insiste. 'Estou explorando. " +
                "A lua sabe onde eu estou. Ela vê tudo, lembra?'",
                "Pula em vez de andar — sempre pulou. Mediram o salto dela uma vez: do telhado " +
                "do templo ao galho mais alto do bosque, sem tocar o chão. 'O chão é lento', " +
                "explica. 'E eu tenho pressa de chegar em lugar nenhum. É o melhor tipo de pressa.'",
                "Onde ela passa, o gelo fica — fininho, brilhante, traiçoeiro. Os perseguidores " +
                "escorregam; ela não. 'É só questão de respeitar o gelo', diz, dando uma " +
                "piscadela. 'Ele me deixa passar porque eu peço com jeito. Vocês chegam pisando " +
                "duro. Claro que ele revida.'",
                "Nas noites sem lua, Lunara fica quieta, olhando o céu vazio. É a única hora em " +
                "que para de pular. 'Ela volta amanhã', murmura, mais para si mesma. 'Sempre " +
                "volta. Eu só fico aqui guardando o frio até lá. Alguém tem que guardar.'"
            ],
            [3029, 3027], // small sapphire, black pearl
            [
                new SkinDef("skin:lunara:default", "Lebre Lunar",
                    "Branco e azul de luar sobre neve. Leve o bastante para nunca afundar no próprio gelo.",
                    252, 9, 86, 87, 94, "default", 0),
                new SkinDef("skin:lunara:crescent", "Dança do Crescente",
                    "O traje das noites de festival, com guizos de gelo que tocam a cada salto. Lunara adora — ninguém consegue segui-la mesmo assim.",
                    150, 9, 28, 90, 115, "kaeros", 1500),
                new SkinDef("skin:lunara:newmoon", "Véu da Lua Nova",
                    "O que ela veste nas noites escuras, quando a lua some. Cinza-prata, quieto. Só quem fica até a lua voltar chega a ver.",
                    156, 9, 19, 19, 94, "affinity", 6),
            ]),

        new("waifu:gaia", "Gaia", "Arqueira dos Monólitos", 5, "earth", "bow", KaeliRole.Archer,
            21, 170, Classes.ShamanId,
            "A terra não floresce para Gaia: ela se ergue. Onde outros veem pedra parada, ela vê " +
            "uma flecha esperando, uma raiz pronta, um monólito que ainda não decidiu cair.",
            "Paciente como rocha, econômica nas palavras, certeira como sentença. Espera o tiro certo a vida toda se preciso.",
            new TraitDef("trait:gaia", "Presa", "prey", 0.05, 0.30, "prey",
                "Gaia marca um alvo como Presa; seu dano contra a Presa cresce quanto mais a caça " +
                "dura (+5% por segundo, até +30%). Quando a Presa morre, a marca salta para o próximo " +
                "alvo e Gaia ganha cadência por alguns segundos. Escolha a prioridade e persiga."),
            [
                "Foi criada por um eremita que esculpia menires no alto de um planalto. Ela não " +
                "aprendeu a falar antes de aprender a ouvir a pedra: 'Cada monólito tem uma nota', " +
                "ele dizia, batendo na rocha. 'Quem ouve a nota, sabe onde a pedra quer rachar.' " +
                "Gaia ouviu. Hoje suas flechas rachadas no ponto exato.",
                "O arco dela é de uma madeira petrificada que pesa como ferro. Ninguém mais " +
                "consegue armá-lo. 'Não é força', ela corrige, raramente. 'É concordância. O arco " +
                "verga para quem ele acha que vai mirar direito. Os outros ele só ignora.'",
                "Caçava feras que ninguém aceitava enfrentar — não por glória, mas porque elas " +
                "esmagavam as trilhas de pedra que o eremita levou a vida erguendo. 'Ele plantou " +
                "rocha onde ninguém planta nada', diz. 'O mínimo que devo é não deixar derrubarem.'",
                "Quando precisa pensar, Gaia empilha pedras — uma sobre a outra, em equilíbrio " +
                "impossível, sem cola, sem truque. Deixa as torres pelo caminho. 'Para quem vier " +
                "depois saber que alguém passou e teve calma', explica. 'A terra lembra. Eu " +
                "também. É o que a gente faz de melhor: ficar.'"
            ],
            [5897, 5879], // wolf paw, spider silk
            [
                new SkinDef("skin:gaia:default", "Arqueira dos Monólitos",
                    "Couro batido cor de barro e o arco de madeira petrificada às costas, pesado como sentença.",
                    137, 97, 121, 121, 95, "default", 0),
                new SkinDef("skin:gaia:bedrock", "Guarda da Rocha-Mãe",
                    "A vestimenta cerimonial dos guardiões do planalto, com placas de menir lascado. Gaia raramente a tira do baú — mas quando tira, é guerra.",
                    148, 121, 102, 102, 121, "gold", 2500),
                new SkinDef("skin:gaia:quartz", "Veio de Quartzo",
                    "Linhas claras de cristal correm pelo couro como veios numa rocha. Presente da afinidade: ela só a mostra a quem aprendeu a esperar com ela.",
                    157, 121, 19, 86, 119, "affinity", 6),
            ]),
    ];

    public static readonly IReadOnlyDictionary<string, WaifuDef> ById = All.ToDictionary(w => w.Id);

    public static readonly IReadOnlyDictionary<string, SkinDef> SkinById =
        All.SelectMany(w => w.Skins).ToDictionary(s => s.Id);

    /// <summary>Dona da skin (skin id → waifu).</summary>
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
