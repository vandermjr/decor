using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IInstallationAppointmentRepository : IRepository<InstallationAppointment>
{
    Task<InstallationAppointment?> GetByIdAsync(int appointmentId, CancellationToken cancellationToken = default);
    Task<bool> ServiceOrderItemExistsAsync(int orderItemId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveConflictAsync(int? executorEmployeeId, int? executorPartnerId, DateTime scheduledDate, int? excludingAppointmentId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InstallationAppointment>> GetScheduleForExecutorAsync(int? executorEmployeeId, int? executorPartnerId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppointmentReschedule>> GetReschedulesAsync(int appointmentId, CancellationToken cancellationToken = default);
    Task<int> RescheduleAsync(InstallationAppointment appointment, AppointmentReschedule reschedule, CancellationToken cancellationToken = default);
}