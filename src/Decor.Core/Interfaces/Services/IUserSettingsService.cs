using Decor.Core.Configuration;
using Decor.Core.Common;

namespace Decor.Core.Interfaces.Services;
public interface IUserSettingsService
{
    Task<UserSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SetThemeAsync(DecorThemeStyle theme, CancellationToken cancellationToken = default);
    Task SetLanguageAsync(string language, CancellationToken cancellationToken = default);
}