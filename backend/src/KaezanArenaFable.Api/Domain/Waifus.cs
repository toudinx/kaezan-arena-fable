namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Skill shapes: single (projectile/strike on target), beam (line from caster),
/// nova (ring around caster), area (circle on target tile), cone (frontal),
/// buff (self). FX ids are Tibia CONST_ME_* / CONST_ANI_* values.
/// </summary>
public sealed record SkillDef(
    string Id, string Name, string Shape, string Element, double Power, int CooldownMs,
    int Range, int Radius, int MissileId, int EffectId, int StunMs, string? Buff,
    int BuffMs, string Description);

public sealed record WaifuDef(
    string Id, string Name, string Title, int Rarity, string Element, string Weapon,
    int LookType, int Head, int Body, int Legs, int Feet,
    double BaseAtk, int BaseHp,
    string Skill1, string Skill2, string Skill3, string Ultimate, string Description);

public static class Waifus
{
    public static readonly IReadOnlyDictionary<string, SkillDef> Skills = new[]
    {
        // --- Mira (citizen) ---
        new SkillDef("mira_strike", "Golpe Duplo", "single", "physical", 1.7, 4000, 1, 0, 0, 216, 0, null, 0, "Um golpe rápido e certeiro em corpo a corpo."),
        new SkillDef("mira_spin", "Rodopio", "nova", "physical", 1.2, 8000, 0, 1, 0, 35, 0, null, 0, "Gira a lâmina atingindo todos ao redor."),
        new SkillDef("mira_warcry", "Grito de Guerra", "buff", "physical", 0, 12000, 0, 0, 0, 14, 0, "atk", 8000, "+35% de ataque por 8s."),
        new SkillDef("mira_fury", "Fúria Cidadã", "nova", "physical", 2.6, 0, 0, 2, 0, 35, 0, "atk", 5000, "Explosão de fúria ao redor e bônus de ataque."),
        // --- Wren (hunter) ---
        new SkillDef("wren_shot", "Tiro Certeiro", "single", "physical", 1.7, 4000, 6, 0, 3, 3, 0, null, 0, "Flecha precisa de longo alcance."),
        new SkillDef("wren_rain", "Chuva de Flechas", "area", "physical", 1.4, 9000, 6, 2, 3, 45, 0, null, 0, "Saraivada que cobre uma área."),
        new SkillDef("wren_step", "Passo Leve", "buff", "physical", 0, 12000, 0, 0, 0, 15, 0, "haste", 6000, "+30% de velocidade por 6s."),
        new SkillDef("wren_volley", "Saraivada Final", "area", "physical", 2.8, 0, 7, 3, 3, 45, 0, null, 0, "Uma tempestade de flechas devastadora."),
        // --- Lyra (mage) ---
        new SkillDef("lyra_spark", "Faísca", "single", "energy", 1.6, 3500, 5, 0, 36, 12, 0, null, 0, "Esfera de energia concentrada."),
        new SkillDef("lyra_arc", "Arco Voltaico", "beam", "energy", 1.3, 8000, 5, 0, 0, 38, 0, null, 0, "Feixe elétrico em linha reta."),
        new SkillDef("lyra_overload", "Sobrecarga", "buff", "energy", 0, 12000, 0, 0, 0, 13, 0, "atkspeed", 6000, "+40% de velocidade de ataque por 6s."),
        new SkillDef("lyra_storm", "Tempestade Estática", "area", "energy", 2.8, 0, 6, 3, 5, 73, 800, null, 0, "Raios caem sobre a área alvo."),
        // --- Tessa (warrior) ---
        new SkillDef("tessa_charge", "Investida", "single", "physical", 1.6, 4000, 1, 0, 0, 216, 1000, null, 0, "Avanço que atordoa o alvo por 1s."),
        new SkillDef("tessa_cleave", "Talho Amplo", "cone", "physical", 1.3, 7000, 0, 2, 0, 216, 0, null, 0, "Corte largo à frente."),
        new SkillDef("tessa_iron", "Postura Férrea", "buff", "physical", 0, 12000, 0, 0, 0, 4, 0, "shield", 5000, "-50% de dano recebido por 5s."),
        new SkillDef("tessa_execute", "Execução", "single", "physical", 3.0, 0, 1, 0, 0, 173, 0, null, 0, "Golpe brutal; executa alvos abaixo de 15% de vida."),
        // --- Sage (druid) ---
        new SkillDef("sage_thorn", "Espinho Vivo", "single", "earth", 1.6, 3500, 4, 0, 15, 47, 0, null, 0, "Espinho venenoso disparado pela natureza."),
        new SkillDef("sage_vines", "Vinhas Prendedoras", "area", "earth", 1.1, 9000, 5, 2, 15, 21, 2000, null, 0, "Vinhas que prendem inimigos por 2s."),
        new SkillDef("sage_bark", "Casca Rígida", "buff", "earth", 0, 12000, 0, 0, 0, 15, 0, "shield", 5000, "-50% de dano recebido por 5s."),
        new SkillDef("sage_bloom", "Erupção Natural", "area", "earth", 2.8, 0, 6, 3, 30, 45, 0, null, 0, "A terra explode sob os inimigos."),
        // --- Nyx (assassin) ---
        new SkillDef("nyx_stab", "Punhalada", "single", "physical", 1.9, 4000, 1, 0, 0, 1, 0, null, 0, "Ataque furtivo de alto dano."),
        new SkillDef("nyx_twin", "Lâminas Gêmeas", "cone", "physical", 1.3, 7000, 0, 1, 0, 216, 0, null, 0, "Corte duplo à frente."),
        new SkillDef("nyx_smoke", "Manto de Fumaça", "buff", "physical", 0, 12000, 0, 0, 0, 158, 0, "haste", 6000, "+30% de velocidade por 6s."),
        new SkillDef("nyx_dance", "Dança da Morte", "nova", "physical", 2.9, 0, 0, 2, 0, 1, 0, null, 0, "Sequência letal que atinge tudo ao redor."),
        // --- Kaela (knight) ---
        new SkillDef("kaela_bash", "Quebra-Escudo", "single", "physical", 1.6, 4500, 1, 0, 0, 4, 1500, null, 0, "Pancada que atordoa por 1.5s."),
        new SkillDef("kaela_wave", "Onda de Choque", "cone", "physical", 1.3, 8000, 0, 2, 0, 35, 0, null, 0, "Tremor frontal que abala o chão."),
        new SkillDef("kaela_bastion", "Bastião", "buff", "physical", 0, 12000, 0, 0, 0, 13, 0, "shield", 6000, "-50% de dano recebido por 6s."),
        new SkillDef("kaela_judgement", "Julgamento", "nova", "physical", 2.7, 0, 0, 2, 0, 35, 1200, null, 0, "Impacto devastador que atordoa ao redor."),
        // --- Sylwen (norsewoman) ---
        new SkillDef("sylwen_whisper", "Disparo Sussurrante", "single", "ice", 1.7, 3500, 6, 0, 29, 44, 0, null, 0, "Flecha gélida silenciosa."),
        new SkillDef("sylwen_pierce", "Perfurante Gélida", "beam", "ice", 1.3, 8000, 5, 0, 0, 42, 0, null, 0, "Rajada de gelo em linha que perfura tudo."),
        new SkillDef("sylwen_windbreak", "Quebra-Vento", "buff", "ice", 0, 12000, 0, 0, 0, 44, 0, "atkspeed", 6000, "+40% de velocidade de ataque por 6s."),
        new SkillDef("sylwen_thornfall", "Tempestade de Espinhos", "area", "ice", 2.7, 0, 6, 3, 29, 43, 1500, null, 0, "Tornado de gelo que congela a área."),
        // --- Ember (wizard) ---
        new SkillDef("ember_fireball", "Bola de Fogo", "area", "fire", 1.5, 5000, 5, 1, 4, 6, 0, null, 0, "Projétil explosivo de fogo."),
        new SkillDef("ember_wave", "Onda Flamejante", "cone", "fire", 1.3, 8000, 0, 3, 0, 7, 0, null, 0, "Leque de chamas à frente."),
        new SkillDef("ember_fuse", "Pavio Curto", "buff", "fire", 0, 12000, 0, 0, 0, 16, 0, "atk", 8000, "+35% de ataque por 8s."),
        new SkillDef("ember_inferno", "Inferno", "area", "fire", 3.0, 0, 6, 3, 4, 7, 0, null, 0, "O chão vira um mar de chamas."),
        // --- Rosa (pirate) ---
        new SkillDef("rosa_double", "Tiro Duplo", "single", "physical", 1.7, 4000, 5, 0, 2, 3, 0, null, 0, "Dois disparos em sequência."),
        new SkillDef("rosa_burst", "Rajada", "cone", "physical", 1.3, 7500, 0, 2, 0, 3, 0, null, 0, "Tiros em leque à frente."),
        new SkillDef("rosa_luck", "Maré de Sorte", "buff", "physical", 0, 12000, 0, 0, 0, 22, 0, "crit", 6000, "+20% de chance crítica por 6s."),
        new SkillDef("rosa_bombard", "Bombardeio", "area", "physical", 2.8, 0, 6, 3, 10, 5, 0, null, 0, "Chuva de balas de canhão."),
        // --- Velvet (nightmare) ---
        new SkillDef("velvet_void", "Corrente do Vazio", "single", "death", 1.8, 3500, 5, 0, 32, 18, 0, null, 0, "Projétil de morte súbita."),
        new SkillDef("velvet_umbral", "Caminho Umbral", "beam", "death", 1.4, 8000, 5, 0, 0, 18, 0, null, 0, "Rastro sombrio que atravessa inimigos."),
        new SkillDef("velvet_collapse", "Colapso", "nova", "death", 1.3, 9000, 0, 2, 0, 48, 1000, null, 0, "Implosão de energia ao redor."),
        new SkillDef("velvet_storm", "Colapso da Tempestade", "area", "death", 3.2, 0, 6, 3, 32, 18, 0, null, 0, "O vazio consome a área alvo."),
        // --- Aurora (summoner) ---
        new SkillDef("aurora_lance", "Lança Sagrada", "single", "holy", 1.7, 3500, 5, 0, 31, 40, 0, null, 0, "Lança de luz perfurante."),
        new SkillDef("aurora_light", "Luz Purificadora", "beam", "holy", 1.3, 8000, 5, 0, 0, 50, 0, null, 0, "Feixe sagrado em linha."),
        new SkillDef("aurora_bless", "Bênção", "buff", "holy", 0, 12000, 0, 0, 0, 49, 0, "atk", 8000, "+35% de ataque por 8s."),
        new SkillDef("aurora_judgement", "Juízo Celeste", "area", "holy", 3.0, 0, 6, 3, 31, 50, 0, null, 0, "A luz divina pune a área."),
        // --- Mirai (barbarian) ---
        new SkillDef("mirai_claw", "Garra Rasgante", "cone", "physical", 1.5, 4000, 0, 1, 0, 1, 0, null, 0, "Garras que rasgam à frente."),
        new SkillDef("mirai_roar", "Rugido Primal", "nova", "physical", 1.2, 8000, 0, 1, 0, 35, 1000, null, 0, "Rugido que atordoa os próximos."),
        new SkillDef("mirai_collapse", "Campo de Colapso", "area", "physical", 1.4, 10000, 4, 2, 0, 35, 1500, null, 0, "Esmaga uma área, atordoando."),
        new SkillDef("mirai_bloodfang", "Presa Sangrenta", "nova", "physical", 2.9, 0, 0, 2, 0, 173, 0, null, 0, "Mordida primal; executa alvos abaixo de 15% de vida."),
    }.ToDictionary(s => s.Id);

