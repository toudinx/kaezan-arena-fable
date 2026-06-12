namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// One node of a Kaeli's mastery tree. EffectKind/Target:
/// - "stat":    Target ∈ { atkPercent, hpPercent, critChance, damageReduction, cooldownPercent, gaugePercent }
/// - "slotmod": Target = "slot:0".."slot:3" — multiplica o Power do slot (em todas as posturas)
/// - "ultmod":  Target = "ultimate" — amplifica a ultimate (duração do buff / cura)
/// - "trait":   amplifica o efeito do trait de assinatura da Kaeli
/// Dentro de um ramo, o node de Order N exige o de Order N-1 (regra implícita do serviço).
/// </summary>
public sealed record MasteryNodeDef(
    string Id, string Branch, int Order, string Name, string Description, int Cost,
    string EffectKind, string EffectTarget, double Value);

/// <summary>
/// Agregados de maestria pré-computados no início da run (determinismo: constantes na run).
/// </summary>
public sealed record MasteryAggregates(
    double AtkMult, double HpMult, double CritChanceBonus, double DamageReductionBonus,
    double CooldownMult, double GaugeMult, double TraitMult,
    double Slot0PowerMult, double Slot1PowerMult, double Slot2PowerMult, double Slot3PowerMult,
    double UltimatePowerMult)
{
    public static readonly MasteryAggregates None =
        new(1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1);

    public double SlotPowerMult(int slot) => slot switch
    {
        0 => Slot0PowerMult, 1 => Slot1PowerMult, 2 => Slot2PowerMult, 3 => Slot3PowerMult,
        _ => UltimatePowerMult
    };
}

/// <summary>
/// Tudo que a conta injeta na run além da WaifuDef: nível de afinidade (bônus de stats),
/// agregados de maestria e a skin selecionada (visual no mundo). Constante durante a run.
/// </summary>
public sealed record KaeliLoadout(int AffinityLevel, MasteryAggregates Mastery, SkinDef Skin)
{
    public static KaeliLoadout Default(WaifuDef waifu) =>
        new(1, MasteryAggregates.None, waifu.DefaultSkin);
}

