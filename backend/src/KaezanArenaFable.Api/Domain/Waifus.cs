using System.Text.Json.Serialization;

namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Signature passive that differentiates a Kaeli inside her class. Engine-supported kinds:
/// executioner (Value=bônus, Param=fração de HP), fortress (Value=redução fixa),
/// bulwark (Value=redução, Param=fração de HP própria), pack_hunter (Value=bônus por inimigo
/// adjacente, Param=cap), deadeye (Value=crit bônus, Param=distância mínima),
/// slayer (Value=bônus, Tag=bestiaryClass), skill_lifesteal (Value=fração),
/// chiller (Value=fator de slow, Param=duração ms), overcharge (Value=bônus de gauge).
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
/// A Kaeli: identidade (lore/personalidade), trait de assinatura, skins e gostos de presente.
/// O kit de combate vem da classe (ClassId); Skins[0] é o visual padrão.
/// Lore tem 4 fragmentos, destravados por afinidade (GameConfig.AffinityLoreLevels).
/// </summary>
public sealed record WaifuDef(
    string Id, string Name, string Title, int Rarity, string Element, string Weapon,
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
/// Roster enxuto e profundo (decisão 2026-06-12): 9 Kaelis — 3 por raridade, todas com
/// trait, lore, skins e presentes. Cortadas: Tessa, Nyx, Lyra, Rosa (contas antigas recebem
/// refund em Kaeros via AccountSanitizer). Kaela foi promovida a 5★.
/// IDs `waifu:*` e `skin:*` são estáveis — nunca renomear.
/// </summary>
public static class Waifus
{
    public static readonly IReadOnlyList<WaifuDef> All =
    [
        // ===== 3★ =====
        new("waifu:mira", "Mira", "Punho de Thais", 3, "physical", "melee",
            17, 195, Classes.BarbarianId,
            "Cresceu nas ruas de Thais e nunca recusou uma briga justa. Hoje briga pelas " +
            "justas dos outros — de graça, se a causa for boa; por queijo, se não for.",
            "Esquentada, leal, riso fácil. Resolve no soco o que a diplomacia demorar demais.",
            new TraitDef("trait:mira", "Coração Valente", "bulwark", 0.25, 0.35, "",
                "Quando o HP de Mira está abaixo de 35%, ela recebe 25% menos dano."),
            [
                "O primeiro soco foi por um pão. O padeiro acertou um carrinho de mão na cabeça " +
                "dela; ela acertou o chão com a cara. Aos oito anos, Mira aprendeu a lição que " +
                "carrega até hoje: não é sobre não cair — é sobre o que você faz no chão.",
                "Aos quinze, virou a sombra do mercado de Thais: quem mexesse com os pequenos " +
                "respondia a ela. Os guardas fingiam não ver. Era conveniente — ela resolvia em " +
                "trinta segundos o que a burocracia levava três semanas para arquivar.",
                "Kaela em pessoa veio recrutá-la para a guarda real, com armadura, selo e " +
                "salário. Mira ouviu tudo, agradeceu, e disse não: 'Muralha protege quem está " +
                "dentro. Eu cuido de quem ficou do lado de fora.' Dizem que Kaela sorriu.",
                "Ela treina toda madrugada no cais, socando sacos de areia até o sol nascer. " +
                "Não é por disciplina, ela admite: 'É que se eu parar de lutar por um dia " +
                "inteiro, começo a pensar. E pensar dói mais.'"
            ],
            [3607, 3582], // cheese, ham
            [
                new SkinDef("skin:mira:default", "Punho de Thais",
                    "O visual de sempre: roupa simples, atadura nova, cicatriz antiga.",
                    136, 78, 96, 97, 115, "default", 0),
                new SkinDef("skin:mira:corsair", "Corsária por um Dia",
                    "Ganhou de um capitão aposentado depois de limpar a taverna dele. A bandana é dele; o resto da história ela não conta.",
                    155, 95, 113, 96, 115, "gold", 2500),
                new SkinDef("skin:mira:jester", "Estrela do Mercado",
                    "Nos festivais de Thais, Mira luta de fantasia para as crianças. Os sinos denunciam cada gancho de esquerda.",
                    270, 94, 105, 81, 94, "kaeros", 800),
            ]),

        new("waifu:wren", "Wren", "Caçadora da Mata", 3, "physical", "bow",
            16, 150, Classes.SentinelId,
            "Seus olhos enxergam o alvo antes mesmo do arco subir. A floresta a criou, " +
            "e ela cobra caro de quem a desrespeita.",
            "Quieta, observadora, seca no humor. Conta as flechas — e os favores.",
            new TraitDef("trait:wren", "Olho de Águia", "deadeye", 0.15, 3, "",
                "+15% de chance de crítico contra alvos a 3 ou mais tiles de distância."),
            [
                "Foi encontrada aos quatro anos, sozinha, na borda da mata — sem nome, sem " +
                "medo, segurando uma flecha quebrada como se fosse um brinquedo. O velho " +
                "guarda-caça que a achou disse depois: 'Não adotei ela. Ela me adotou.'",
                "O primeiro arco ela mesma entalhou, torto, de galho de teixo. Errou noventa e " +
                "nove flechas no mesmo tronco. A centésima acertou. Ela nunca mais errou — e " +
                "guarda a flecha de número noventa e nove até hoje, 'para lembrar do preço'.",
                "Quando caçadores ilegais queimaram um ninho de grifos, Wren os caçou por onze " +
                "dias. Não machucou nenhum: entregou todos amarrados no posto da guarda, com as " +
                "próprias armadilhas deles. 'A mata não perdoa', disse. 'Eu só faço a entrega.'",
                "À noite, no acampamento, ela fala com o arco. Baixinho, em uma língua que " +
                "ninguém reconhece. Se perguntam, ela dá de ombros: 'Ele atira melhor quando " +
                "está de bom humor.' Ninguém sabe se é piada."
            ],
            [7408, 3578], // wyvern fang, fish
            [
                new SkinDef("skin:wren:default", "Caçadora da Mata",
                    "Couro batido, capuz fundo e o cheiro de pinho que não sai nunca.",
                    137, 97, 121, 121, 95, "default", 0),
                new SkinDef("skin:wren:tide", "Maré de Verão",
                    "Uma vez por ano a mata fica quente demais até para ela. O arco vai junto para a praia — claro que vai.",
                    158, 97, 86, 86, 95, "kaeros", 800),
            ]),

        new("waifu:sage", "Sage", "Guardiã Verdejante", 3, "earth", "wand",
            16, 130, Classes.ShamanId,
            "A floresta fala com ela — e às vezes ataca por ela. Sage é a tradutora de um " +
            "mundo verde que perdeu a paciência com o nosso.",
            "Serena na superfície, teimosa como raiz. Trata plantas como gente e gente como muda.",
            new TraitDef("trait:sage", "Seiva Vital", "skill_lifesteal", 0.06, 0, "",
                "Habilidades curam Sage em 6% do dano causado."),
            [
                "Nasceu numa vila de lenhadores e aos seis anos foi devolvida: as árvores que o " +
                "pai cortava de manhã amanheciam inteiras de novo. A vila chamou de maldição. O " +
                "círculo de druidas que a acolheu chamou de ouvido.",
                "Seu rito de iniciação durou um dia a mais que o de todos: passou a noite " +
                "extra discutindo com um carvalho que se recusava a mover as raízes da trilha. " +
                "Venceu o carvalho. Os druidas ainda contam essa história em voz baixa.",
                "A árvore-mãe do seu bosque está doente — um apodrecimento que sobe do fundo da " +
                "terra, devagar, há anos. É por isso que Sage desce às cavernas e criptas: está " +
                "procurando a raiz do mal. Literalmente.",
                "Ela rega uma flor de túmulo todos os dias, num cemitério onde não conhece " +
                "ninguém. 'Alguém plantou e foi embora', explica. 'Promessa de planta é " +
                "promessa. Alguém tem que cumprir as que ficaram.'"
            ],
            [3661, 5879], // grave flower, spider silk
            [
                new SkinDef("skin:sage:default", "Guardiã Verdejante",
                    "Vestes do círculo, tingidas com o verde da própria mata que ela guarda.",
                    148, 121, 102, 102, 121, "default", 0),
                new SkinDef("skin:sage:hermit", "Votos da Eremita",
                    "Nas peregrinações, Sage veste o pano mais simples que existe. A floresta não se impressiona com bordado.",
                    157, 121, 19, 20, 119, "gold", 1500),
            ]),

        // ===== 4★ =====
        new("waifu:mirai", "Mirai", "Presa Primal", 4, "physical", "melee",
            20, 220, Classes.WarriorId,
            "Criada por lobos de inverno nas montanhas, luta como eles: em silêncio, em " +
            "bando, e sempre para proteger a matilha. A matilha, agora, é você.",
            "Direta, territorial, carinhosa do jeito errado. Rosna antes de sorrir.",
            new TraitDef("trait:mirai", "Instinto de Matilha", "pack_hunter", 0.05, 0.25, "",
                "+5% de dano para cada inimigo a até 2 tiles (máximo +25%). Mirai luta melhor cercada."),
            [
                "A loba que a encontrou no degelo deveria tê-la comido. Em vez disso, carregou " +
                "o embrulho chorão pela nuca até a toca. Os caçadores da região juram que, " +
                "naquele inverno, a alcateia caçou em dobro — como quem alimenta mais uma boca.",
                "Sua primeira caçada foi aos nove: um cervo, no escuro, só com as mãos e os " +
                "dentes da matilha ao redor. Ela errou. A loba-mãe uivou mesmo assim. Mirai " +
                "aprendeu ali que a matilha não celebra o abate — celebra a volta de todos.",
                "Os humanos a acharam aos quinze, falando mais uivo que palavra. Levou anos " +
                "para aceitar paredes e talheres. A primeira coisa que entendeu de verdade na " +
                "civilização foi uma briga de taverna: 'Isso', ela disse, sorrindo, 'isso eu sei.'",
                "Ela escolheu te seguir do mesmo jeito que a loba a escolheu: sem cerimônia e " +
                "sem volta. 'Matilha não é sangue', ela explica, batendo no próprio peito. 'É " +
                "quem caça contigo quando o inverno chega.'"
            ],
            [3577, 5897], // meat, wolf paw
            [
                new SkinDef("skin:mirai:default", "Presa Primal",
                    "Peles, presas e cicatrizes que ela apresenta pelo nome, como velhas amigas.",
                    147, 78, 77, 96, 115, "default", 0),
                new SkinDef("skin:mirai:nightstalker", "Caçadora Noturna",
                    "Quando a presa é esperta demais, a matilha caça em silêncio absoluto. Presente da afinidade: ela só veste isso por quem confia.",
                    156, 114, 0, 0, 114, "affinity", 6),
            ]),

        new("waifu:sylwen", "Neva", "Geometria do Frio", 4, "ice", "wand",
            19, 140, Classes.CryomancerId,
            "Ve o frio como linguagem — cada cristal de gelo e uma equacao que o mundo " +
            "esqueceu de resolver. Neva so traduz.",
            "Precisa, fria, fala em angulos e segundos. Nao e cruel; so exata.",
            new TraitDef("trait:sylwen", "Precisao Glacial", "chiller", 0.25, 2000, "",
                "Dano de gelo de Neva aplica 25% de lentidão por 2s aos inimigos."),
            [
                "Aos seis anos, fez florescer uma tempestade de neve perfeita dentro de casa. " +
                "A mae varreu. Neva anotou o angulo de cada floco que sobrou no chao. Tinha " +
                "dezenove paginas e nao parou desde entao.",
                "Na academia de cristalomancia, entregou a tese final em branco. No verso havia " +
                "uma unica equacao. O avaliador levou tres semanas para provar que estava certa. " +
                "Ela esperou sem reclamar: 'O resultado nao muda enquanto voce calcula.'",
                "Reconstruiu a Batalha do Gelo Partido em miniatura usando figuras de cristal. " +
                "Cada figura morreu na ordem certa. Os generais pediram para destruir o modelo. " +
                "Neva disse nao: 'Errar a geometria nao desfaz o que aconteceu.'",
                "Ela veio porque os monstros tem padroes. 'Tudo tem', diz, ajustando as luvas. " +
                "'Os que parecem caoticos sao so equacoes com variaveis que voce ainda nao encontrou.'"
            ],
            [3029, 3027], // small sapphire, black pearl
            [
                new SkinDef("skin:sylwen:default", "Geometria do Frio",
                    "Vestes brancas como estrutura de cristal. Cada dobra calculada.",
                    252, 9, 86, 87, 94, "default", 0),
                new SkinDef("skin:sylwen:lanterns", "Nevasca do Festival",
                    "Uma vez foi a um festival para estudar como o gelo se comporta na multidao. Ficou mais tempo do que o previsto. Nao explica o motivo.",
                    150, 9, 28, 90, 115, "kaeros", 1000),
            ]),

        new("waifu:ember", "Ember", "Chama Viva", 4, "fire", "wand",
            19, 140, Classes.PyromancerId,
            "Expulsa da academia por entusiasmo excessivo com fogo. A academia ainda está " +
            "de pé, o que, segundo ela, prova que o entusiasmo era na medida certa.",
            "Elétrica, tagarela, gênia e desastrada na mesma frase. O fogo gosta dela de volta.",
            new TraitDef("trait:ember", "Combustão", "overcharge", 0.30, 0, "",
                "O gauge de ultimate de Ember enche 30% mais rápido."),
            [
                "A explosão que a expulsou da academia destruiu exatamente uma coisa: a parede " +
                "do auditório onde o conselho votava a proibição de magia experimental. Ember " +
                "jura até hoje que foi coincidência. O buraco tinha o formato da votação: 7 a 2.",
                "Dos nove conselheiros, dois votaram a favor dela. Um era a professora Maren, " +
                "que lhe disse na saída: 'Fogo que não queima nada é só luz fria. Vai. Queima " +
                "longe daqui, onde eles não possam te apagar.' Ember tatuou a frase no antebraço.",
                "Ela não lança fogo: conversa com ele. Negocia. Promete. Nas noites ruins, " +
                "pede desculpa. 'Todo mundo trata o fogo como ferramenta ou como desastre', " +
                "explica. 'Ninguém pergunta o que ele quer. Por isso ele foge de todo mundo.'",
                "Seu caderno de pesquisa tem 412 páginas de experimentos proibidos, anotados em " +
                "código. Na última página, uma única linha legível: 'Quando eu provar que dá " +
                "para queimar sem consumir, volto pela porta da frente.'"
            ],
            [2828, 3033], // book, small amethyst
            [
                new SkinDef("skin:ember:default", "Chama Viva",
                    "O vermelho não é tinta: é o tom exato do cabelo dela depois do incidente nº 27.",
                    149, 113, 94, 78, 79, "default", 0),
                new SkinDef("skin:ember:academy", "Dias de Academia",
                    "O uniforme que ela 'esqueceu de devolver'. Ainda serve. A mancha de fuligem na manga é original de fábrica.",
                    138, 113, 86, 86, 95, "affinity", 4),
                new SkinDef("skin:ember:hellhunter", "Caçadora do Inferno",
                    "Forjada depois do primeiro Hellhound: se o inferno tem cães, alguém precisa ser o cão de caça do outro lado.",
                    288, 113, 94, 94, 114, "gold", 4000),
            ]),

        // ===== 5★ =====
        new("waifu:kaela", "Kaela", "Muralha de Thais", 5, "physical", "melee",
            21, 270, Classes.WarriorId,
            "Carrega o escudo da família há três gerações — invicta. Onde Kaela planta os " +
            "pés, a linha não recua: nunca recuou, não vai começar hoje.",
            "Formal, generosa, teimosíssima. Fala pouco de si e tudo do dever.",
            new TraitDef("trait:kaela", "Última Muralha", "fortress", 0.12, 0, "",
                "Kaela recebe 12% menos dano de todas as fontes."),
            [
                "O escudo tem 214 marcas de batalha e três nomes gravados por dentro: a avó, " +
                "que o carregou na Guerra dos Portões; a mãe, que o carregou uma única noite — " +
                "a noite certa; e Kaela, que o recebeu aos doze, junto com a frase da família: " +
                "'A muralha não pergunta o que vem. Só responde que não passa.'",
                "No cerco da Porta Oeste, a linha quebrou e a guarda recuou — menos ela. Kaela " +
                "segurou o vão da porta sozinha por onze minutos, o tempo exato de evacuar o " +
                "distrito. Quando os reforços chegaram, ela contou os civis, não os inimigos: " +
                "'Duzentos e quarenta. Todos. Agora podemos conversar sobre o resto.'",
                "Ela tentou recrutar uma brigona de rua chamada Mira três vezes. Nas três, " +
                "ouviu não. Na terceira, Mira explicou o motivo — e Kaela parou de insistir. " +
                "Hoje, quando a guarda não pode agir, uma moeda com o brasão dela aparece no " +
                "mercado, e o problema some. As duas nunca comentam o arranjo.",
                "Toda noite ela acende uma vela na muralha e fica um turno além do seu. Não é " +
                "ordem, nem penitência. 'A cidade dorme melhor com uma luz acesa lá em cima', " +
                "diz. 'E eu durmo melhor sabendo quem segura a vela.'"
            ],
            [3017, 2920], // silver brooch, torch
            [
                new SkinDef("skin:kaela:default", "Muralha de Thais",
                    "A armadura da linhagem, polida todo domingo, amassada onde a história mandou.",
                    139, 95, 130, 130, 131, "default", 0),
                new SkinDef("skin:kaela:vanguard", "Vanguarda Real",
                    "O uniforme de gala da guarda. Kaela odeia desfiles — mas ninguém na parada marcha tão reto.",
                    142, 114, 94, 94, 114, "gold", 4000),
                new SkinDef("skin:kaela:oath", "Juramento da Alvorada",
                    "Branca como o turno da madrugada. Só veste para quem entende por que a vela fica acesa.",
                    140, 0, 0, 9, 95, "affinity", 6),
            ]),

        new("waifu:aurora", "Aurora", "Invocadora do Alvorecer", 5, "holy", "wand",
            22, 150, Classes.OracleId,
            "Cada amanhecer é um feitiço que ela mesma renova. Aurora não reza para a luz: " +
            "a luz é que marca hora com ela.",
            "Doce, pontualíssima, assustadoramente serena. Nunca dorme depois das 4h.",
            new TraitDef("trait:aurora", "Luz Purificadora", "slayer", 0.20, 0, "Undead",
                "+20% de dano contra mortos-vivos. O que a noite esqueceu de enterrar, ela apaga."),
            [
                "Há vinte e três anos, em sua vila natal, a noite durou três dias. Ninguém " +
                "lembra como acabou — só que uma menina de seis anos subiu no campanário, " +
                "cantou uma nota que ninguém ensinou, e o horizonte obedeceu. Aurora não conta " +
                "essa versão. 'O sol só estava atrasado', diz. 'Alguém precisava cobrar.'",
                "Seu primeiro amanhecer invocado de verdade foi aos dezenove, sobre um campo de " +
                "batalha que não acabava. Os clérigos chamam o feitiço de impossível; ela chama " +
                "de pontualidade aplicada. O exército inteiro viu a noite recuar como maré.",
                "O preço: Aurora não dorme depois das quatro da manhã. Nunca. É a hora em que " +
                "ela desce — sozinha — à cripta que ninguém menciona, e renova o selo que " +
                "mantém algo do lado de baixo da porta. Há 17 anos, sem falhar uma madrugada.",
                "Perguntaram-lhe uma vez o que acontece se ela parar. Aurora sorriu, serena " +
                "como sempre, e respondeu servindo o chá: 'A mesma coisa que acontece se o sol " +
                "parar. Por isso nenhum dos dois para. Mais açúcar?'"
            ],
            [2917, 3054], // candlestick, silver amulet
            [
                new SkinDef("skin:aurora:default", "Invocadora do Alvorecer",
                    "Vestes que mudam de tom com a hora do dia. Ao amanhecer, custam a olhar de frente.",
                    141, 0, 1, 9, 86, "default", 0),
                new SkinDef("skin:aurora:court", "Alvorada da Corte",
                    "Para os bailes do palácio. Aurora aparece, ofusca o lustre, e sai antes das quatro — sempre antes das quatro.",
                    140, 1, 1, 0, 9, "gold", 4000),
                new SkinDef("skin:aurora:dusk", "Véu do Crepúsculo",
                    "O que ela veste para descer à cripta. Quem viu, viu pouco: a luz vai com ela, mas baixa, como vela em vigília.",
                    141, 114, 90, 88, 95, "affinity", 6),
            ]),

        new("waifu:velvet", "Velvet", "Eco do Pesadelo", 5, "death", "wand",
            22, 150, Classes.NecromancerId,
            "Dizem que ela voltou do abismo. O abismo discorda: nunca a deixou ir. Velvet " +
            "caminha com um pé em cada mundo — e os dois mundos fingem que não é com eles.",
            "Voz baixa, cortesia antiga, humor de lápide. Sabe o seu nome antes de você dizer.",
            new TraitDef("trait:velvet", "Fome do Abismo", "executioner", 0.25, 0.30, "",
                "+25% de dano contra inimigos abaixo de 30% de HP. O abismo termina o que começa."),
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
                new SkinDef("skin:velvet:default", "Eco do Pesadelo",
                    "O vestido com que voltou do lago. Nunca seca por completo; ninguém teve coragem de comentar.",
                    269, 114, 90, 90, 112, "default", 0),
                new SkinDef("skin:velvet:crimson", "Pesadelo Carmesim",
                    "Quando o Pesadelo sonha, sonha em vermelho. Ela acorda com o vestido assim e devolve à cor de sempre até o meio-dia. Às vezes não devolve.",
                    269, 94, 113, 94, 94, "kaeros", 1500),
                new SkinDef("skin:velvet:brotherhood", "Irmandade do Abismo",
                    "O hábito da ordem que estuda o lado de baixo. Conferiram a ela o posto mais alto. Velvet aceitou por educação — e porque o capuz é confortável.",
                    279, 114, 90, 90, 112, "affinity", 8),
            ]),
    ];

    public static readonly IReadOnlyDictionary<string, WaifuDef> ById = All.ToDictionary(w => w.Id);

    public static readonly IReadOnlyDictionary<string, SkinDef> SkinById =
        All.SelectMany(w => w.Skins).ToDictionary(s => s.Id);

    /// <summary>Dona da skin (skin id → waifu).</summary>
    public static readonly IReadOnlyDictionary<string, string> SkinOwner =
        All.SelectMany(w => w.Skins.Select(s => (s.Id, w.Id))).ToDictionary(p => p.Item1, p => p.Item2);

    public const string StarterWaifuId = "waifu:mirai";
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
