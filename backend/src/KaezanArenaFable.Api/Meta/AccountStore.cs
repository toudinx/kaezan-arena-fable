namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Synchronous facade for the single local account. Persistence is selected at startup.
/// All mutations are serialized here and committed as one repository transaction.
/// </summary>
public sealed class AccountStore
{
    private readonly IAccountRepository _repository;
    private readonly object _lock = new();
    private AccountState _state;

    public AccountStore(IAccountRepository repository, Domain.GameData data, Domain.KaeliRegistry kaelis)
    {
        _repository = repository;
        _state = repository.LoadOrCreate();
        if (AccountSanitizer.Sanitize(_state, data, kaelis))
            _repository.Save(_state);
    }

    public T Read<T>(Func<AccountState, T> reader)
    {
        lock (_lock) return reader(_state);
    }

    public T Mutate<T>(Func<AccountState, T> mutator)
    {
        lock (_lock)
        {
            var next = Clone(_state);
            var result = mutator(next);
            _repository.Save(next);
            _state = next;
            return result;
        }
    }

    public void Mutate(Action<AccountState> mutator) => Mutate(s => { mutator(s); return true; });

    private static AccountState Clone(AccountState source) => new()
    {
        Id = source.Id,
        AccountLevel = source.AccountLevel,
        AccountXp = source.AccountXp,
        Gold = source.Gold,
        Kaeros = source.Kaeros,
        OwnedWaifus = [.. source.OwnedWaifus],
        Shards = source.Shards.ToDictionary(),
        Ascension = source.Ascension.ToDictionary(),
        ActiveWaifuId = source.ActiveWaifuId,
        AffinityXp = source.AffinityXp.ToDictionary(),
        GiftsDate = source.GiftsDate,
        GiftsToday = source.GiftsToday.ToDictionary(),
        OwnedSkins = [.. source.OwnedSkins],
        SelectedSkins = source.SelectedSkins.ToDictionary(),
        Mastery = source.Mastery.ToDictionary(
            entry => entry.Key,
            entry => new MasteryState
            {
                Points = entry.Value.Points,
                Spent = entry.Value.Spent,
                Nodes = [.. entry.Value.Nodes]
            }),
        Pity = source.Pity.ToDictionary(
            entry => entry.Key,
            entry => new PityState
            {
                PullsSinceFiveStar = entry.Value.PullsSinceFiveStar,
                PullsSinceFourStar = entry.Value.PullsSinceFourStar,
                FeaturedGuaranteed = entry.Value.FeaturedGuaranteed,
                TotalPulls = entry.Value.TotalPulls
            }),
        BestiaryKills = source.BestiaryKills.ToDictionary(),
        Inventory = source.Inventory.ToDictionary(
            entry => entry.Key,
            entry => new InventoryStack
            {
                ItemId = entry.Value.ItemId,
                Name = entry.Value.Name,
                Count = entry.Value.Count
            }),
        Equipment = source.Equipment.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToDictionary()),
        DailyDate = source.DailyDate,
        Dailies = source.Dailies.Select(contract => new DailyContract
        {
            Id = contract.Id,
            Kind = contract.Kind,
            Param = contract.Param,
            Description = contract.Description,
            Target = contract.Target,
            Progress = contract.Progress,
            Claimed = contract.Claimed
        }).ToList(),
        RunsPlayed = source.RunsPlayed,
        RunsWon = source.RunsWon,
        TierClears = source.TierClears.ToDictionary()
    };
}
