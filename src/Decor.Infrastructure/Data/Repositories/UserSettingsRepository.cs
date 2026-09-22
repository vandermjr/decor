using Dapper;
using Decor.Core.Configuration;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class UserSettingsRepository(IDatabaseConnection databaseConnection) : IUserSettingsRepository
{
    private readonly IDatabaseConnection _databaseConnection = databaseConnection;

    public async Task<UserSetting?> GetAsync(int userId, string settingKey, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT UserID, SettingKey, SettingValue, ValueType
            FROM user_settings
            WHERE UserID = @UserId AND SettingKey = @SettingKey
            LIMIT 1;";

        using var connection = _databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserSetting>(new CommandDefinition(sql, new { UserId = userId, SettingKey = settingKey }, cancellationToken: cancellationToken));
    }

    public async Task SetAsync(int userId, string settingKey, string settingValue, UserSettingValueType valueType, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO user_settings (UserID, SettingKey, SettingValue, ValueType)
            VALUES (@UserId, @SettingKey, @SettingValue, @ValueType)
            ON DUPLICATE KEY UPDATE
                SettingValue = VALUES(SettingValue),
                ValueType = VALUES(ValueType);";

        using var connection = _databaseConnection.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            UserId = userId,
            SettingKey = settingKey,
            SettingValue = settingValue,
            ValueType = valueType.ToString()
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<string, UserSetting>> GetAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT UserID, SettingKey, SettingValue, ValueType
            FROM user_settings
            WHERE UserID = @UserId;";

        using var connection = _databaseConnection.CreateConnection();
        var settings = await connection.QueryAsync<UserSetting>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
        return settings.ToDictionary(s => s.SettingKey, s => s, StringComparer.Ordinal);
    }
}
