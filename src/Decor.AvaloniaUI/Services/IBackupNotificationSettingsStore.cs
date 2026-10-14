namespace Decor.AvaloniaUI.Services;

public interface IBackupNotificationSettingsStore
{
    BackupNotificationSettings Load();
    void Save(BackupNotificationSettings settings);
}