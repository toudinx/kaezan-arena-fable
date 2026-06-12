using System.Text.Json;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Single local account persisted to a JSON file (kaezan-arena convention).
/// All mutations go through Mutate() which serializes under a lock.
/// </summary>
public sealed class AccountStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private AccountState _state;

    public AccountStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, ".data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "account.json");
        _state = Load();
    }

    private AccountState Load()
    {
        if (File.Exists(_path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<AccountState>(File.ReadAllText(_path));
                if (loaded is not null) return loaded;
            }
            catch (Exception)
            {
                // corrupt file: start fresh below
            }
        }
        var fresh = new AccountState
        {
            Kaeros = GameConfig.StartingKaeros,
            Gold = GameConfig.StartingGold,
            OwnedWaifus = [Waifus.StarterWaifuId],
            ActiveWaifuId = Waifus.StarterWaifuId
        };
        Save(fresh);
        return fresh;
    }

    private void Save(AccountState state) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(state, _json));

    public T Read<T>(Func<AccountState, T> reader)
    {
        lock (_lock) return reader(_state);
    }

    public T Mutate<T>(Func<AccountState, T> mutator)
    {
        lock (_lock)
        {
            var result = mutator(_state);
            Save(_state);
            return result;
        }
    }

    public void Mutate(Action<AccountState> mutator) => Mutate(s => { mutator(s); return true; });
}
