using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace KaezanArenaFable.Api.Meta.Persistence;

public sealed class MySqlDatabaseBootstrapper
{
    public const string DatabaseName = "kaezan_fable";

    private readonly string _connectionString;
    private readonly object _lock = new();
    private ServerVersion? _serverVersion;

    public MySqlDatabaseBootstrapper(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Kaezan Fable connection string must target the separate '{DatabaseName}' database.");
        }

        _connectionString = builder.ConnectionString;
    }

    public string ConnectionString => _connectionString;

    public ServerVersion Prepare()
    {
        lock (_lock)
        {
            if (_serverVersion is not null) return _serverVersion;

            var adminBuilder = new MySqlConnectionStringBuilder(_connectionString)
            {
                Database = ""
            };
            using var connection = new MySqlConnection(adminBuilder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"CREATE DATABASE IF NOT EXISTS `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
            command.ExecuteNonQuery();

            _serverVersion = ServerVersion.AutoDetect(_connectionString);
            return _serverVersion;
        }
    }
}
