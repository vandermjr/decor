using Dapper;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using Decor.Application.Services;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class SystemSettingsRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task SetAsync_InsertsAndUpdatesGlobalSettings()
    {
        var key = $"integration:{Guid.NewGuid():N}";
        var repository = new SystemSettingsRepository(new DatabaseConnection(fixture.ConnectionString));

        await repository.SetAsync(new Dictionary<string, string> { [key] = "initial" });
        await repository.SetAsync(new Dictionary<string, string> { [key] = "updated" });

        var all = await repository.GetAllAsync();
        all[key].Should().Be("updated");

        await using var connection = new MySqlConnection(fixture.ConnectionString);
        var count = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM system_settings WHERE SettingKey = @Key;", new { Key = key });
        count.Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_CreatesMissingTableAndCopiesAdministratorIconAppearance()
    {
        const string weightKey = "IconWeight";
        const string thicknessKey = "IconStrokeThickness";
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        var administratorId = await connection.QuerySingleAsync<int>(
            "SELECT UserID FROM users WHERE Username = 'admin' LIMIT 1;");
        await connection.ExecuteAsync(
            "DELETE FROM user_settings WHERE UserID = @UserId AND SettingKey IN (@WeightKey, @ThicknessKey); DROP TABLE system_settings;",
            new { UserId = administratorId, WeightKey = weightKey, ThicknessKey = thicknessKey });
        await connection.ExecuteAsync(
            "INSERT INTO user_settings (UserID, SettingKey, SettingValue, ValueType) VALUES (@UserId, @WeightKey, '600', 'String'), (@UserId, @ThicknessKey, '1.3', 'String');",
            new { UserId = administratorId, WeightKey = weightKey, ThicknessKey = thicknessKey });

        try
        {
            var service = new IconAppearanceService(new SystemSettingsRepository(new DatabaseConnection(fixture.ConnectionString)));
            await service.InitializeAsync();

            service.CurrentAppearance.MaterialSymbolWeight.Should().Be(600);
            service.CurrentAppearance.StrokeThickness.Should().Be(1.3);
            var settings = await new SystemSettingsRepository(new DatabaseConnection(fixture.ConnectionString)).GetAllAsync();
            settings[weightKey].Should().Be("600");
            settings[thicknessKey].Should().Be("1.3");
        }
        finally
        {
            await connection.ExecuteAsync(
                "DELETE FROM system_settings WHERE SettingKey IN (@WeightKey, @ThicknessKey); DELETE FROM user_settings WHERE UserID = @UserId AND SettingKey IN (@WeightKey, @ThicknessKey);",
                new { UserId = administratorId, WeightKey = weightKey, ThicknessKey = thicknessKey });
        }
    }
}