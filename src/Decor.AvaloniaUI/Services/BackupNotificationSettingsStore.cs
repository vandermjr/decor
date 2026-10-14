using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Decor.AvaloniaUI.Services;

public sealed class BackupNotificationSettingsStore : IBackupNotificationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly ILogger<BackupNotificationSettingsStore> _logger;

    public BackupNotificationSettingsStore(ILogger<BackupNotificationSettingsStore> logger)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Decor", "backup-notification-settings.json"), logger)
    {
    }

    public BackupNotificationSettingsStore(string filePath, ILogger<BackupNotificationSettingsStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public BackupNotificationSettings Load()
    {
        if (!File.Exists(_filePath))
            return new BackupNotificationSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<BackupNotificationSettings>(File.ReadAllText(_filePath), JsonOptions);
            return settings is { LeadTimeMinutes: >= 1 and <= 1440 } ? settings : new BackupNotificationSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogError(exception, "Não foi possível carregar as opções de notificação de backup em {SettingsFile}.", _filePath);
            return new BackupNotificationSettings();
        }
    }

    public void Save(BackupNotificationSettings settings)
    {
        if (settings.LeadTimeMinutes is < 1 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(settings), "A antecedência deve estar entre 1 e 1440 minutos.");

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("O caminho das opções de notificação não possui diretório.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}