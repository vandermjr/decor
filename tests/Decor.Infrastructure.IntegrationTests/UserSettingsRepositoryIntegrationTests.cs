using Dapper;
using Decor.Core.Configuration;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class UserSettingsRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    private UserSettingsRepository CreateRepository() => new(new DatabaseConnection(fixture.ConnectionString));

    [Fact]
    public async Task GetAsync_WhenSettingDoesNotExist_ReturnsNull()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var setting = await repository.GetAsync(userId, DecorUserSettings.Theme);

        setting.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WhenSettingDoesNotExist_InsertsConfiguration()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.SetAsync(userId, DecorUserSettings.Theme, "Dark", UserSettingValueType.String);

        var setting = await repository.GetAsync(userId, DecorUserSettings.Theme);
        setting.Should().NotBeNull();
        setting!.SettingValue.Should().Be("Dark");
        setting.ValueType.Should().Be(UserSettingValueType.String.ToString());
    }

    [Fact]
    public async Task GetAsync_AfterInsert_ReturnsPersistedSetting()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        await repository.SetAsync(userId, DecorUserSettings.Language, "pt-BR", UserSettingValueType.String);

        var setting = await repository.GetAsync(userId, DecorUserSettings.Language);

        setting.Should().NotBeNull();
        setting!.SettingKey.Should().Be(DecorUserSettings.Language);
        setting.SettingValue.Should().Be("pt-BR");
    }

    [Fact]
    public async Task SetAsync_WhenSettingExists_UpdatesValueAndType()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        await repository.SetAsync(userId, DecorUserSettings.Theme, "Light", UserSettingValueType.String);

        await repository.SetAsync(userId, DecorUserSettings.Theme, "Dark", UserSettingValueType.String);

        var setting = await repository.GetAsync(userId, DecorUserSettings.Theme);
        setting.Should().NotBeNull();
        setting!.SettingValue.Should().Be("Dark");
        setting.ValueType.Should().Be(UserSettingValueType.String.ToString());
    }

    [Fact]
    public async Task GetAsync_IsolatesSettingsBetweenDifferentUsers()
    {
        var firstUserId = await CreateUserAsync();
        var secondUserId = await CreateUserAsync();
        var repository = CreateRepository();
        await repository.SetAsync(firstUserId, DecorUserSettings.Theme, "Dark", UserSettingValueType.String);

        var first = await repository.GetAsync(firstUserId, DecorUserSettings.Theme);
        var second = await repository.GetAsync(secondUserId, DecorUserSettings.Theme);

        first.Should().NotBeNull();
        first!.SettingValue.Should().Be("Dark");
        second.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_UsesCompositeKeyToPreventDuplicateSettingForSameUser() 
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        await repository.SetAsync(userId, DecorUserSettings.Theme, "Dark", UserSettingValueType.String);
        await repository.SetAsync(userId, DecorUserSettings.Theme, "Light", UserSettingValueType.String);

        var count = await CountSettingsForUserAsync(userId, DecorUserSettings.Theme);

        count.Should().Be(1);

        var setting = await repository.GetAsync(userId, DecorUserSettings.Theme);
        setting!.SettingValue.Should().Be("Light");
    }

    [Fact]
    public async Task SetAsync_PersistsThemeAndLanguageValues()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.SetAsync(userId, DecorUserSettings.Theme, "Dark", UserSettingValueType.String);
        await repository.SetAsync(userId, DecorUserSettings.Language, "en-US", UserSettingValueType.String);

        var all = await repository.GetAllAsync(userId);
        all.Should().ContainKey(DecorUserSettings.Theme);
        all.Should().ContainKey(DecorUserSettings.Language);
        all[DecorUserSettings.Theme].SettingValue.Should().Be("Dark");
        all[DecorUserSettings.Language].SettingValue.Should().Be("en-US");
    }

    private async Task<int> CreateUserAsync()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        var username = $"user_{Guid.NewGuid():N}";
        var userId = await connection.QuerySingleAsync<int>(
            "INSERT INTO users (Username, DisplayName, PasswordHash, IsActive, MustChangePassword) VALUES (@Username, @DisplayName, @PasswordHash, 1, 0); SELECT LAST_INSERT_ID();",
            new { Username = username, DisplayName = username, PasswordHash = "hash" });
        return userId;
    }

    private async Task<int> CountSettingsForUserAsync(int userId, string settingKey)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM user_settings WHERE UserID = @UserId AND SettingKey = @SettingKey;",
            new { UserId = userId, SettingKey = settingKey });
    }
}
