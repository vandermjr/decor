using Decor.Core.Common;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;
public class ThemeService : IThemeService
{
    private readonly IUserSettingsService _userSettingsService;
    public event Action<DecorThemeStyle>? ThemeChanged;
    
    public DecorThemeStyle CurrentTheme { get; private set; }

    public ThemeService(IUserSettingsService userSettingsService)
    {
        _userSettingsService = userSettingsService;
        CurrentTheme = DecorDefaults.Theme;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        CurrentTheme = (await _userSettingsService.GetAsync(cancellationToken)).Theme;
    }

    public async Task SetThemeAsync(DecorThemeStyle newTheme)
    {
        if (CurrentTheme == newTheme) return;

        await _userSettingsService.SetThemeAsync(newTheme);
        CurrentTheme = newTheme;

        ThemeChanged?.Invoke(newTheme);
    }
}
