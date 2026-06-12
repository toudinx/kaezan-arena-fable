using Microsoft.EntityFrameworkCore;

namespace KaezanArenaFable.Api.Meta.Persistence;

public static class AccountPersistenceRegistration
{
    public static IServiceCollection AddAccountPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<JsonFileAccountRepository>();

        var connectionString = configuration.GetConnectionString("KaezanFable");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IAccountRepository, JsonAccountRepository>();
            return services;
        }

        services.AddSingleton(new MySqlDatabaseBootstrapper(connectionString));
        services.AddDbContextFactory<AccountDbContext>((provider, options) =>
        {
            var bootstrapper = provider.GetRequiredService<MySqlDatabaseBootstrapper>();
            options.UseMySql(bootstrapper.ConnectionString, bootstrapper.Prepare());
        });
        services.AddSingleton<IAccountRepository, MySqlAccountRepository>();
        return services;
    }
}
