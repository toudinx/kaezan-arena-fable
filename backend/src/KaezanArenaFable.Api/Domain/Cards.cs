namespace KaezanArenaFable.Api.Domain;

public sealed record CardDef(string Id, string Name, string Description, string Stat, double Value);

/// <summary>Passive run cards (kaezan-arena card system, rebalanced). Max 3 stacks each.</summary>
public static class Cards
{
    public static readonly IReadOnlyList<CardDef> All =
    [
        new("card:atk", "Lâmina Afiada", "+12% de ataque", "atkPercent", 0.12),
        new("card:atkspeed", "Reflexos", "+10% de velocidade de ataque", "atkSpeedPercent", 0.10),
        new("card:maxhp", "Vitalidade", "+15% de vida máxima (cura o valor ganho)", "maxHpPercent", 0.15),
        new("card:regen", "Eco Restaurador", "+2 de vida por segundo", "regenPerSec", 2),
        new("card:movespeed", "Passos Rápidos", "+8% de velocidade de movimento", "moveSpeedPercent", 0.08),
        new("card:crit", "Olhar Mortal", "+6% de chance crítica", "critChance", 0.06),
        new("card:element", "Afinidade Elemental", "+15% de dano do seu elemento", "elementPercent", 0.15),
        new("card:xp", "Eco do Saber", "+15% de XP da run", "xpPercent", 0.15),
        new("card:gauge", "Fluxo de Eco", "+25% de carga de ultimate", "gaugePercent", 0.25),
        new("card:lifesteal", "Sede Vampírica", "3% do dano vira vida", "lifesteal", 0.03),
        new("card:armor", "Pele de Pedra", "-8% de dano recebido", "damageReduction", 0.08),
        new("card:gold", "Faro de Tesouro", "+20% de ouro saqueado", "goldPercent", 0.20),
        new("card:antidote", "Antídoto", "-50% de dano de condições (veneno, queimadura...)", "conditionResist", 0.50),
    ];

    public static readonly IReadOnlyDictionary<string, CardDef> ById = All.ToDictionary(c => c.Id);
}
