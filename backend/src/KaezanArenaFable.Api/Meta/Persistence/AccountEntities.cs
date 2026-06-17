namespace KaezanArenaFable.Api.Meta.Persistence;

public sealed class AccountRow
{
    public string Id { get; set; } = "";
    public int Level { get; set; }
    public long Xp { get; set; }
    public long Gold { get; set; }
    public long Kaeros { get; set; }
    public string ActiveWaifuId { get; set; } = "";
    public string DailyDate { get; set; } = "";
    public string GiftsDate { get; set; } = "";
    public int RunsPlayed { get; set; }
    public int RunsWon { get; set; }
}

public sealed class AccountWaifuRow
{
    public string AccountId { get; set; } = "";
    public string WaifuId { get; set; } = "";
    public int Ascension { get; set; }
    public int Shards { get; set; }
    public long AffinityXp { get; set; }
    public int GiftsToday { get; set; }
    public string SelectedSkinId { get; set; } = "";
}

public sealed class AccountSkinRow
{
    public string AccountId { get; set; } = "";
    public string SkinId { get; set; } = "";
}

public sealed class AccountEquipmentRow
{
    public string AccountId { get; set; } = "";
    public string WaifuId { get; set; } = "";
    public int Tier { get; set; } = 1;
    public string Slot { get; set; } = "";
    public int ItemId { get; set; }
}

public sealed class AccountInventoryRow
{
    public string AccountId { get; set; } = "";
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

public sealed class AccountMasteryRow
{
    public string AccountId { get; set; } = "";
    public string WaifuId { get; set; } = "";
    public string NodeId { get; set; } = "";
}

public sealed class AccountMasteryPointsRow
{
    public string AccountId { get; set; } = "";
    public string WaifuId { get; set; } = "";
    public int Points { get; set; }
    public int Spent { get; set; }
}

public sealed class GachaPityRow
{
    public string AccountId { get; set; } = "";
    public string BannerId { get; set; } = "";
    public int SinceFive { get; set; }
    public int SinceFour { get; set; }
    public bool Guaranteed { get; set; }
    public int Total { get; set; }
}

public sealed class GachaHistoryRow
{
    public long Id { get; set; }
    public string AccountId { get; set; } = "";
    public string BannerId { get; set; } = "";
    public string WaifuId { get; set; } = "";
    public int Rarity { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class BestiaryRow
{
    public string AccountId { get; set; } = "";
    public string Species { get; set; } = "";
    public long Kills { get; set; }
}

public sealed class DailyContractRow
{
    public string AccountId { get; set; } = "";
    public string Date { get; set; } = "";
    public string ContractId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Param { get; set; } = "";
    public string Description { get; set; } = "";
    public long Target { get; set; }
    public long Progress { get; set; }
    public bool Claimed { get; set; }
}

public sealed class TierClearRow
{
    public string AccountId { get; set; } = "";
    public int Tier { get; set; }
    public int Clears { get; set; }
}

public sealed class RunResultRow
{
    public long Id { get; set; }
    public string AccountId { get; set; } = "";
    public ulong Seed { get; set; }
    public int Tier { get; set; }
    public string WaifuId { get; set; } = "";
    public string Outcome { get; set; } = "";
    public int Kills { get; set; }
    public int RunLevel { get; set; }
    public long DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class ReplayRow
{
    public long Id { get; set; }
    public string AccountId { get; set; } = "";
    public ulong Seed { get; set; }
    public int Tier { get; set; }
    public string CommandsJson { get; set; } = "";
    public string FinalHash { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public sealed class DailyChallengeScoreRow
{
    public string Date { get; set; } = "";
    public string AccountId { get; set; } = "";
    public long Score { get; set; }
    public long TimeMs { get; set; }
}
