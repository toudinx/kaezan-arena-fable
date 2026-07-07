using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>Species selection for wave spawns. Prefab rooms carry an authored species theme
/// (LM-08); themed picks still draw from the run rng so replays stay bit-perfect. Difficulty is
/// untouched: budget/wave logic and tier stat multipliers apply to themed species as usual.</summary>
public static class SpawnSelection
{
    public static string CommonSpecies(Rng rng, Room room, DungeonTier tier) =>
        room.SpawnTheme.Length > 0 ? rng.Pick(room.SpawnTheme) : rng.Pick(tier.CommonMobs);
}
