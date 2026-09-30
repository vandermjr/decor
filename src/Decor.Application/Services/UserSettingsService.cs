using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using System.Globalization;

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
            Language = ParseLanguage(settings.GetValueOrDefault(DecorUserSettings.Language)?.SettingValue),
            IconAppearance = ParseIconAppearance(
                settings.GetValueOrDefault(DecorUserSettings.IconWeight)?.SettingValue,
                settings.GetValueOrDefault(DecorUserSettings.IconStrokeThickness)?.SettingValue)
        };
    }

    public Task SetThemeAsync(DecorThemeStyle theme, CancellationToken cancellationToken = default) =>
        SetAsync(DecorUserSettings.Theme, theme.ToString(), cancellationToken);

    public Task SetLanguageAsync(string language, CancellationToken cancellationToken = default) =>
        SetAsync(DecorUserSettings.Language, language, cancellationToken);

    public async Task SetIconAppearanceAsync(IconAppearance appearance, CancellationToken cancellationToken = default)
    {
        await SetAsync(DecorUserSettings.IconWeight, appearance.MaterialSymbolWeight.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await SetAsync(DecorUserSettings.IconStrokeThickness, appearance.StrokeThickness.ToString("0.0", CultureInfo.InvariantCulture), cancellationToken);
    }

    private Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var userId = authenticatedUserContext.User?.UserID
            ?? throw new InvalidOperationException("Não é possível persistir configurações sem um usuário autenticado.");

        return userSettingsRepository.SetAsync(userId, key, value, UserSettingValueType.String, cancellationToken);
    }

    private static UserSettings Defaults() => new()
    {
        Theme = DecorDefaults.Theme,
        Language = DecorDefaults.DefaultLanguage.Name,
        IconAppearance = IconAppearance.Default
    };

    private static DecorThemeStyle ParseTheme(string? value) =>
        Enum.TryParse<DecorThemeStyle>(value, true, out var theme) && Enum.IsDefined(theme)
            ? theme
            : DecorDefaults.Theme;

    private static string ParseLanguage(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DecorDefaults.DefaultLanguage.Name : value;

    private static IconAppearance ParseIconAppearance(string? weightValue, string? strokeValue)
    {
        var defaults = IconAppearance.Default;
        var hasValidWeight = int.TryParse(weightValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight)
            && weight is >= 100 and <= 700 && weight % 100 == 0;
        var hasValidStroke = double.TryParse(strokeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var stroke)
            && stroke is >= 0 and <= 3;
        return new IconAppearance(hasValidWeight ? weight : defaults.MaterialSymbolWeight, hasValidStroke ? Math.Round(stroke, 1, MidpointRounding.AwayFromZero) : defaults.StrokeThickness);
    }
}
