namespace KaezanArenaFable.Api.Meta;

public interface IAccountRepository
{
    AccountState LoadOrCreate();
    void Save(AccountState state);
}