/// <summary>
/// Árvore de Maestria de Eco por Kaeli (FABLE_TRACK F-B): template de 3 ramos
/// (Ofensiva/Defensiva/Eco) parametrizado por classe + trait, com nomes que referenciam
/// as skills reais do kit. IDs `mastery:*` são estáveis — nunca renomear.
/// </summary>
public static class Mastery
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<MasteryNodeDef>> TreeByWaifu =
        Waifus.All.ToDictionary(w => w.Id, BuildTree);

    public static readonly IReadOnlyDictionary<string, MasteryNodeDef> NodeById =
        TreeByWaifu.Values.SelectMany(nodes => nodes).ToDictionary(n => n.Id);

    /// <summary>Custo total da árvore (todas iguais por construção).</summary>
    public static int TreeTotalCost(string waifuId) =>
        TreeByWaifu.TryGetValue(waifuId, out var tree) ? tree.Sum(n => n.Cost) : 0;

    public static MasteryAggregates Aggregate(string waifuId, IReadOnlyCollection<string> unlockedNodes)
    {
        if (unlockedNodes.Count == 0 || !TreeByWaifu.TryGetValue(waifuId, out var tree))
            return MasteryAggregates.None;

        double atk = 1, hp = 1, crit = 0, dr = 0, cdr = 0, gauge = 1, trait = 1, ult = 1;
        double[] slots = [1, 1, 1, 1];

        foreach (var node in tree)
        {
            if (!unlockedNodes.Contains(node.Id)) continue;
            switch (node.EffectKind)
            {
                case "stat":
                    switch (node.EffectTarget)
                    {
                        case "atkPercent": atk += node.Value; break;
                        case "hpPercent": hp += node.Value; break;
                        case "critChance": crit += node.Value; break;
                        case "damageReduction": dr += node.Value; break;
                        case "cooldownPercent": cdr += node.Value; break;
                        case "gaugePercent": gauge += node.Value; break;
                    }
                    break;
                case "slotmod":
                    if (node.EffectTarget.StartsWith("slot:")
                        && int.TryParse(node.EffectTarget["slot:".Length..], out var slot)
                        && slot is >= 0 and <= 3)
                        slots[slot] += node.Value;
                    break;
                case "ultmod": ult += node.Value; break;
                case "trait": trait += node.Value; break;
            }
        }

        return new MasteryAggregates(atk, hp, crit, dr, Math.Max(1 - cdr, 0.5), gauge, trait,
            slots[0], slots[1], slots[2], slots[3], ult);
    }

    private static IReadOnlyList<MasteryNodeDef> BuildTree(WaifuDef waifu)
    {
        var shortName = waifu.Name.ToLowerInvariant();
        var cls = Classes.ById[waifu.ClassId];
        var stance = cls.InitialStance(waifu.Element);
        var slot1 = Classes.Skills[stance.Slots[0]];
        var slot3 = Classes.Skills[stance.Slots[2]];
        var ultimate = Classes.Skills[stance.Ultimate];
        var trait = waifu.Trait;

        string Id(string node) => $"mastery:{shortName}:{node}";

        return
        [
            // ---- Ofensiva ----
            new MasteryNodeDef(Id("off1"), "off", 1, "Vigor de Batalha",
                "+4% de ataque.", 1, "stat", "atkPercent", 0.04),
            new MasteryNodeDef(Id("off2"), "off", 2, $"{slot1.Name}+",
                $"O slot 1 ({slot1.Name} e equivalentes de postura) causa +15% de dano.",
                2, "slotmod", "slot:0", 0.15),
            new MasteryNodeDef(Id("off3"), "off", 3, "Olhar Mortal",
                "+4% de chance de crítico.", 2, "stat", "critChance", 0.04),
            new MasteryNodeDef(Id("off4"), "off", 4, $"{slot3.Name} Supremo",
                $"Node-chave: o slot 3 ({slot3.Name} e equivalentes de postura) causa +25% de dano.",
                3, "slotmod", "slot:2", 0.25),

            // ---- Defensiva ----
            new MasteryNodeDef(Id("def1"), "def", 1, "Fôlego",
                "+6% de HP máximo.", 1, "stat", "hpPercent", 0.06),
            new MasteryNodeDef(Id("def2"), "def", 2, "Pele de Pedra",
                "Dano recebido reduzido em 3%.", 2, "stat", "damageReduction", 0.03),
            new MasteryNodeDef(Id("def3"), "def", 3, "Vitalidade",
                "+8% de HP máximo.", 2, "stat", "hpPercent", 0.08),
            new MasteryNodeDef(Id("def4"), "def", 4, "Ritmo de Combate",
                "Node-chave: todos os cooldowns dos slots 1-4 reduzidos em 10%.",
                3, "stat", "cooldownPercent", 0.10),

            // ---- Eco (assinatura) ----
            new MasteryNodeDef(Id("eco1"), "eco", 1, "Eco Desperto",
                $"O trait \"{trait.Name}\" fica 25% mais forte.", 2, "trait", "trait", 0.25),
            new MasteryNodeDef(Id("eco2"), "eco", 2, "Pulso de Eco",
                "O gauge de ultimate enche 15% mais rápido.", 2, "stat", "gaugePercent", 0.15),
            new MasteryNodeDef(Id("eco3"), "eco", 3, "Eco Profundo",
                $"O trait \"{trait.Name}\" fica mais 35% mais forte.", 3, "trait", "trait", 0.35),
            new MasteryNodeDef(Id("eco4"), "eco", 4, $"{ultimate.Name} Verdadeiro",
                $"Node-chave: a ultimate ({ultimate.Name}) tem efeito 40% maior (duração/cura).",
                3, "ultmod", "ultimate", 0.40),
        ];
    }
}