    public static readonly IReadOnlyList<WaifuDef> All =
    [
        // 3★
        new("waifu:mira", "Mira", "Cidadã Valente", 3, "physical", "melee", 136, 78, 96, 97, 115, 17, 190,
            "mira_strike", "mira_spin", "mira_warcry", "mira_fury", "Cresceu nas ruas de Thais e nunca recusou uma briga justa."),
        new("waifu:wren", "Wren", "Caçadora da Mata", 3, "physical", "bow", 137, 97, 121, 121, 95, 16, 145,
            "wren_shot", "wren_rain", "wren_step", "wren_volley", "Seus olhos enxergam o alvo antes mesmo do arco subir."),
        new("waifu:lyra", "Lyra", "Aprendiz do Éter", 3, "energy", "wand", 138, 0, 86, 86, 95, 16, 125,
            "lyra_spark", "lyra_arc", "lyra_overload", "lyra_storm", "Uma estudante de magia que sobrecarrega tudo — inclusive a si mesma."),
        new("waifu:tessa", "Tessa", "Lâmina Errante", 3, "physical", "melee", 142, 114, 94, 94, 114, 17, 190,
            "tessa_charge", "tessa_cleave", "tessa_iron", "tessa_execute", "Mercenária de poucas palavras e cortes precisos."),
        new("waifu:sage", "Sage", "Guardiã Verdejante", 3, "earth", "wand", 148, 121, 102, 102, 121, 16, 125,
            "sage_thorn", "sage_vines", "sage_bark", "sage_bloom", "A floresta fala com ela — e às vezes ataca por ela."),
        new("waifu:nyx", "Nyx", "Sombra Contratada", 3, "physical", "melee", 156, 114, 0, 0, 114, 18, 170,
            "nyx_stab", "nyx_twin", "nyx_smoke", "nyx_dance", "Ninguém viu seu rosto duas vezes. Ninguém quis ver."),
        // 4★
        new("waifu:kaela", "Kaela", "Muralha de Thais", 4, "physical", "melee", 139, 95, 130, 130, 131, 19, 230,
            "kaela_bash", "kaela_wave", "kaela_bastion", "kaela_judgement", "Carrega o escudo da família há três gerações — invicta."),
        new("waifu:sylwen", "Sylwen", "Vento Norsa", 4, "ice", "bow", 252, 9, 86, 87, 94, 19, 160,
            "sylwen_whisper", "sylwen_pierce", "sylwen_windbreak", "sylwen_thornfall", "O vento do norte sussurra; a flecha dela responde."),
        new("waifu:ember", "Ember", "Chama Viva", 4, "fire", "wand", 149, 113, 94, 78, 79, 19, 140,
            "ember_fireball", "ember_wave", "ember_fuse", "ember_inferno", "Expulsa da academia por 'entusiasmo excessivo com fogo'."),
        new("waifu:rosa", "Rosa", "Corsária Escarlate", 4, "physical", "bow", 155, 78, 94, 79, 115, 19, 160,
            "rosa_double", "rosa_burst", "rosa_luck", "rosa_bombard", "Seu navio afundou. Os inimigos dela também."),
        new("waifu:mirai", "Mirai", "Presa Primal", 4, "physical", "melee", 147, 78, 77, 96, 115, 20, 220,
            "mirai_claw", "mirai_roar", "mirai_collapse", "mirai_bloodfang", "Criada por feras nas montanhas, luta como elas."),
        // 5★
        new("waifu:velvet", "Velvet", "Eco do Pesadelo", 5, "death", "wand", 269, 114, 90, 90, 112, 22, 150,
            "velvet_void", "velvet_umbral", "velvet_collapse", "velvet_storm", "Dizem que ela voltou do abismo. O abismo discorda: nunca a deixou ir."),
        new("waifu:aurora", "Aurora", "Invocadora do Alvorecer", 5, "holy", "wand", 141, 0, 1, 9, 86, 22, 150,
            "aurora_lance", "aurora_light", "aurora_bless", "aurora_judgement", "Cada amanhecer é um feitiço que ela mesma renova."),
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
        "bow" => 3,  // arrow
        "wand" => element switch
        {
            "fire" => 4, "ice" => 29, "energy" => 5, "earth" => 30, "holy" => 31, "death" => 11,
            _ => 5
        },
        _ => 0
    };
}
