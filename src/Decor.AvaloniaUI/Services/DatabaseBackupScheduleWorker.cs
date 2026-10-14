using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Decor.AvaloniaUI.Services;

public sealed class DatabaseBackupScheduleWorker(
    IDatabaseBackupScheduleStore scheduleStore,
    IDatabaseBackupService backupService,
    ILogger<DatabaseBackupScheduleWorker> logger,
    IBackupNotificationSettingsStore? notificationSettingsStore = null,
    INotificationCenter? notificationCenter = null) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessDueSchedulesAsync(stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    public async Task ProcessDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var settings = notificationSettingsStore?.Load() ?? new BackupNotificationSettings();
        foreach (var savedSchedule in scheduleStore.Load().Where(item => item.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var schedule = savedSchedule;
            var notificationAt = schedule.NextExecutionAt.AddMinutes(-settings.LeadTimeMinutes);
            if (settings.IsEnabled && schedule.NextExecutionAt > now && notificationAt <= now &&
                schedule.NotifiedForExecutionAt != schedule.NextExecutionAt)
            {
                notificationCenter?.Publish("Backup agendado", $"Backup previsto para {schedule.NextExecutionAt:dd/MM/yyyy HH:mm}. O aviso programado para {notificationAt:dd/MM/yyyy HH:mm} está sendo exibido agora.");
                schedule = schedule with { NotifiedForExecutionAt = schedule.NextExecutionAt };
                scheduleStore.Update(schedule);
            }

            if (schedule.NextExecutionAt > now)
                continue;

            try
            {
                var fileName = $"decor_backup_{schedule.Id}_{now:yyyyMMdd_HHmmss}.zip";
                var destinations = schedule.Destinations
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => Path.Combine(path, fileName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (destinations.Length == 0)
                    throw new InvalidOperationException("O agendamento não possui destinos válidos.");

                notificationCenter?.Publish("Backup em andamento", $"Iniciando backup agendado para {schedule.NextExecutionAt:dd/MM/yyyy HH:mm}.");
                var result = await backupService.CreateBackupAsync(destinations, cancellationToken);
                logger.LogInformation("Backup agendado {ScheduleId} concluído em {Files}.", schedule.Id, string.Join(", ", result.Files));
                notificationCenter?.Publish("Backup realizado", $"Backup concluído em {result.CreatedAt:dd/MM/yyyy HH:mm}. Destino: {string.Join(", ", result.Files)}");
                scheduleStore.Update(Advance(schedule, DateTime.Now));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao executar o backup agendado {ScheduleId}; nova tentativa em cinco minutos.", schedule.Id);
                notificationCenter?.Publish("Falha no backup", $"Não foi possível concluir o backup agendado: {exception.Message}");
                scheduleStore.Update(schedule with { NextExecutionAt = DateTime.Now.AddMinutes(5), NotifiedForExecutionAt = null });
            }
        }
    }

    private static DatabaseBackupSchedule Advance(DatabaseBackupSchedule schedule, DateTime completedAt)
    {
        if (schedule.Recurrence == BackupRecurrence.Once)
            return schedule with { IsEnabled = false };

        var next = completedAt.Date.Add(schedule.StartAt.TimeOfDay);
        if (next <= completedAt)
            next = next.AddDays(1);

        while (!IsAllowedDay(schedule, next.DayOfWeek))
            next = next.AddDays(1);

        return schedule with { NextExecutionAt = next, NotifiedForExecutionAt = null };
    }

    private static bool IsAllowedDay(DatabaseBackupSchedule schedule, DayOfWeek day) => schedule.Recurrence switch
    {
        BackupRecurrence.Daily => true,
        BackupRecurrence.Weekdays => day is >= DayOfWeek.Monday and <= DayOfWeek.Friday,
        BackupRecurrence.Custom => schedule.WeekDays.Contains(day switch
        {
            DayOfWeek.Monday => "seg",
            DayOfWeek.Tuesday => "ter",
            DayOfWeek.Wednesday => "qua",
            DayOfWeek.Thursday => "qui",
            DayOfWeek.Friday => "sex",
            DayOfWeek.Saturday => "sáb",
            DayOfWeek.Sunday => "dom",
            _ => string.Empty
        }),
        _ => false
    };
}