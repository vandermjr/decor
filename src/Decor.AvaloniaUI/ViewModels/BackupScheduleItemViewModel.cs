using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Decor.AvaloniaUI.ViewModels;

public enum BackupRecurrence
{
    Once,
    Daily,
    Weekdays,
    Custom
}

public sealed class BackupScheduleItemViewModel : INotifyPropertyChanged
{
    private bool _isEnabled = true;

    public BackupScheduleItemViewModel(
        int id,
        DateTime startAt,
        DateTime nextExecutionAt,
        BackupRecurrence recurrence,
        IReadOnlyList<string> weekDays,
        bool useSpecificFolder,
        string specificFolderPath,
        bool useExternalDrive,
        string externalDrivePath,
        IReadOnlyList<string> destinations,
        string fileName)
    {
        Id = id;
        StartAt = startAt;
        NextExecutionAt = nextExecutionAt;
        Recurrence = recurrence;
        WeekDays = weekDays;
        UseSpecificFolder = useSpecificFolder;
        SpecificFolderPath = specificFolderPath;
        UseExternalDrive = useExternalDrive;
        ExternalDrivePath = externalDrivePath;
        Destinations = destinations;
        FileName = fileName;
    }

    public int Id { get; }
    public DateTime StartAt { get; }
    public DateTime NextExecutionAt { get; }
    public BackupRecurrence Recurrence { get; }
    public IReadOnlyList<string> WeekDays { get; }
    public bool UseSpecificFolder { get; }
    public string SpecificFolderPath { get; }
    public bool UseExternalDrive { get; }
    public string ExternalDrivePath { get; }
    public IReadOnlyList<string> Destinations { get; }
    public string FileName { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    public string TimeDisplay => StartAt.ToString("HH:mm");
    public string StartDateDisplay => StartAt.ToString("dd/MM/yyyy");
    public string NextExecutionDisplay => $"Próxima execução: {NextExecutionAt:dd/MM/yyyy HH:mm}";
    public string PeriodDisplay => Recurrence switch
    {
        BackupRecurrence.Once => $"Uma vez ({NextExecutionAt:dd/MM/yyyy})",
        BackupRecurrence.Daily => "Diariamente",
        BackupRecurrence.Weekdays => "Seg a Sex",
        BackupRecurrence.Custom => string.Join(", ", WeekDays),
        _ => StartDateDisplay
    };
    public string DestinationsDisplay => string.Join(" | ", Destinations);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
