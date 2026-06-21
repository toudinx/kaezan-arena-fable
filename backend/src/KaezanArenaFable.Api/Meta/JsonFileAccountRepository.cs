using System.Text.Json;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

public sealed class JsonFileAccountRepository
{
    private readonly string _path;
    private readonly ILogger<JsonFileAccountRepository> _logger;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public JsonFileAccountRepository(
        IWebHostEnvironment environment,
        ILogger<JsonFileAccountRepository> logger)
    {
        var directory = Path.Combine(environment.ContentRootPath, ".data");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "account.json");
        _logger = logger;
    }

    public AccountState LoadOrCreate()
    {
        var existing = TryLoadExisting();
        if (existing is not null) return existing;

        var fresh = AccountStateDefaults.Create();
        Save(fresh);
        return fresh;
    }

    public AccountState? TryLoadExisting()
    {
        if (!File.Exists(_path)) return null;

        try
        {
            return JsonSerializer.Deserialize<AccountState>(File.ReadAllText(_path));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not read account JSON at {Path}", _path);
            return null;
        }
    }

    public void Save(AccountState state) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(state, _json));
}

public sealed class JsonAccountRepository(JsonFileAccountRepository files) : IAccountRepository
{
    public AccountState LoadOrCreate() => files.LoadOrCreate();
    public void Save(AccountState state) => files.Save(state);
}

internal static class AccountStateDefaults
{
    public static AccountState Create() => new()
    {
        Kaeros = GameConfig.StartingKaeros,
        Gold = GameConfig.StartingGold,
        LastSeenUtc = DateTimeOffset.UtcNow.ToString("O"),
        OwnedWaifus = [Waifus.StarterWaifuId],
        ActiveWaifuId = Waifus.StarterWaifuId
    };
}
