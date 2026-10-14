namespace Decor.AvaloniaUI.Services;

public interface IDatabaseBackupScheduleStore
{
    event EventHandler? SchedulesChanged;
    IReadOnlyList<DatabaseBackupSchedule> Load();
    void Upsert(DatabaseBackupSchedule schedule);
    void Delete(int id);
    void Update(DatabaseBackupSchedule schedule);
}