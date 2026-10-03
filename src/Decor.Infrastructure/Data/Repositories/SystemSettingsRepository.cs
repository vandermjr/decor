using Dapper;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using System.Reflection;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class SystemSettingsRepository(IDatabaseConnection databaseConnection) : ISystemSettingsRepository
{
    private const string MigrationResourceName = "Decor.Infrastructure.Migrations.20261002_add_system_settings.sql";
    private readonly IDatabaseConnection _databaseConnection = databaseConnection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            await using var migrationStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(MigrationResourceName)
                ?? throw new InvalidOperationException($"Embedded database migration '{MigrationResourceName}' was not found.");
            using var reader = new StreamReader(migrationStream);
            var migrationSql = await reader.ReadToEndAsync(cancellationToken);
            using var connection = _databaseConnection.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition(migrationSql, cancellationToken: cancellationToken));
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        const string sql = "SELECT SettingKey, SettingValue FROM system_settings;";
        using var connection = _databaseConnection.CreateConnection();
        var settings = await connection.QueryAsync<SystemSettingRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return settings.ToDictionary(setting => setting.SettingKey, setting => setting.SettingValue, StringComparer.Ordinal);
    }

    public async Task SetAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        const string sql = @"
            INSERT INTO system_settings (SettingKey, SettingValue)
            VALUES (@SettingKey, @SettingValue)
            ON DUPLICATE KEY UPDATE SettingValue = VALUES(SettingValue);";

        using var connection = _databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var (key, value) in settings)
            {
                await connection.ExecuteAsync(new CommandDefinition(sql,
                    new { SettingKey = key, SettingValue = value }, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private sealed class SystemSettingRow
    {
        public string SettingKey { get; init; } = string.Empty;
        public string SettingValue { get; init; } = string.Empty;
    }
}