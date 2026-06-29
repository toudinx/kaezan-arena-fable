namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// One node of a Kaeli's mastery tree. EffectKind/Target:
/// - "stat":    Target ∈ { atkPercent, hpPercent, critChance, damageReduction, cooldownPercent, gaugePercent }
/// - "slotmod": Target = "slot:0".."slot:3" — multiplies slot Power across every stance
/// - "ultmod":  Target = "ultimate" — amplifies the ultimate (buff duration / healing)
/// - "trait":   amplifies the Kaeli signature trait effect
/// Inside a branch, the node at Order N requires the Order N-1 node.
/// </summary>
public sealed record MasteryNodeDef(
    string Id, string Branch, int Order, string Name, string Description, int Cost,
    string EffectKind, string EffectTarget, double Value);

/// <summary>
/// Precomputed mastery aggregates at run start (determinism: constants during the run).
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
/// Everything the account injects into the run beyond WaifuDef: affinity level, mastery
/// aggregates, and the selected skin. Constant during the run.
/// </summary>
public sealed record KaeliLoadout(int AffinityLevel, MasteryAggregates Mastery, SkinDef Skin)
{
    public static KaeliLoadout Default(WaifuDef waifu) =>
        new(1, MasteryAggregates.None, waifu.DefaultSkin);
}

/// <summary>
/// Echo Mastery tree per Kaeli (FABLE_TRACK F-B): a 3-branch template
/// (Offense/Defense/Echo) parameterized by class + trait, with names that reference
/// the real kit skills. IDs `mastery:*` are stable — never rename them.
/// </summary>
public static class Mastery
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<MasteryNodeDef>> TreeByWaifu =
        Waifus.All.ToDictionary(w => w.Id, BuildTree);

    public static readonly IReadOnlyDictionary<string, MasteryNodeDef> NodeById =
        TreeByWaifu.Values.SelectMany(nodes => nodes).ToDictionary(n => n.Id);

    /// <summary>Total tree cost (all trees match by construction).</summary>
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
            // ---- Offense ----
            new MasteryNodeDef(Id("off1"), "off", 1, "Battle Vigor",
                "+4% attack.", 1, "stat", "atkPercent", 0.04),
            new MasteryNodeDef(Id("off2"), "off", 2, $"{slot1.Name}+",
                $"Slot 1 ({slot1.Name} and stance equivalents) deals +15% damage.",
                2, "slotmod", "slot:0", 0.15),
            new MasteryNodeDef(Id("off3"), "off", 3, "Killing Gaze",
                "+4% critical chance.", 2, "stat", "critChance", 0.04),
            new MasteryNodeDef(Id("off4"), "off", 4, $"{slot3.Name} Supreme",
                $"Keystone: slot 3 ({slot3.Name} and stance equivalents) deals +25% damage.",
                3, "slotmod", "slot:2", 0.25),

            // ---- Defense ----
            new MasteryNodeDef(Id("def1"), "def", 1, "Second Wind",
                "+6% maximum HP.", 1, "stat", "hpPercent", 0.06),
            new MasteryNodeDef(Id("def2"), "def", 2, "Stone Skin",
                "Incoming damage reduced by 3%.", 2, "stat", "damageReduction", 0.03),
            new MasteryNodeDef(Id("def3"), "def", 3, "Vitality",
                "+8% maximum HP.", 2, "stat", "hpPercent", 0.08),
            new MasteryNodeDef(Id("def4"), "def", 4, "Combat Rhythm",
                "Keystone: all slot 1-4 cooldowns reduced by 10%.",
                3, "stat", "cooldownPercent", 0.10),

            // ---- Echo (signature) ----
            new MasteryNodeDef(Id("eco1"), "eco", 1, "Awakened Echo",
                $"The \"{trait.Name}\" trait becomes 25% stronger.", 2, "trait", "trait", 0.25),
            new MasteryNodeDef(Id("eco2"), "eco", 2, "Echo Pulse",
                "The ultimate gauge fills 15% faster.", 2, "stat", "gaugePercent", 0.15),
            new MasteryNodeDef(Id("eco3"), "eco", 3, "Deep Echo",
                $"The \"{trait.Name}\" trait becomes another 35% stronger.", 3, "trait", "trait", 0.35),
            new MasteryNodeDef(Id("eco4"), "eco", 4, $"True {ultimate.Name}",
                $"Keystone: the ultimate ({ultimate.Name}) has 40% stronger effect (duration/healing).",
                3, "ultmod", "ultimate", 0.40),
        ];
    }
}
