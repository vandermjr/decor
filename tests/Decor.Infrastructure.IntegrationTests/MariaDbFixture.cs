using Dapper;
using MySqlConnector;
using Testcontainers.MariaDb;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class MariaDbFixture : IAsyncLifetime
{
    private readonly MariaDbContainer _container = new MariaDbBuilder("mariadb:10.11.6")
        .WithDatabase("decor_integration")
        .WithUsername("decor_test")
        .WithPassword("decor_test_password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var connection = new MySqlConnection(ConnectionString);
        var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");
        foreach (var migration in new[]
                 {
                     "20260830_add_authentication_authorization.sql",
                     "20260831_add_users_must_change_password.sql",
                     "20260901_add_administration_security.sql",
                     "20260902_harden_administration_security.sql"
                 })
        {
            await connection.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(migrationsDirectory, migration)));
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
