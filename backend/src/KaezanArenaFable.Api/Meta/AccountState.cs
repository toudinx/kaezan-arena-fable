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

/// <summary>Echo Mastery per Kaeli: available points, spent points and unlocked nodes.</summary>
public sealed class MasteryState
{
    public int Points { get; set; }
    public int Spent { get; set; }
    public List<string> Nodes { get; set; } = [];
}

public sealed class AccountState
{
    public string Id { get; set; } = "local";
    public int AccountLevel { get; set; } = 1;
    public long AccountXp { get; set; }
    public long Gold { get; set; }
    public long Kaeros { get; set; }
    public string LastSeenUtc { get; set; } = "";

    public List<string> OwnedWaifus { get; set; } = [];
    public Dictionary<string, int> Shards { get; set; } = [];
    public Dictionary<string, int> Ascension { get; set; } = [];
    public string ActiveWaifuId { get; set; } = "";

    // Kaeli depth: affinity, daily gifts, skins, and mastery.
    public Dictionary<string, long> AffinityXp { get; set; } = [];
    public string GiftsDate { get; set; } = "";
    public Dictionary<string, int> GiftsToday { get; set; } = [];
    public List<string> OwnedSkins { get; set; } = [];
    public Dictionary<string, string> SelectedSkins { get; set; } = [];
    public Dictionary<string, MasteryState> Mastery { get; set; } = [];

    public Dictionary<string, PityState> Pity { get; set; } = [];
    public Dictionary<string, long> BestiaryKills { get; set; } = [];
    public Dictionary<int, InventoryStack> Inventory { get; set; } = [];
    public Dictionary<string, Dictionary<string, int>> Equipment { get; set; } = [];

    public string DailyDate { get; set; } = "";
    public List<DailyContract> Dailies { get; set; } = [];

    public int RunsPlayed { get; set; }
    public int RunsWon { get; set; }
    public Dictionary<int, int> TierClears { get; set; } = [];

    /// <summary>G-10: default helper config per Kaeli — "targeting|skills|ult|pref|movement|autoheal|nav".</summary>
    public Dictionary<string, string> HelperProfiles { get; set; } = [];

    /// <summary>Equipment loadout key: one set per Kaeli PER tier ("waifu:x#3").</summary>
    public static string EquipKey(string waifuId, int tier) => $"{waifuId}#{tier}";

    /// <summary>Decomposes the loadout key. Legacy key (without "#tier") falls back to tier 1.</summary>
    public static (string WaifuId, int Tier) ParseEquipKey(string key)
    {
        var hash = key.LastIndexOf('#');
        return hash > 0 && int.TryParse(key[(hash + 1)..], out var tier)
            ? (key[..hash], tier)
            : (key, 1);
    }
}

public sealed class InventoryStack
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public int Count { get; set; }
}
