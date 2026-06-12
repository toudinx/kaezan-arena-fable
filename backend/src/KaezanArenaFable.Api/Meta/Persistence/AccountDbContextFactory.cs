using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KaezanArenaFable.Api.Meta.Persistence;

public sealed class AccountDbContextFactory : IDesignTimeDbContextFactory<AccountDbContext>
{
    public AccountDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseMySql(
                "Server=localhost;Database=kaezan_fable;User=root;Password=;",
                new MariaDbServerVersion(new Version(10, 4, 0)))
            .Options;
        return new AccountDbContext(options);
    }
}
