namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Per-tier visual theme consumed by <see cref="Engine.DungeonGenerator"/>. Every id is a
/// Tibia object appearance id exported by tools/AssetExtractor (see the "semantic" groups in
/// content-config.json). The frontend renderer is generic — it draws whatever ground/wall/decor
/// id the backend emits — so a biome is purely backend data with no frontend coupling.
/// </summary>
/// <param name="Ground">Walkable floor variants, rng-picked per cell.</param>
/// <param name="BossGround">Floor of the boss hall, a distinct material.</param>
/// <param name="WallH">Wall cell with open floor to the N/S only (the wall runs E–W).</param>
/// <param name="WallV">Wall cell with open floor to the E/W only (the wall runs N–S).</param>
/// <param name="WallPole">Wall cell with open floor on both axes (a junction nub).</param>
/// <param name="WallCorner">Outer corner: blocked on all four sides, open only on a diagonal.</param>
/// <param name="Decor">Ambient props scattered inside rooms.</param>
/// <param name="DecorChance">Per room-cell chance of a decor prop.</param>
/// <param name="Accent">Themed accent tiles (e.g. lava pools); empty = none.</param>
/// <param name="AccentChance">Per room-cell chance of an accent tile (evaluated before decor).</param>
public sealed record BiomeDef(
    ushort[] Ground, ushort[] BossGround,
    ushort WallH, ushort WallV, ushort WallPole, ushort WallCorner,
    ushort[] Decor, double DecorChance,
    ushort[] Accent, double AccentChance);

/// <summary>
/// Biome catalog by tier. Asset notes (all verified present in frontend manifest.json):
/// <list type="bullet">
/// <item>wall.cave 356–367 are fully-opaque dirt textures with painted ridges — no addressable
/// per-orientation corner sprites, but being opaque they never leave an empty corner "tooth".</item>
/// <item>wall.stone 1112–1116 are 64px alpha pieces: 1112 vertical body, 1113 horizontal body,
/// 1114 rubble pole, 1116 a solid corner (the 1116/1118/1120/1122 ids share one mask, so we
/// use 1116 — it fills the corner 100%, so corners read solid even if not perfectly oriented).</item>
/// <item>1047/1048/958 are bone props (crypt); 727–730 are lava tiles (lair/abyss accent);
/// 499–504 are cracked/mossy stone floors (crypt); 307–315 grass (orc fort).</item>
/// </list>
/// </summary>
public static class Biomes
{
    private static readonly ushort[] CaveGround = [351, 352, 353, 354, 355];
    private static readonly ushort[] StoneGround = [416, 418];
    private static readonly ushort[] MossStone = [499, 500, 501, 502, 503, 504];
    private static readonly ushort[] DarkStone = [416, 418, 499];
    private static readonly ushort[] GrassGround = [307, 308, 309, 310, 311, 312, 313, 314, 315];

    private static readonly ushort[] CaveRocks = [1772, 1773, 1774, 1775];
    private static readonly ushort[] Bones = [1047, 1048, 958];
    private static readonly ushort[] Lava = [727, 728, 729, 730];

    // dirt walls (opaque) — pole/corner reuse a body tile since the family has no clean corner.
    private const ushort DirtH = 356, DirtV = 358, DirtPole = 360, DirtCorner = 356;
    // stone walls (64px alpha) — solid corner/junction via 1116 to avoid corner gaps.
    private const ushort StoneH = 1113, StoneV = 1112, StonePole = 1116, StoneCorner = 1116;

    /// <summary>Tier 1 — Toca Ecoante: brown dirt cavern with boulders.</summary>
    public static readonly BiomeDef Cave = new(
        CaveGround, StoneGround, DirtH, DirtV, DirtPole, DirtCorner,
        CaveRocks, 0.025, [], 0);

    /// <summary>Tier 2 — Forte Uruk: grassy orc camp ringed by stone ruins.</summary>
    public static readonly BiomeDef Fort = new(
        GrassGround, StoneGround, StoneH, StoneV, StonePole, StoneCorner,
        CaveRocks, 0.02, [], 0);

    /// <summary>Tier 3 — Cripta Sombria: mossy stone crypt strewn with bones.</summary>
    public static readonly BiomeDef Crypt = new(
        MossStone, StoneGround, StoneH, StoneV, StonePole, StoneCorner,
        Bones, 0.03, [], 0);

    /// <summary>Tier 4 — Covil Escamado: dark stone lair with decorative lava pools.</summary>
    public static readonly BiomeDef Lair = new(
        DarkStone, StoneGround, StoneH, StoneV, StonePole, StoneCorner,
        CaveRocks, 0.02, Lava, 0.05);

    /// <summary>Tier 5 — Abismo Ecoante: stone abyss flooded with lava and bone.</summary>
    public static readonly BiomeDef Abyss = new(
        DarkStone, StoneGround, StoneH, StoneV, StonePole, StoneCorner,
        Bones, 0.02, Lava, 0.055);

    public static BiomeDef ForTier(int tier) => tier switch
    {
        1 => Cave,
        2 => Fort,
        3 => Crypt,
        4 => Lair,
        5 => Abyss,
        _ => Cave,
    };
}
