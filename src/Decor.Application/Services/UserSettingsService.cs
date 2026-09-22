using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class UserSettingsService(
    IUserSettingsRepository userSettingsRepository,
    IAuthenticatedUserContext authenticatedUserContext) : IUserSettingsService
{
    public async Task<UserSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = authenticatedUserContext.User?.UserID;
        if (userId is null)
            return Defaults();

        var settings = await userSettingsRepository.GetAllAsync(userId.Value, cancellationToken);
        return new UserSettings
        {
            Theme = ParseTheme(settings.GetValueOrDefault(DecorUserSettings.Theme)?.SettingValue),
            Language = ParseLanguage(settings.GetValueOrDefault(DecorUserSettings.Language)?.SettingValue)
        };
    }

    public Task SetThemeAsync(DecorThemeStyle theme, CancellationToken cancellationToken = default) =>
        SetAsync(DecorUserSettings.Theme, theme.ToString(), cancellationToken);

    public Task SetLanguageAsync(string language, CancellationToken cancellationToken = default) =>
        SetAsync(DecorUserSettings.Language, language, cancellationToken);

    private Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var userId = authenticatedUserContext.User?.UserID
            ?? throw new InvalidOperationException("Não é possível persistir configurações sem um usuário autenticado.");

        return userSettingsRepository.SetAsync(userId, key, value, UserSettingValueType.String, cancellationToken);
    }

    private static UserSettings Defaults() => new()
    {
        Theme = DecorDefaults.Theme,
        Language = DecorDefaults.DefaultLanguage.Name
    };

    private static DecorThemeStyle ParseTheme(string? value) =>
        Enum.TryParse<DecorThemeStyle>(value, true, out var theme) && Enum.IsDefined(theme)
            ? theme
            : DecorDefaults.Theme;

    private static string ParseLanguage(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DecorDefaults.DefaultLanguage.Name : value;
}