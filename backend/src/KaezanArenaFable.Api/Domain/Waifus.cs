namespace KaezanArenaFable.Api.Domain;

public sealed record WaifuDef(
    string Id, string Name, string Title, int Rarity, string Element, string Weapon,
    int LookType, int Head, int Body, int Legs, int Feet,
    double BaseAtk, int BaseHp, string ClassId, string Description);

public static class Waifus
{
    public static readonly IReadOnlyList<WaifuDef> All =
    [
        // Class mapping: melee -> Warrior; physical/holy ranged -> Sentinel;
        // ice/earth -> Shaman; fire/energy/death -> Wizard.

        // 3-star
        new("waifu:mira", "Mira", "Cidadã Valente", 3, "physical", "melee",
            136, 78, 96, 97, 115, 17, 190, Classes.WarriorId,
            "Cresceu nas ruas de Thais e nunca recusou uma briga justa."),
        new("waifu:wren", "Wren", "Caçadora da Mata", 3, "physical", "bow",
            137, 97, 121, 121, 95, 16, 145, Classes.SentinelId,
            "Seus olhos enxergam o alvo antes mesmo do arco subir."),
        new("waifu:lyra", "Lyra", "Aprendiz do Éter", 3, "energy", "wand",
            138, 0, 86, 86, 95, 16, 125, Classes.WizardId,
            "Uma estudante de magia que sobrecarrega tudo — inclusive a si mesma."),
        new("waifu:tessa", "Tessa", "Lâmina Errante", 3, "physical", "melee",
            142, 114, 94, 94, 114, 17, 190, Classes.WarriorId,
            "Mercenária de poucas palavras e cortes precisos."),
        new("waifu:sage", "Sage", "Guardiã Verdejante", 3, "earth", "wand",
            148, 121, 102, 102, 121, 16, 125, Classes.ShamanId,
            "A floresta fala com ela — e às vezes ataca por ela."),
        new("waifu:nyx", "Nyx", "Sombra Contratada", 3, "physical", "melee",
            156, 114, 0, 0, 114, 18, 170, Classes.WarriorId,
            "Ninguém viu seu rosto duas vezes. Ninguém quis ver."),

        // 4-star
        new("waifu:kaela", "Kaela", "Muralha de Thais", 4, "physical", "melee",
            139, 95, 130, 130, 131, 19, 230, Classes.WarriorId,
            "Carrega o escudo da família há três gerações — invicta."),
        new("waifu:sylwen", "Sylwen", "Vento Norsa", 4, "ice", "bow",
            252, 9, 86, 87, 94, 19, 160, Classes.ShamanId,
            "O vento do norte sussurra; a flecha dela responde."),
        new("waifu:ember", "Ember", "Chama Viva", 4, "fire", "wand",
            149, 113, 94, 78, 79, 19, 140, Classes.WizardId,
            "Expulsa da academia por entusiasmo excessivo com fogo."),
        new("waifu:rosa", "Rosa", "Corsária Escarlate", 4, "physical", "bow",
            155, 78, 94, 79, 115, 19, 160, Classes.SentinelId,
            "Seu navio afundou. Os inimigos dela também."),
        new("waifu:mirai", "Mirai", "Presa Primal", 4, "physical", "melee",
            147, 78, 77, 96, 115, 20, 220, Classes.WarriorId,
            "Criada por feras nas montanhas, luta como elas."),

        // 5-star
        new("waifu:velvet", "Velvet", "Eco do Pesadelo", 5, "death", "wand",
            269, 114, 90, 90, 112, 22, 150, Classes.WizardId,
            "Dizem que ela voltou do abismo. O abismo discorda: nunca a deixou ir."),
        new("waifu:aurora", "Aurora", "Invocadora do Alvorecer", 5, "holy", "wand",
            141, 0, 1, 9, 86, 22, 150, Classes.SentinelId,
            "Cada amanhecer é um feitiço que ela mesma renova."),
    ];

    public static readonly IReadOnlyDictionary<string, WaifuDef> ById = All.ToDictionary(w => w.Id);

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
