namespace KaezanArenaFable.Api.Domain;

/// <summary>
/// Per-tier visual theme consumed by <see cref="Engine.DungeonGenerator"/>. Every id is a
/// Tibia object appearance id exported by tools/AssetExtractor (see the "semantic" groups in
/// content-config.json). The frontend renderer is generic — it draws whatever ground/wall/decor
/// id the backend emits — so a biome is purely backend data with no frontend coupling.
/// </summary>
/// <param name="Ground">Walkable floor variants, rng-picked per cell.</param>
/// <param name="BossGround">Floor of the boss hall, a distinct material.</param>
/// <param name="Bedrock">LM-07: opaque rock tile painted under enclosed blocked cells (those touching no
/// floor). The map's negative then reads as a solid rock massif instead of a hard-edged black void.</param>
/// <param name="WallH">Wall cell with open floor to the N/S only (the wall runs E–W).</param>
/// <param name="WallV">Wall cell with open floor to the E/W only (the wall runs N–S).</param>
/// <param name="WallPole">Wall cell with open floor on both axes (a junction nub).</param>
/// <param name="WallCorner">Outer corner: blocked on all four sides, open only on a diagonal. Also the
/// solid piece used for concave L-corners and for the bedrock massif (it fills the cell flush).</param>
/// <param name="Decor">Ambient props clustered inside rooms.</param>
/// <param name="DecorChance">Density of decor props per room (clustered, not per-cell scattered).</param>
/// <param name="Accent">Themed accent tiles (e.g. lava pools); empty = none.</param>
/// <param name="AccentChance">Density of accent pools per room (clustered).</param>
/// <param name="Atmosphere">G-07: per-stratum color-grade/light/fog/particle palette. Purely cosmetic
/// data the frontend renders as a post-process; the engine never reads it.</param>
public sealed record BiomeDef(
    ushort[] Ground, ushort[] BossGround, ushort Bedrock,
    ushort WallH, ushort WallV, ushort WallPole, ushort WallCorner,
    ushort[] Decor, double DecorChance,
    ushort[] Accent, double AccentChance,
    BiomeAtmosphere Atmosphere);

/// <summary>
/// G-07: cosmetic atmosphere of a stratum, sent verbatim to the renderer (no engine coupling).
/// Colors are 0–255 channels; strengths are 0–1. <see cref="ParticleDrift"/> is the vertical motion
/// of ambient motes: -1 rises (embers), +1 falls (ash/dust), 0 floats in place.
/// </summary>
public sealed record BiomeAtmosphere(
    string Name,
    byte TintR, byte TintG, byte TintB, double TintStrength,
    byte FogR, byte FogG, byte FogB, double FogStrength,
    double Vignette,
    byte ParticleR, byte ParticleG, byte ParticleB, double ParticleDensity, int ParticleDrift);

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
    // LM-07: bedrock backing for the interior massif. Opaque tiles so an enclosed cell never shows black
    // even where its wall sprite has alpha — the dirt body for cave, plain grey stone for the rest.
    private const ushort DirtBedrock = 356, StoneBedrock = 416;

    // G-07: one color-grade + light/fog/particle palette per stratum. A strong grade + drifting motes
    // is the cheapest way to make each estrato read as a distinct place while reusing the same tiles.
    private static readonly BiomeAtmosphere CaveAtmo = new(
        "Toca Ecoante", 255, 190, 120, 0.16, 40, 28, 18, 0.18, 0.34,
        216, 196, 156, 0.40, 0);                  // warm amber, floating dust
    private static readonly BiomeAtmosphere FortAtmo = new(
        "Forte Uruk", 200, 224, 178, 0.12, 120, 140, 110, 0.10, 0.22,
        222, 236, 172, 0.34, 0);                  // green daylight, drifting pollen
    private static readonly BiomeAtmosphere CryptAtmo = new(
        "Cripta Sombria", 132, 152, 210, 0.20, 28, 34, 56, 0.26, 0.46,
        176, 196, 232, 0.46, 1);                  // cold blue mist, settling motes
    private static readonly BiomeAtmosphere LairAtmo = new(
        "Covil Escamado", 255, 150, 90, 0.22, 50, 18, 12, 0.20, 0.40,
        255, 172, 84, 0.52, -1);                  // hot orange, rising embers
    private static readonly BiomeAtmosphere AbyssAtmo = new(
        "Abismo Ecoante", 182, 92, 202, 0.24, 30, 12, 38, 0.30, 0.52,
        222, 122, 232, 0.56, -1);                 // violet abyss, rising ash

    /// <summary>Tier 1 — Toca Ecoante: brown dirt cavern with boulders.</summary>
    public static readonly BiomeDef Cave = new(
        CaveGround, StoneGround, DirtBedrock, DirtH, DirtV, DirtPole, DirtCorner,
        CaveRocks, 0.025, [], 0, CaveAtmo);

    /// <summary>Tier 2 — Forte Uruk: grassy orc camp ringed by stone ruins.</summary>
    public static readonly BiomeDef Fort = new(
        GrassGround, StoneGround, StoneBedrock, StoneH, StoneV, StonePole, StoneCorner,
        CaveRocks, 0.02, [], 0, FortAtmo);

    /// <summary>Tier 3 — Cripta Sombria: mossy stone crypt strewn with bones.</summary>
    public static readonly BiomeDef Crypt = new(
        MossStone, StoneGround, StoneBedrock, StoneH, StoneV, StonePole, StoneCorner,
        Bones, 0.03, [], 0, CryptAtmo);

    /// <summary>Tier 4 — Covil Escamado: dark stone lair with decorative lava pools.</summary>
    public static readonly BiomeDef Lair = new(
        DarkStone, StoneGround, StoneBedrock, StoneH, StoneV, StonePole, StoneCorner,
        CaveRocks, 0.02, Lava, 0.05, LairAtmo);

    /// <summary>Tier 5 — Abismo Ecoante: stone abyss flooded with lava and bone.</summary>
    public static readonly BiomeDef Abyss = new(
        DarkStone, StoneGround, StoneBedrock, StoneH, StoneV, StonePole, StoneCorner,
        Bones, 0.02, Lava, 0.055, AbyssAtmo);

    public static BiomeDef ForTier(int tier) => tier switch
    {
        1 => Cave,
        2 => Fort,
        3 => Crypt,
        4 => Lair,
        5 => Abyss,
        _ => Cave,
    };

    /// <summary>
    /// LM-08: os 5 biomas canônicos como linhas chaveadas por tier — a fonte do seed do
    /// <c>ContentStore</c> (igual ao bloco de tiers). Editar um bioma no admin muda runs ao vivo, nunca
    /// estes defaults: a rede-ouro (LM-01) mede <see cref="ForTier"/> e fica verde sem rebaseline.
    /// </summary>
    public static IReadOnlyList<BiomeRow> AllDefaults() =>
    [
        new(1, "Toca Ecoante", Cave),
        new(2, "Forte Uruk", Fort),
        new(3, "Cripta Sombria", Crypt),
        new(4, "Covil Escamado", Lair),
        new(5, "Abismo Ecoante", Abyss),
    ];
}

/// <summary>
/// LM-08: um bioma serializável chaveado por tier, para o <c>ContentStore</c> persistir/editar.
/// <see cref="BiomeDef"/>/<see cref="BiomeAtmosphere"/> já serializam em System.Text.Json.
/// </summary>
public sealed record BiomeRow(int Tier, string Name, BiomeDef Def);
