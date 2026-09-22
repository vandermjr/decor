using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;

namespace Decor.Application.Tests;

public sealed class UserSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_AuthenticatedUserWithoutSettings_ReturnsDefaults()
    {
        var repository = new FakeUserSettingsRepository();
        var context = AuthenticatedContext(7);
        var service = new UserSettingsService(repository, context);

        var settings = await service.GetAsync();

        settings.Theme.Should().Be(DecorDefaults.Theme);
        settings.Language.Should().Be(DecorDefaults.DefaultLanguage.Name);
        repository.RequestedUserId.Should().Be(7);
    }

    [Fact]
    public async Task GetAsync_LoadsPersistedTheme()
    {
        var repository = new FakeUserSettingsRepository
        {
            Settings = new Dictionary<string, UserSetting>
            {
                [DecorUserSettings.Theme] = Setting(DecorUserSettings.Theme, "Light")
            }
        };
        var service = new UserSettingsService(repository, AuthenticatedContext(7));

        var settings = await service.GetAsync();

        settings.Theme.Should().Be(DecorThemeStyle.Light);
        settings.Language.Should().Be(DecorDefaults.DefaultLanguage.Name);
    }

    [Fact]
    public async Task GetAsync_LoadsPersistedLanguage()
    {
        var repository = new FakeUserSettingsRepository
        {
            Settings = new Dictionary<string, UserSetting>
            {
                [DecorUserSettings.Language] = Setting(DecorUserSettings.Language, "en-US")
            }
        };
        var service = new UserSettingsService(repository, AuthenticatedContext(7));

        var settings = await service.GetAsync();

        settings.Language.Should().Be("en-US");
        settings.Theme.Should().Be(DecorDefaults.Theme);
    }

    [Fact]
    public async Task GetAsync_LoadsPersistedThemeAndLanguage()
    {
        var repository = new FakeUserSettingsRepository
        {
            Settings = new Dictionary<string, UserSetting>
            {
                [DecorUserSettings.Theme] = Setting(DecorUserSettings.Theme, "Light"),
                [DecorUserSettings.Language] = Setting(DecorUserSettings.Language, "en-US")
            }
        };
        var service = new UserSettingsService(repository, AuthenticatedContext(7));

        var settings = await service.GetAsync();

        settings.Theme.Should().Be(DecorThemeStyle.Light);
        settings.Language.Should().Be("en-US");
    }

    [Fact]
    public async Task GetAsync_AnonymousUser_ReturnsDefaultsWithoutRepositoryCall()
    {
        var repository = new FakeUserSettingsRepository();
        var service = new UserSettingsService(repository, new RecordingAuthenticatedUserContext());

        var settings = await service.GetAsync();

        settings.Theme.Should().Be(DecorDefaults.Theme);
        settings.Language.Should().Be(DecorDefaults.DefaultLanguage.Name);
        repository.GetAllCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_InvalidTheme_ReturnsDefaultTheme()
    {
        var repository = new FakeUserSettingsRepository
        {
            Settings = new Dictionary<string, UserSetting>
            {
                [DecorUserSettings.Theme] = Setting(DecorUserSettings.Theme, "Invalid")
            }
        };
        var service = new UserSettingsService(repository, AuthenticatedContext(7));

        var settings = await service.GetAsync();

        settings.Theme.Should().Be(DecorDefaults.Theme);
    }

    [Fact]
    public async Task GetAsync_UndefinedNumericTheme_ReturnsDefaultTheme()
    {
        var repository = new FakeUserSettingsRepository
        {
            Settings = new Dictionary<string, UserSetting>
            {
                [DecorUserSettings.Theme] = Setting(DecorUserSettings.Theme, "999")
            }
        };
        var service = new UserSettingsService(repository, AuthenticatedContext(7));

        var settings = await service.GetAsync();

        settings.Theme.Should().Be(DecorDefaults.Theme);
    }

    [Fact]
    public async Task SetThemeAsync_PersistsThemeKeyAndStringValue()
    {
        var repository = new FakeUserSettingsRepository();
        var service = new UserSettingsService(repository, AuthenticatedContext(7));

        await service.SetThemeAsync(DecorThemeStyle.Light);

        repository.LastSet.Should().Be((7, DecorUserSettings.Theme, "Light", UserSettingValueType.String));
    }

    [Fact]
    public async Task SetLanguageAsync_PersistsLanguageKeyAndStringValue()
    {
        var repository = new FakeUserSettingsRepository();
        var service = new UserSettingsService(repository, AuthenticatedContext(7));

        await service.SetLanguageAsync("en-US");

        repository.LastSet.Should().Be((7, DecorUserSettings.Language, "en-US", UserSettingValueType.String));
    }

    private static RecordingAuthenticatedUserContext AuthenticatedContext(int userId)
    {
        var context = new RecordingAuthenticatedUserContext();
        context.SignIn(new ApplicationUser { UserID = userId });
        return context;
    }

    private static UserSetting Setting(string key, string value) => new()
    {
        UserID = 7,
        SettingKey = key,
        SettingValue = value,
        ValueType = UserSettingValueType.String.ToString()
    };
}

internal sealed class FakeUserSettingsRepository : IUserSettingsRepository
{
    public IReadOnlyDictionary<string, UserSetting> Settings { get; init; } = new Dictionary<string, UserSetting>();
    public int? RequestedUserId { get; private set; }
    public int GetAllCalls { get; private set; }
    public (int UserId, string Key, string Value, UserSettingValueType ValueType)? LastSet { get; private set; }

    public Task<UserSetting?> GetAsync(int userId, string settingKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<UserSetting?>(Settings.GetValueOrDefault(settingKey));

    public Task<IReadOnlyDictionary<string, UserSetting>> GetAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        GetAllCalls++;
        RequestedUserId = userId;
        return Task.FromResult(Settings);
    }

    public Task SetAsync(int userId, string settingKey, string settingValue, UserSettingValueType valueType, CancellationToken cancellationToken = default)
    {
        LastSet = (userId, settingKey, settingValue, valueType);
        return Task.CompletedTask;
    }
}