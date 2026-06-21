using Microsoft.EntityFrameworkCore;

namespace KaezanArenaFable.Api.Meta.Persistence;

public sealed class MySqlAccountRepository(
    IDbContextFactory<AccountDbContext> contextFactory,
    JsonFileAccountRepository jsonFiles,
    ILogger<MySqlAccountRepository> logger) : IAccountRepository
{
    private bool _initialized;

    public AccountState LoadOrCreate()
    {
        EnsureInitialized();
        using var db = contextFactory.CreateDbContext();
        var account = db.Accounts.AsNoTracking().OrderBy(row => row.Id).FirstOrDefault();
        if (account is null)
        {
            var imported = jsonFiles.TryLoadExisting();
            var state = imported ?? AccountStateDefaults.Create();
            Save(state);
            logger.LogInformation(
                imported is null
                    ? "Created fresh account {AccountId} in MySQL"
                    : "Imported account {AccountId} from JSON into MySQL",
                state.Id);
            return state;
        }

        return LoadState(db, account);
    }

    public void Save(AccountState state)
    {
        EnsureInitialized();
        using var db = contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();

        var account = db.Accounts.SingleOrDefault(row => row.Id == state.Id);
        if (account is null)
        {
            account = new AccountRow { Id = state.Id };
            db.Accounts.Add(account);
        }
        MapAccount(state, account);
        db.SaveChanges();

        db.AccountWaifus.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.AccountSkins.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.AccountMastery.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.AccountMasteryPoints.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.AccountEquipment.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.AccountInventory.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.GachaPity.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.Bestiary.Where(row => row.AccountId == state.Id).ExecuteDelete();
        db.DailyContracts
            .Where(row => row.AccountId == state.Id && row.Date == state.DailyDate)
            .ExecuteDelete();
        db.TierClears.Where(row => row.AccountId == state.Id).ExecuteDelete();

        db.AccountWaifus.AddRange(state.OwnedWaifus.Distinct(StringComparer.Ordinal).Select(waifuId =>
            new AccountWaifuRow
            {
                AccountId = state.Id,
                WaifuId = waifuId,
                Ascension = state.Ascension.GetValueOrDefault(waifuId),
                Shards = state.Shards.GetValueOrDefault(waifuId),
                AffinityXp = state.AffinityXp.GetValueOrDefault(waifuId),
                GiftsToday = state.GiftsToday.GetValueOrDefault(waifuId),
                SelectedSkinId = state.SelectedSkins.GetValueOrDefault(waifuId, "")
            }));
        db.AccountSkins.AddRange(state.OwnedSkins.Distinct(StringComparer.Ordinal).Select(skinId =>
            new AccountSkinRow { AccountId = state.Id, SkinId = skinId }));
        db.AccountMastery.AddRange(state.Mastery.SelectMany(entry =>
            entry.Value.Nodes.Select(nodeId => new AccountMasteryRow
            {
                AccountId = state.Id,
                WaifuId = entry.Key,
                NodeId = nodeId
            })));
        db.AccountMasteryPoints.AddRange(state.Mastery
            .Where(entry => entry.Value.Points != 0 || entry.Value.Spent != 0)
            .Select(entry => new AccountMasteryPointsRow
            {
                AccountId = state.Id,
                WaifuId = entry.Key,
                Points = entry.Value.Points,
                Spent = entry.Value.Spent
            }));
        db.AccountInventory.AddRange(state.Inventory.Values.Select(stack => new AccountInventoryRow
        {
            AccountId = state.Id,
            ItemId = stack.ItemId,
            Name = stack.Name,
            Count = stack.Count
        }));
        db.AccountEquipment.AddRange(state.Equipment.SelectMany(entry =>
        {
            var (waifuId, tier) = AccountState.ParseEquipKey(entry.Key);
            return entry.Value.Select(slot => new AccountEquipmentRow
            {
                AccountId = state.Id,
                WaifuId = waifuId,
                Tier = tier,
                Slot = slot.Key,
                ItemId = slot.Value
            });
        }));
        db.GachaPity.AddRange(state.Pity.Select(entry => new GachaPityRow
        {
            AccountId = state.Id,
            BannerId = entry.Key,
            SinceFive = entry.Value.PullsSinceFiveStar,
            SinceFour = entry.Value.PullsSinceFourStar,
            Guaranteed = entry.Value.FeaturedGuaranteed,
            Total = entry.Value.TotalPulls
        }));
        db.Bestiary.AddRange(state.BestiaryKills.Select(entry => new BestiaryRow
        {
            AccountId = state.Id,
            Species = entry.Key,
            Kills = entry.Value
        }));
        db.DailyContracts.AddRange(state.Dailies.Select(contract => new DailyContractRow
        {
            AccountId = state.Id,
            Date = state.DailyDate,
            ContractId = contract.Id,
            Kind = contract.Kind,
            Param = contract.Param,
            Description = contract.Description,
            Target = contract.Target,
            Progress = contract.Progress,
            Claimed = contract.Claimed
        }));
        db.TierClears.AddRange(state.TierClears.Select(entry => new TierClearRow
        {
            AccountId = state.Id,
            Tier = entry.Key,
            Clears = entry.Value
        }));

        db.SaveChanges();
        transaction.Commit();
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        using var db = contextFactory.CreateDbContext();
        db.Database.Migrate();
        _initialized = true;
    }

    private static AccountState LoadState(AccountDbContext db, AccountRow account)
    {
        var state = new AccountState
        {
            Id = account.Id,
            AccountLevel = account.Level,
            AccountXp = account.Xp,
            Gold = account.Gold,
            Kaeros = account.Kaeros,
            LastSeenUtc = account.LastSeenUtc,
            ActiveWaifuId = account.ActiveWaifuId,
            DailyDate = account.DailyDate,
            GiftsDate = account.GiftsDate,
            RunsPlayed = account.RunsPlayed,
            RunsWon = account.RunsWon
        };

        foreach (var row in db.AccountWaifus.AsNoTracking().Where(row => row.AccountId == account.Id))
        {
            state.OwnedWaifus.Add(row.WaifuId);
            if (row.Ascension != 0) state.Ascension[row.WaifuId] = row.Ascension;
            if (row.Shards != 0) state.Shards[row.WaifuId] = row.Shards;
            if (row.AffinityXp != 0) state.AffinityXp[row.WaifuId] = row.AffinityXp;
            if (row.GiftsToday != 0) state.GiftsToday[row.WaifuId] = row.GiftsToday;
            if (!string.IsNullOrEmpty(row.SelectedSkinId)) state.SelectedSkins[row.WaifuId] = row.SelectedSkinId;
        }

        foreach (var row in db.AccountSkins.AsNoTracking().Where(row => row.AccountId == account.Id))
            state.OwnedSkins.Add(row.SkinId);

        foreach (var row in db.AccountMasteryPoints.AsNoTracking().Where(row => row.AccountId == account.Id))
            state.Mastery[row.WaifuId] = new MasteryState { Points = row.Points, Spent = row.Spent };

        foreach (var row in db.AccountMastery.AsNoTracking()
                     .Where(row => row.AccountId == account.Id).OrderBy(row => row.NodeId))
        {
            if (!state.Mastery.TryGetValue(row.WaifuId, out var mastery))
                state.Mastery[row.WaifuId] = mastery = new MasteryState();
            mastery.Nodes.Add(row.NodeId);
        }

        foreach (var row in db.AccountInventory.AsNoTracking().Where(row => row.AccountId == account.Id))
            state.Inventory[row.ItemId] = new InventoryStack { ItemId = row.ItemId, Name = row.Name, Count = row.Count };

        foreach (var row in db.AccountEquipment.AsNoTracking().Where(row => row.AccountId == account.Id))
        {
            var key = AccountState.EquipKey(row.WaifuId, row.Tier);
            if (!state.Equipment.TryGetValue(key, out var loadout))
                state.Equipment[key] = loadout = [];
            loadout[row.Slot] = row.ItemId;
        }

        foreach (var row in db.GachaPity.AsNoTracking().Where(row => row.AccountId == account.Id))
        {
            state.Pity[row.BannerId] = new PityState
            {
                PullsSinceFiveStar = row.SinceFive,
                PullsSinceFourStar = row.SinceFour,
                FeaturedGuaranteed = row.Guaranteed,
                TotalPulls = row.Total
            };
        }

        foreach (var row in db.Bestiary.AsNoTracking().Where(row => row.AccountId == account.Id))
            state.BestiaryKills[row.Species] = row.Kills;

        state.Dailies = db.DailyContracts.AsNoTracking()
            .Where(row => row.AccountId == account.Id && row.Date == account.DailyDate)
            .OrderBy(row => row.ContractId)
            .Select(row => new DailyContract
            {
                Id = row.ContractId,
                Kind = row.Kind,
                Param = row.Param,
                Description = row.Description,
                Target = row.Target,
                Progress = row.Progress,
                Claimed = row.Claimed
            })
            .ToList();

        foreach (var row in db.TierClears.AsNoTracking().Where(row => row.AccountId == account.Id))
            state.TierClears[row.Tier] = row.Clears;

        return state;
    }

    private static void MapAccount(AccountState state, AccountRow account)
    {
        account.Level = state.AccountLevel;
        account.Xp = state.AccountXp;
        account.Gold = state.Gold;
        account.Kaeros = state.Kaeros;
        account.LastSeenUtc = state.LastSeenUtc;
        account.ActiveWaifuId = state.ActiveWaifuId;
        account.DailyDate = state.DailyDate;
        account.GiftsDate = state.GiftsDate;
        account.RunsPlayed = state.RunsPlayed;
        account.RunsWon = state.RunsWon;
    }
}
