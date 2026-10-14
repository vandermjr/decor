using System.ComponentModel;
using System.Runtime.CompilerServices;
using Decor.AvaloniaUI.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class BackupScheduleItemViewModel : INotifyPropertyChanged
{
    private bool _isEnabled = true;
    private DateTime _nextExecutionAt;
    private DateTime? _notifiedForExecutionAt;
    private string _fileName;

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
        string fileName,
        DateTime? notifiedForExecutionAt = null)
    {
        Id = id;
        StartAt = startAt;
        _nextExecutionAt = nextExecutionAt;
        Recurrence = recurrence;
        WeekDays = weekDays;
        UseSpecificFolder = useSpecificFolder;
        SpecificFolderPath = specificFolderPath;
        UseExternalDrive = useExternalDrive;
        ExternalDrivePath = externalDrivePath;
        Destinations = destinations;
        _fileName = fileName;
        _notifiedForExecutionAt = notifiedForExecutionAt;
    }

    public int Id { get; }
    public DateTime StartAt { get; }
    public DateTime NextExecutionAt => _nextExecutionAt;
    public BackupRecurrence Recurrence { get; }
    public IReadOnlyList<string> WeekDays { get; }
    public bool UseSpecificFolder { get; }
    public string SpecificFolderPath { get; }
    public bool UseExternalDrive { get; }
    public string ExternalDrivePath { get; }
    public IReadOnlyList<string> Destinations { get; }
    public string FileName => _fileName;
    public DateTime? NotifiedForExecutionAt => _notifiedForExecutionAt;

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

    public void SynchronizeExecutionState(DatabaseBackupSchedule schedule)
    {
        IsEnabled = schedule.IsEnabled;
        if (_nextExecutionAt != schedule.NextExecutionAt)
        {
            _nextExecutionAt = schedule.NextExecutionAt;
            OnPropertyChanged(nameof(NextExecutionAt));
            OnPropertyChanged(nameof(NextExecutionDisplay));
            OnPropertyChanged(nameof(PeriodDisplay));
        }
        if (_notifiedForExecutionAt != schedule.NotifiedForExecutionAt)
        {
            _notifiedForExecutionAt = schedule.NotifiedForExecutionAt;
            OnPropertyChanged(nameof(NotifiedForExecutionAt));
        }
        var fileName = $"decor_backup_{Id}_{schedule.NextExecutionAt:yyyyMMdd_HHmm}.zip";
        if (_fileName != fileName)
        {
            _fileName = fileName;
            OnPropertyChanged(nameof(FileName));
        }
    }

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
