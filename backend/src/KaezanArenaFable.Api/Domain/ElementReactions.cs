namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// One elemental reaction (F-E): applying a second element onto a target already marked with a
/// different one triggers this. Damage is a fraction of the triggering hit (never an explosive
/// multiplier — reactions are a bonus for playing elements, not the only viable path).
/// FX ids are Tibia CONST_ME_* values.
/// </summary>
/// <param name="Radius">0 = single target; &gt;0 = the reaction also splashes neighbours.</param>
public sealed record ReactionDef(
    string Id, string Name, double DamageFraction, int Radius,
    int StunMs, double SlowFactor, int SlowMs, int Fx);

/// <summary>
/// Reaction matrix keyed by (mark element, incoming element). Data-driven so new pairings are
/// just rows here. Pairs that should fire regardless of order are registered both ways. The
/// stance toggles (ice↔earth, energy↔fire) all react, so toggling mid-combo is rewarded.
/// </summary>
public static class ElementReactions
{
    /// <summary>Elements that can mark a target / trigger a reaction (physical/support never do).</summary>
    private static readonly HashSet<string> Reactive = ["fire", "ice", "energy", "earth", "holy", "death"];

    public static bool IsReactive(string element) => Reactive.Contains(element);

    // reaction prototypes
    private static readonly ReactionDef Vaporize =
        new("reaction:vaporize", "Estilhaço", 0.70, 1, 0, 1.0, 0, 43);     // ice + fire: thermal shock, splashes
    private static readonly ReactionDef Permafrost =
        new("reaction:permafrost", "Permafrost", 0.40, 0, 0, GameConfig.SlowFactorFloor, 2500, 44); // ice + earth: locks the target
    private static readonly ReactionDef Overload =
        new("reaction:overload", "Sobrecarga", 0.60, 1, 0, 1.0, 0, 12);    // energy + fire: bursts outward
    private static readonly ReactionDef Superconduct =
        new("reaction:superconduct", "Supercondução", 0.50, 0, 1200, 1.0, 0, 32); // energy + ice: stuns
    private static readonly ReactionDef Detonate =
        new("reaction:detonate", "Detonação", 0.85, 0, 0, 1.0, 0, 4);      // fire + earth: detonates the burn
    private static readonly ReactionDef Annihilate =
        new("reaction:annihilate", "Aniquilação", 1.00, 0, 0, 1.0, 0, 18); // holy + death: opposites annihilate

    private static readonly IReadOnlyDictionary<(string Mark, string Trigger), ReactionDef> Matrix = Build();

    private static Dictionary<(string, string), ReactionDef> Build()
    {
        var m = new Dictionary<(string, string), ReactionDef>();
        void Pair(string a, string b, ReactionDef def)
        {
            m[(a, b)] = def;
            m[(b, a)] = def;
        }

        Pair("ice", "fire", Vaporize);
        Pair("ice", "earth", Permafrost);
        Pair("energy", "fire", Overload);
        Pair("ice", "energy", Superconduct);
        Pair("fire", "earth", Detonate);
        Pair("holy", "death", Annihilate);
        return m;
    }

    /// <summary>The reaction for a target marked with <paramref name="mark"/> hit by <paramref name="trigger"/>, if any.</summary>
    public static ReactionDef? Lookup(string mark, string trigger) =>
        Matrix.GetValueOrDefault((mark, trigger));
}
