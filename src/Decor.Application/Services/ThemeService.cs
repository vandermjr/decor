using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Helpers;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;
public class ThemeService : IThemeService
{
    private readonly IUserSettingsService _userSettingsService;
    private readonly ApplicationSettings _currentSettings;
    public event Action<DecorThemeStyle>? ThemeChanged;
    
    public DecorThemeStyle CurrentTheme { get; private set; }

    public ThemeService(IUserSettingsService userSettingsService)
    {
        _userSettingsService = userSettingsService;
        _currentSettings = _userSettingsService.LoadSettings();
        CurrentTheme = EnumHelper.StringToEnum<DecorThemeStyle>(_currentSettings.UserTheme)
                           ?? DecorDefaults.Theme;
    }

    public async Task SetThemeAsync(DecorThemeStyle newTheme)
    {
        if (CurrentTheme == newTheme) return;

        CurrentTheme = newTheme;
        _currentSettings.UserTheme = newTheme.ToString();

        await _userSettingsService.SaveSettingsAsync(_currentSettings);

        ThemeChanged?.Invoke(newTheme);
    }
}
