using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Decor.AvaloniaUI.Services;

public sealed class DatabaseBackupScheduleStore : IDatabaseBackupScheduleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly ILogger<DatabaseBackupScheduleStore> _logger;

    public event EventHandler? SchedulesChanged;

    public DatabaseBackupScheduleStore(ILogger<DatabaseBackupScheduleStore> logger)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Decor", "backup-schedules.json"), logger)
    {
    }

    public DatabaseBackupScheduleStore(string filePath, ILogger<DatabaseBackupScheduleStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public IReadOnlyList<DatabaseBackupSchedule> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<DatabaseBackupSchedule>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(exception, "Não foi possível carregar os agendamentos de backup em {ScheduleFile}.", _filePath);
                return [];
            }
        }
    }

    public void Upsert(DatabaseBackupSchedule schedule)
    {
        lock (_sync)
        {
            var schedules = ReadUnderLock();
            var index = schedules.FindIndex(item => item.Id == schedule.Id);
            if (index < 0)
                schedules.Add(schedule);
            else
                schedules[index] = schedule;
            WriteUnderLock(schedules);
        }
        SchedulesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Delete(int id)
    {
        lock (_sync)
        {
            var schedules = ReadUnderLock();
            schedules.RemoveAll(item => item.Id == id);
            WriteUnderLock(schedules);
        }
        SchedulesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(DatabaseBackupSchedule schedule)
    {
        lock (_sync)
        {
            var schedules = ReadUnderLock();
            var index = schedules.FindIndex(item => item.Id == schedule.Id);
            if (index < 0)
                return;

            schedules[index] = schedule;
            WriteUnderLock(schedules);
        }
        SchedulesChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<DatabaseBackupSchedule> ReadUnderLock()
    {
        if (!File.Exists(_filePath))
            return [];

        return JsonSerializer.Deserialize<List<DatabaseBackupSchedule>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
    }

    private void WriteUnderLock(IReadOnlyCollection<DatabaseBackupSchedule> schedules)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("O caminho do arquivo de agendamentos não possui diretório.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(schedules, JsonOptions));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}