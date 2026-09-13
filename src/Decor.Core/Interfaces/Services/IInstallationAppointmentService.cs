using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IInstallationAppointmentService
{
    Task<InstallationAppointmentDTO> CreateAppointmentAsync(int orderItemId, DateTime scheduledDate, TimeSpan? scheduledTime, int? executorEmployeeId, int? executorPartnerId, int createdByEmployeeId, CancellationToken cancellationToken = default);
    Task<InstallationAppointmentDTO> RescheduleAsync(int appointmentId, DateTime newDate, string reason, int registeredByEmployeeId, CancellationToken cancellationToken = default);
    Task CancelAsync(int appointmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InstallationAppointmentDTO>> GetScheduleForExecutorAsync(int? executorEmployeeId, int? executorPartnerId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppointmentRescheduleDTO>> GetRescheduleHistoryAsync(int appointmentId, CancellationToken cancellationToken = default);
}