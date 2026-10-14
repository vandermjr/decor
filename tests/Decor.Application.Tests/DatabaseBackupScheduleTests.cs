using System.Collections.ObjectModel;
using Decor.AvaloniaUI.Services;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Decor.Application.Tests;

public sealed class DatabaseBackupScheduleTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"decor-schedules-{Guid.NewGuid():N}");

    [Fact]
    public void ScheduleStore_PersistsSchedulesAcrossInstances()
    {
        var filePath = Path.Combine(_directory, "backup-schedules.json");
        var schedule = CreateSchedule(1, BackupRecurrence.Daily, true);
        var firstStore = new DatabaseBackupScheduleStore(filePath, NullLogger<DatabaseBackupScheduleStore>.Instance);
        firstStore.Upsert(schedule);

        var loaded = new DatabaseBackupScheduleStore(filePath, NullLogger<DatabaseBackupScheduleStore>.Instance).Load();

        var persisted = Assert.Single(loaded);
        Assert.Equal(schedule.Id, persisted.Id);
        Assert.Equal(schedule.StartAt, persisted.StartAt);
        Assert.Equal(schedule.NextExecutionAt, persisted.NextExecutionAt);
        Assert.Equal(schedule.Recurrence, persisted.Recurrence);
        Assert.Equal(schedule.WeekDays, persisted.WeekDays);
        Assert.Equal(schedule.Destinations, persisted.Destinations);
        Assert.Equal(schedule.IsEnabled, persisted.IsEnabled);
    }

    [Fact]
    public void NotificationSettingsStore_PersistsEnabledStateAndLeadTime()
    {
        var filePath = Path.Combine(_directory, "notification-settings.json");
        var store = new BackupNotificationSettingsStore(filePath, NullLogger<BackupNotificationSettingsStore>.Instance);
        var expected = new BackupNotificationSettings(false, 45);
        store.Save(expected);

        var loaded = new BackupNotificationSettingsStore(filePath, NullLogger<BackupNotificationSettingsStore>.Instance).Load();

        Assert.Equal(expected, loaded);
    }

    [Fact]
    public async Task Worker_ExecutesDueOneTimeBackupAndDisablesSchedule()
    {
        var backupFiles = Array.Empty<string>();
        var backup = new Mock<IDatabaseBackupService>();
        backup.Setup(service => service.CreateBackupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<string>, CancellationToken>((files, _) => backupFiles = files.ToArray())
            .ReturnsAsync(() => new DatabaseBackupResult(DateTime.Now, backupFiles));
        var store = new InMemoryScheduleStore(CreateSchedule(7, BackupRecurrence.Once, true));
        var worker = new DatabaseBackupScheduleWorker(store, backup.Object, NullLogger<DatabaseBackupScheduleWorker>.Instance);

        await worker.ProcessDueSchedulesAsync();

        backup.Verify(service => service.CreateBackupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(backupFiles);
        Assert.StartsWith(Path.Combine(_directory, "destination", "decor_backup_7_"), backupFiles[0]);
        Assert.EndsWith(".zip", backupFiles[0]);
        Assert.False(Assert.Single(store.Load()).IsEnabled);
    }

    [Fact]
    public async Task Worker_SkipsFutureSchedules()
    {
        var backup = new Mock<IDatabaseBackupService>();
        var store = new InMemoryScheduleStore(CreateSchedule(3, BackupRecurrence.Once, true) with
        {
            NextExecutionAt = DateTime.Now.AddDays(1)
        });
        var worker = new DatabaseBackupScheduleWorker(store, backup.Object, NullLogger<DatabaseBackupScheduleWorker>.Instance);

        await worker.ProcessDueSchedulesAsync();

        backup.Verify(service => service.CreateBackupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(Assert.Single(store.Load()).IsEnabled);
    }

    [Fact]
    public async Task Worker_DeliversImmediatelyWhenConfiguredAlarmTimeHasPassed()
    {
        var backup = new Mock<IDatabaseBackupService>();
        var schedule = CreateSchedule(9, BackupRecurrence.Once, true) with
        {
            NextExecutionAt = DateTime.Now.AddMinutes(10)
        };
        var store = new InMemoryScheduleStore(schedule);
        var notifications = new TestNotificationCenter();
        var worker = new DatabaseBackupScheduleWorker(
            store,
            backup.Object,
            NullLogger<DatabaseBackupScheduleWorker>.Instance,
            new TestSettingsStore(new BackupNotificationSettings(true, 15)),
            notifications);

        await worker.ProcessDueSchedulesAsync();
        await worker.ProcessDueSchedulesAsync();

        Assert.Single(notifications.Items);
        Assert.Equal("Backup agendado", notifications.Items[0].Title);
        Assert.Contains(schedule.NextExecutionAt.ToString("dd/MM/yyyy HH:mm"), notifications.Items[0].Message);
        Assert.Contains("está sendo exibido agora", notifications.Items[0].Message);
        Assert.DoesNotContain("será iniciado em 15 min", notifications.Items[0].Message);
        Assert.Equal(schedule.NextExecutionAt, Assert.Single(store.Load()).NotifiedForExecutionAt);
        backup.Verify(service => service.CreateBackupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Worker_DoesNotNotifyBeforeCalculatedAlarmTime()
    {
        var backup = new Mock<IDatabaseBackupService>();
        var schedule = CreateSchedule(10, BackupRecurrence.Once, true) with
        {
            NextExecutionAt = DateTime.Now.AddMinutes(30)
        };
        var store = new InMemoryScheduleStore(schedule);
        var notifications = new TestNotificationCenter();
        var worker = new DatabaseBackupScheduleWorker(
            store,
            backup.Object,
            NullLogger<DatabaseBackupScheduleWorker>.Instance,
            new TestSettingsStore(new BackupNotificationSettings(true, 15)),
            notifications);

        await worker.ProcessDueSchedulesAsync();

        Assert.Empty(notifications.Items);
        Assert.Null(Assert.Single(store.Load()).NotifiedForExecutionAt);
    }

    private DatabaseBackupSchedule CreateSchedule(int id, BackupRecurrence recurrence, bool enabled) => new(
        id,
        DateTime.Now.AddDays(-1),
        DateTime.Now.AddMinutes(-1),
        recurrence,
        [],
        true,
        Path.Combine(_directory, "destination"),
        false,
        string.Empty,
        [Path.Combine(_directory, "destination")],
        enabled);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class InMemoryScheduleStore(DatabaseBackupSchedule schedule) : IDatabaseBackupScheduleStore
    {
        private readonly List<DatabaseBackupSchedule> _schedules = [schedule];

        public event EventHandler? SchedulesChanged;

        public IReadOnlyList<DatabaseBackupSchedule> Load() => _schedules.ToArray();

        public void Upsert(DatabaseBackupSchedule value)
        {
            var index = _schedules.FindIndex(item => item.Id == value.Id);
            if (index < 0)
                _schedules.Add(value);
            else
                _schedules[index] = value;
            SchedulesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Delete(int id)
        {
            _schedules.RemoveAll(item => item.Id == id);
            SchedulesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Update(DatabaseBackupSchedule value)
        {
            var index = _schedules.FindIndex(item => item.Id == value.Id);
            if (index >= 0)
                _schedules[index] = value;
            SchedulesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TestSettingsStore(BackupNotificationSettings settings) : IBackupNotificationSettingsStore
    {
        public BackupNotificationSettings Load() => settings;
        public void Save(BackupNotificationSettings value) => throw new NotSupportedException();
    }

    private sealed class TestNotificationCenter : INotificationCenter
    {
        public ObservableCollection<AppNotification> Notifications { get; } = [];
        public IReadOnlyList<AppNotification> Items => Notifications;
        public void Publish(string title, string message) => Notifications.Insert(0, new AppNotification(DateTime.Now, title, message));
        public void Clear() => Notifications.Clear();
    }
}