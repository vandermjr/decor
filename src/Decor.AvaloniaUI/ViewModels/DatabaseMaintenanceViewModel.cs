using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class DatabaseMaintenanceViewModel : INotifyPropertyChanged
{
    private readonly IDatabaseBackupService _databaseBackupService;
    private DateTimeOffset? _backupDate = DateTimeOffset.Now.Date;
    private TimeSpan? _backupTime = CurrentTime();
    private BackupRecurrence _recurrence = BackupRecurrence.Once;
    private bool _backupOnMonday;
    private bool _backupOnTuesday;
    private bool _backupOnWednesday;
    private bool _backupOnThursday;
    private bool _backupOnFriday;
    private bool _backupOnSaturday;
    private bool _backupOnSunday;
    private bool _useSpecificFolder = true;
    private bool _useExternalDrive;
    private string _specificFolderPath = string.Empty;
    private string _externalDrivePath = string.Empty;
    private string _statusMessage = "Informe a data inicial, horário inicial e pelo menos um destino para o backup.";
    private string _summary = string.Empty;
    private bool _isBackupRunning;
    private BackupScheduleItemViewModel? _selectedSchedule;
    private int? _editingScheduleId;
    private int _nextScheduleId = 1;
    private readonly RelayCommand _saveScheduleCommand;
    private readonly RelayCommand _startImmediateBackupCommand;
    private readonly RelayCommand _newScheduleCommand;
    private readonly RelayCommand _deleteSelectedScheduleCommand;
    private readonly DispatcherTimer _clockTimer;

    public DatabaseMaintenanceViewModel(IDatabaseBackupService databaseBackupService)
    {
        _databaseBackupService = databaseBackupService;
        _saveScheduleCommand = new RelayCommand(SaveSchedule);
        _startImmediateBackupCommand = new RelayCommand(async () => await StartImmediateBackupAsync(), () => !IsBackupRunning);
        _newScheduleCommand = new RelayCommand(NewSchedule);
        _deleteSelectedScheduleCommand = new RelayCommand(DeleteSelectedSchedule, () => SelectedSchedule is not null);
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => OnBackupPreviewChanged();
        _clockTimer.Start();
    }

    public DateTimeOffset? BackupDate
    {
        get => _backupDate;
        set
        {
            if (Set(ref _backupDate, value))
            {
                OnPropertyChanged(nameof(BackupDateDisplay));
                OnPropertyChanged(nameof(NextExecutionPreviewDisplay));
                OnBackupPreviewChanged();
            }
        }
    }

    public TimeSpan? BackupTime
    {
        get => _backupTime;
        set
        {
            if (Set(ref _backupTime, value))
            {
                OnPropertyChanged(nameof(BackupTimeDisplay));
                OnPropertyChanged(nameof(NextExecutionPreviewDisplay));
                OnBackupPreviewChanged();
            }
        }
    }

    public bool IsOnceBackup
    {
        get => Recurrence == BackupRecurrence.Once;
        set
        {
            if (value)
                Recurrence = BackupRecurrence.Once;
        }
    }

    public bool IsDailyBackup
    {
        get => Recurrence == BackupRecurrence.Daily;
        set
        {
            if (value)
                Recurrence = BackupRecurrence.Daily;
        }
    }

    public bool IsWeekdaysBackup
    {
        get => Recurrence == BackupRecurrence.Weekdays;
        set
        {
            if (value)
                Recurrence = BackupRecurrence.Weekdays;
        }
    }

    public bool IsCustomBackup
    {
        get => Recurrence == BackupRecurrence.Custom;
        set
        {
            if (value)
            {
                Recurrence = BackupRecurrence.Custom;
                EnsureCurrentWeekDaySelected();
            }
        }
    }

    public BackupRecurrence Recurrence
    {
        get => _recurrence;
        private set
        {
            if (!Set(ref _recurrence, value)) return;
            OnPropertyChanged(nameof(IsOnceBackup));
            OnPropertyChanged(nameof(IsDailyBackup));
            OnPropertyChanged(nameof(IsWeekdaysBackup));
            OnPropertyChanged(nameof(IsCustomBackup));
            OnPropertyChanged(nameof(PeriodDisplay));
            OnPropertyChanged(nameof(NextExecutionPreviewDisplay));
            OnBackupPreviewChanged();
        }
    }

    public bool BackupOnMonday
    {
        get => _backupOnMonday;
        set => SetWeekDay(ref _backupOnMonday, value);
    }

    public bool BackupOnTuesday
    {
        get => _backupOnTuesday;
        set => SetWeekDay(ref _backupOnTuesday, value);
    }

    public bool BackupOnWednesday
    {
        get => _backupOnWednesday;
        set => SetWeekDay(ref _backupOnWednesday, value);
    }

    public bool BackupOnThursday
    {
        get => _backupOnThursday;
        set => SetWeekDay(ref _backupOnThursday, value);
    }

    public bool BackupOnFriday
    {
        get => _backupOnFriday;
        set => SetWeekDay(ref _backupOnFriday, value);
    }

    public bool BackupOnSaturday
    {
        get => _backupOnSaturday;
        set => SetWeekDay(ref _backupOnSaturday, value);
    }

    public bool BackupOnSunday
    {
        get => _backupOnSunday;
        set => SetWeekDay(ref _backupOnSunday, value);
    }

    public bool UseSpecificFolder
    {
        get => _useSpecificFolder;
        set
        {
            if (Set(ref _useSpecificFolder, value))
                OnBackupPreviewChanged();
        }
    }

    public bool UseExternalDrive
    {
        get => _useExternalDrive;
        set
        {
            if (Set(ref _useExternalDrive, value))
                OnBackupPreviewChanged();
        }
    }

    public string SpecificFolderPath
    {
        get => _specificFolderPath;
        set
        {
            if (Set(ref _specificFolderPath, value))
                OnBackupPreviewChanged();
        }
    }

    public string ExternalDrivePath
    {
        get => _externalDrivePath;
        set
        {
            if (Set(ref _externalDrivePath, value))
                OnBackupPreviewChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public string Summary
    {
        get => _summary;
        private set
        {
            if (Set(ref _summary, value))
                OnPropertyChanged(nameof(HasSummary));
        }
    }

    public bool IsBackupRunning
    {
        get => _isBackupRunning;
        private set
        {
            if (!Set(ref _isBackupRunning, value)) return;
            _startImmediateBackupCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(ImmediateBackupButtonText));
        }
    }

    public BackupScheduleItemViewModel? SelectedSchedule
    {
        get => _selectedSchedule;
        set
        {
            if (!Set(ref _selectedSchedule, value)) return;
            _deleteSelectedScheduleCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSelectedSchedule));
            LoadSelectedScheduleForEditing();
        }
    }

    public ObservableCollection<BackupScheduleItemViewModel> Schedules { get; } = [];
    public bool HasSchedules => Schedules.Count > 0;
    public bool HasSelectedSchedule => SelectedSchedule is not null;
    public string EditorTitle => _editingScheduleId is null ? "Novo agendamento" : "Editar agendamento";
    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
    public string BackupDateDisplay => BackupDate?.ToString("dd/MM/yyyy") ?? "Selecionar data";
    public string BackupTimeDisplay => BackupTime?.ToString(@"hh\:mm") ?? "Selecionar horário";
    public string NextExecutionPreviewDisplay => TryBuildScheduledAt(out var scheduledAt) && TryCalculateNextExecution(scheduledAt, out var nextExecutionAt, setStatusMessage: false)
        ? $"Próxima execução: {nextExecutionAt:dd/MM/yyyy HH:mm} ({FormatTimeUntil(nextExecutionAt)})"
        : Recurrence == BackupRecurrence.Custom
            ? "Selecione pelo menos um dia da semana para calcular a próxima execução."
            : "Próxima execução será calculada ao salvar.";
    public string PeriodDisplay => Recurrence switch
    {
        BackupRecurrence.Once => "Uma vez; será desativado após executar",
        BackupRecurrence.Daily => "Diariamente enquanto estiver ativo",
        BackupRecurrence.Weekdays => "Seg a Sex enquanto estiver ativo",
        BackupRecurrence.Custom => GetSelectedWeekDays().Any()
            ? $"{string.Join(", ", GetSelectedWeekDays())} enquanto estiver ativo"
            : "Personalizado; selecione os dias da semana",
        _ => string.Empty
    };
    public string BackupFileName => TryBuildScheduledAt(out var scheduledAt) && TryCalculateNextExecution(scheduledAt, out var nextExecutionAt, setStatusMessage: false)
        ? $"decor_backup_{nextExecutionAt:yyyyMMdd_HHmm}.zip"
        : "decor_backup.zip";
    public string SpecificFolderBackupPath => CreateBackupPath(UseSpecificFolder, SpecificFolderPath);
    public string ExternalDriveBackupPath => CreateBackupPath(UseExternalDrive, ExternalDrivePath);
    public bool HasSpecificFolderBackupPath => !string.IsNullOrWhiteSpace(SpecificFolderBackupPath);
    public bool HasExternalDriveBackupPath => !string.IsNullOrWhiteSpace(ExternalDriveBackupPath);
    public string ImmediateBackupButtonText => IsBackupRunning ? "Gerando backup..." : "Iniciar backup agora";
    public ICommand NewScheduleCommand => _newScheduleCommand;
    public ICommand DeleteSelectedScheduleCommand => _deleteSelectedScheduleCommand;
    public ICommand SaveScheduleCommand => _saveScheduleCommand;
    public ICommand StartImmediateBackupCommand => _startImmediateBackupCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SaveSchedule()
    {
        Summary = string.Empty;

        if (!TryGetScheduledAt(out var scheduledAt))
            return;

        if (Recurrence == BackupRecurrence.Custom && !GetSelectedWeekDays().Any())
        {
            StatusMessage = "Selecione pelo menos um dia da semana para a repetição personalizada.";
            return;
        }

        if (!TryCalculateNextExecution(scheduledAt, out var nextExecutionAt, setStatusMessage: true))
            return;

        var destinations = GetSelectedDestinations().ToArray();
        if (destinations.Length == 0)
        {
            StatusMessage = "Selecione e preencha pelo menos um local de backup.";
            return;
        }

        var schedule = new BackupScheduleItemViewModel(
            _editingScheduleId ?? _nextScheduleId++,
            scheduledAt,
            nextExecutionAt,
            Recurrence,
            Recurrence == BackupRecurrence.Custom ? GetSelectedWeekDays().ToArray() : [],
            UseSpecificFolder,
            SpecificFolderPath.Trim(),
            UseExternalDrive,
            ExternalDrivePath.Trim(),
            destinations,
            BackupFileName);

        if (_editingScheduleId is null)
        {
            Schedules.Add(schedule);
        }
        else
        {
            var current = Schedules.FirstOrDefault(item => item.Id == _editingScheduleId.Value);
            if (current is not null)
            {
                var index = Schedules.IndexOf(current);
                schedule.IsEnabled = current.IsEnabled;
                Schedules[index] = schedule;
            }
        }

        SelectedSchedule = schedule;
        _editingScheduleId = null;
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(HasSchedules));

        StatusMessage = "Agendamento salvo. A execução será ligada ao serviço de backup.";
        Summary = BuildSummary(scheduledAt, nextExecutionAt, destinations);
    }

    private async Task StartImmediateBackupAsync()
    {
        if (IsBackupRunning)
            return;

        Summary = string.Empty;
        var backupFiles = GetImmediateBackupFiles().ToArray();
        if (backupFiles.Length == 0)
        {
            StatusMessage = "Selecione e preencha pelo menos um destino para iniciar o backup agora.";
            return;
        }

        try
        {
            IsBackupRunning = true;
            StatusMessage = "Gerando backup imediato...";
            var result = await _databaseBackupService.CreateBackupAsync(backupFiles);
            StatusMessage = "Backup imediato concluído.";
            Summary = $"Criado em: {result.CreatedAt:dd/MM/yyyy HH:mm:ss}\nArquivo compactado: {Path.GetFileName(result.Files.First())}\nDestino: {string.Join(", ", result.Files)}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Não foi possível gerar o backup: {exception.Message}";
        }
        finally
        {
            IsBackupRunning = false;
        }
    }

    private void NewSchedule()
    {
        _editingScheduleId = null;
        SelectedSchedule = null;
        BackupDate = DateTimeOffset.Now.Date;
        BackupTime = CurrentTime();
        Recurrence = BackupRecurrence.Once;
        ClearCustomWeekDays();
        Summary = string.Empty;
        StatusMessage = "Configure um novo agendamento de backup.";
        OnPropertyChanged(nameof(EditorTitle));
    }

    public void LoadSelectedScheduleForEditing()
    {
        if (SelectedSchedule is null)
            return;

        _editingScheduleId = SelectedSchedule.Id;
        BackupDate = new DateTimeOffset(SelectedSchedule.StartAt.Date);
        BackupTime = SelectedSchedule.StartAt.TimeOfDay;
        Recurrence = SelectedSchedule.Recurrence;
        BackupOnMonday = SelectedSchedule.WeekDays.Contains("seg");
        BackupOnTuesday = SelectedSchedule.WeekDays.Contains("ter");
        BackupOnWednesday = SelectedSchedule.WeekDays.Contains("qua");
        BackupOnThursday = SelectedSchedule.WeekDays.Contains("qui");
        BackupOnFriday = SelectedSchedule.WeekDays.Contains("sex");
        BackupOnSaturday = SelectedSchedule.WeekDays.Contains("sáb");
        BackupOnSunday = SelectedSchedule.WeekDays.Contains("dom");
        UseSpecificFolder = SelectedSchedule.UseSpecificFolder;
        SpecificFolderPath = SelectedSchedule.SpecificFolderPath;
        UseExternalDrive = SelectedSchedule.UseExternalDrive;
        ExternalDrivePath = SelectedSchedule.ExternalDrivePath;
        Summary = string.Empty;
        StatusMessage = "Editando agendamento selecionado.";
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void DeleteSelectedSchedule()
    {
        if (SelectedSchedule is null)
            return;

        var removedSchedule = SelectedSchedule;
        Schedules.Remove(removedSchedule);
        SelectedSchedule = null;
        if (_editingScheduleId == removedSchedule.Id)
        {
            _editingScheduleId = null;
            OnPropertyChanged(nameof(EditorTitle));
        }

        OnPropertyChanged(nameof(HasSchedules));
        StatusMessage = "Agendamento removido.";
    }

    private bool TryGetScheduledAt(out DateTime scheduledAt)
    {
        scheduledAt = default;

        if (BackupDate is null)
        {
            StatusMessage = "Escolha a data inicial do backup.";
            return false;
        }

        if (BackupTime is null)
        {
            StatusMessage = "Escolha o horário inicial do backup.";
            return false;
        }

        scheduledAt = BackupDate.Value.Date.Add(BackupTime.Value);
        return true;
    }

    private bool TryCalculateNextExecution(DateTime startAt, out DateTime nextExecutionAt, bool setStatusMessage)
    {
        var now = DateTime.Now;
        nextExecutionAt = startAt;

        if (Recurrence == BackupRecurrence.Once)
        {
            if (nextExecutionAt <= now)
                nextExecutionAt = now.Date.Add(startAt.TimeOfDay);
            if (nextExecutionAt <= now)
                nextExecutionAt = nextExecutionAt.AddDays(1);

            return true;
        }

        var allowedDays = GetAllowedDays().ToArray();
        if (allowedDays.Length == 0)
        {
            if (setStatusMessage)
                StatusMessage = "Selecione pelo menos um dia da semana para a repetição personalizada.";
            return false;
        }

        if (nextExecutionAt <= now)
        {
            nextExecutionAt = now.Date.Add(startAt.TimeOfDay);
            if (nextExecutionAt <= now)
                nextExecutionAt = nextExecutionAt.AddDays(1);
        }

        while (!allowedDays.Contains(nextExecutionAt.DayOfWeek))
            nextExecutionAt = nextExecutionAt.AddDays(1);

        return true;
    }

    private IEnumerable<string> GetSelectedDestinations()
    {
        if (HasSpecificFolderBackupPath)
            yield return SpecificFolderBackupPath;
        if (HasExternalDriveBackupPath)
            yield return ExternalDriveBackupPath;
    }

    private IEnumerable<string> GetImmediateBackupFiles()
    {
        var fileName = $"decor_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        if (UseSpecificFolder && !string.IsNullOrWhiteSpace(SpecificFolderPath))
            yield return Path.Combine(SpecificFolderPath.Trim(), fileName);
        if (UseExternalDrive && !string.IsNullOrWhiteSpace(ExternalDrivePath))
            yield return Path.Combine(ExternalDrivePath.Trim(), fileName);
    }

    private IEnumerable<string> GetSelectedWeekDays()
    {
        if (BackupOnMonday) yield return "seg";
        if (BackupOnTuesday) yield return "ter";
        if (BackupOnWednesday) yield return "qua";
        if (BackupOnThursday) yield return "qui";
        if (BackupOnFriday) yield return "sex";
        if (BackupOnSaturday) yield return "sáb";
        if (BackupOnSunday) yield return "dom";
    }

    private IEnumerable<DayOfWeek> GetAllowedDays()
    {
        if (Recurrence == BackupRecurrence.Daily)
        {
            yield return DayOfWeek.Sunday;
            yield return DayOfWeek.Monday;
            yield return DayOfWeek.Tuesday;
            yield return DayOfWeek.Wednesday;
            yield return DayOfWeek.Thursday;
            yield return DayOfWeek.Friday;
            yield return DayOfWeek.Saturday;
            yield break;
        }

        if (Recurrence == BackupRecurrence.Weekdays)
        {
            yield return DayOfWeek.Monday;
            yield return DayOfWeek.Tuesday;
            yield return DayOfWeek.Wednesday;
            yield return DayOfWeek.Thursday;
            yield return DayOfWeek.Friday;
            yield break;
        }

        if (BackupOnMonday) yield return DayOfWeek.Monday;
        if (BackupOnTuesday) yield return DayOfWeek.Tuesday;
        if (BackupOnWednesday) yield return DayOfWeek.Wednesday;
        if (BackupOnThursday) yield return DayOfWeek.Thursday;
        if (BackupOnFriday) yield return DayOfWeek.Friday;
        if (BackupOnSaturday) yield return DayOfWeek.Saturday;
        if (BackupOnSunday) yield return DayOfWeek.Sunday;
    }

    private void EnsureCurrentWeekDaySelected()
    {
        if (GetSelectedWeekDays().Any())
            return;

        switch (DateTime.Now.DayOfWeek)
        {
            case DayOfWeek.Monday:
                BackupOnMonday = true;
                break;
            case DayOfWeek.Tuesday:
                BackupOnTuesday = true;
                break;
            case DayOfWeek.Wednesday:
                BackupOnWednesday = true;
                break;
            case DayOfWeek.Thursday:
                BackupOnThursday = true;
                break;
            case DayOfWeek.Friday:
                BackupOnFriday = true;
                break;
            case DayOfWeek.Saturday:
                BackupOnSaturday = true;
                break;
            case DayOfWeek.Sunday:
                BackupOnSunday = true;
                break;
        }
    }

    private static string FormatTimeUntil(DateTime target)
    {
        var remaining = target - DateTime.Now;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var totalHours = (int)Math.Floor(remaining.TotalHours);
        return totalHours > 0
            ? $"em {totalHours}h {remaining.Minutes}min {remaining.Seconds}s"
            : $"em {remaining.Minutes}min {remaining.Seconds}s";
    }

    private string BuildSummary(DateTime scheduledAt, DateTime nextExecutionAt, IReadOnlyCollection<string> destinations) =>
        $"Início: {scheduledAt:dd/MM/yyyy HH:mm}\nPróxima execução: {nextExecutionAt:dd/MM/yyyy HH:mm} ({FormatTimeUntil(nextExecutionAt)})\nRepetição: {PeriodDisplay}\nArquivo compactado: {BackupFileName}\nDestino: {string.Join(", ", destinations)}";

    private void ClearCustomWeekDays()
    {
        BackupOnMonday = false;
        BackupOnTuesday = false;
        BackupOnWednesday = false;
        BackupOnThursday = false;
        BackupOnFriday = false;
        BackupOnSaturday = false;
        BackupOnSunday = false;
    }

    private bool TryBuildScheduledAt(out DateTime scheduledAt)
    {
        scheduledAt = default;
        if (BackupDate is null || BackupTime is null)
            return false;

        scheduledAt = BackupDate.Value.Date.Add(BackupTime.Value);
        return true;
    }

    private string CreateBackupPath(bool isSelected, string folderPath)
    {
        if (!isSelected || string.IsNullOrWhiteSpace(folderPath))
            return string.Empty;

        var trimmedPath = folderPath.Trim();
        return Path.Combine(trimmedPath, BackupFileName);
    }

    private static TimeSpan CurrentTime()
    {
        var now = DateTime.Now;
        return new TimeSpan(now.Hour, now.Minute, 0);
    }

    private void OnBackupPreviewChanged()
    {
        OnPropertyChanged(nameof(BackupFileName));
        OnPropertyChanged(nameof(NextExecutionPreviewDisplay));
        OnPropertyChanged(nameof(SpecificFolderBackupPath));
        OnPropertyChanged(nameof(ExternalDriveBackupPath));
        OnPropertyChanged(nameof(HasSpecificFolderBackupPath));
        OnPropertyChanged(nameof(HasExternalDriveBackupPath));
        RefreshSummaryIfVisible();
    }

    private void RefreshSummaryIfVisible()
    {
        if (!HasSummary)
            return;

        if (!TryBuildScheduledAt(out var scheduledAt) ||
            !TryCalculateNextExecution(scheduledAt, out var nextExecutionAt, setStatusMessage: false))
            return;

        var destinations = GetSelectedDestinations().ToArray();
        if (destinations.Length == 0)
            return;

        Summary = BuildSummary(scheduledAt, nextExecutionAt, destinations);
    }

    private void SetWeekDay(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (!Set(ref field, value, propertyName)) return;
        OnPropertyChanged(nameof(PeriodDisplay));
        OnPropertyChanged(nameof(NextExecutionPreviewDisplay));
        OnBackupPreviewChanged();
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
