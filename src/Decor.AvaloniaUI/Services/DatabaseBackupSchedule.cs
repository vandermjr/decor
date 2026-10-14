namespace Decor.AvaloniaUI.Services;

public enum BackupRecurrence
{
    Once,
    Daily,
    Weekdays,
    Custom
}

public sealed record DatabaseBackupSchedule(
    int Id,
    DateTime StartAt,
    DateTime NextExecutionAt,
    BackupRecurrence Recurrence,
    IReadOnlyList<string> WeekDays,
    bool UseSpecificFolder,
    string SpecificFolderPath,
    bool UseExternalDrive,
    string ExternalDrivePath,
    IReadOnlyList<string> Destinations,
    bool IsEnabled,
    DateTime? NotifiedForExecutionAt = null);