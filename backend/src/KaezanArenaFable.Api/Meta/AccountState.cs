namespace KaezanArenaFable.Api.Meta;

public sealed class PityState
{
    public int PullsSinceFiveStar { get; set; }
    public int PullsSinceFourStar { get; set; }
    public bool FeaturedGuaranteed { get; set; }
    public int TotalPulls { get; set; }
}

public sealed class DailyContract
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = ""; // kill_species | clear_tier | open_chests | collect_gold
    public string Param { get; set; } = "";
    public string Description { get; set; } = "";
    public long Target { get; set; }
    public long Progress { get; set; }
    public bool Claimed { get; set; }
}

public sealed class AccountState
{
    public string Id { get; set; } = "local";
    public int AccountLevel { get; set; } = 1;
    public long AccountXp { get; set; }
    public long Gold { get; set; }
    public long Kaeros { get; set; }

    public List<string> OwnedWaifus { get; set; } = [];
    public Dictionary<string, int> Shards { get; set; } = [];
    public Dictionary<string, int> Ascension { get; set; } = [];
    public string ActiveWaifuId { get; set; } = "";

    public Dictionary<string, PityState> Pity { get; set; } = [];
    public Dictionary<string, long> BestiaryKills { get; set; } = [];
    public Dictionary<int, InventoryStack> Inventory { get; set; } = [];
    public Dictionary<string, Dictionary<string, int>> Equipment { get; set; } = [];

    public string DailyDate { get; set; } = "";
    public List<DailyContract> Dailies { get; set; } = [];

    public int RunsPlayed { get; set; }
    public int RunsWon { get; set; }
    public Dictionary<int, int> TierClears { get; set; } = [];
}

public sealed class InventoryStack
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public int Count { get; set; }
}
