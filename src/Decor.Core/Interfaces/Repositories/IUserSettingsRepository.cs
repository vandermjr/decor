using Decor.Core.Configuration;
using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IUserSettingsRepository
{
    Task<UserSetting?> GetAsync(int userId, string settingKey, CancellationToken cancellationToken = default);
    Task SetAsync(int userId, string settingKey, string settingValue, UserSettingValueType valueType, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, UserSetting>> GetAllAsync(int userId, CancellationToken cancellationToken = default);
}
