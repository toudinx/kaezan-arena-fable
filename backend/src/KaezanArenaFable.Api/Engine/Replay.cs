using System.IO.Compression;
using System.Text.Json;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>One command as it was APPLIED by the engine (post card-pause filtering), pinned to the
/// tick that consumed it. Replaying = enqueueing every entry of tick N right before ticking N.</summary>
public sealed record ReplayCommandEntry(long Tick, CommandKind Kind, int A, int B, string? S);

/// <summary>Canonical state hash recorded at the end of tick <see cref="Tick"/> (before the
/// snapshot is built). Lets --replay-check bisect the first divergent tick.</summary>
public sealed record ReplayHashEntry(long Tick, string Hash);

/// <summary>
/// FF-01: everything needed to re-simulate a run bit-perfectly without SignalR/account state.
/// Small editable content (tier def, role tunings, biome, equipment, mastery aggregates) is FROZEN
/// verbatim; large stable content (WaifuDef, monster species, items) is resolved by id at check
/// time — editing that content invalidates old replays, which is acceptable for a debug artifact.
/// </summary>
public sealed record ReplayFile(
    int Version,
    long Seed,
    GameMode Mode,
    string WaifuId,
    int Ascension,
    DungeonTier TierDef,
    EquipmentStats EquipmentStats,
    int AffinityLevel,
    MasteryAggregates Mastery,
    string SkinId,
    Dictionary<string, long> BestiaryKills,
    string? HelperProfile,
    Dictionary<KaeliRole, RoleTuning> RoleTunings,
    BiomeDef Biome,
    long FinalTick,
    string FinalHash,
    List<ReplayHashEntry> TickHashes,
    List<ReplayCommandEntry> Commands)
{
    public const int CurrentVersion = 1;
}

/// <summary>Gzip-JSON persistence for <see cref="ReplayFile"/>, shared by the live backend
/// (RunManager) and the headless checker (tools/BalanceSim --replay-check).</summary>
public static class ReplayIo
{
    public const string Extension = ".replay.json.gz";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static void Save(ReplayFile replay, string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var file = File.Create(path);
        using var gz = new GZipStream(file, CompressionLevel.Fastest);
        JsonSerializer.Serialize(gz, replay, Json);
    }

    public static ReplayFile Load(string path)
    {
        using var file = File.OpenRead(path);
        using Stream stream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(file, CompressionMode.Decompress)
            : file;
        return JsonSerializer.Deserialize<ReplayFile>(stream, Json)
               ?? throw new InvalidDataException($"empty replay file: {path}");
    }
}
