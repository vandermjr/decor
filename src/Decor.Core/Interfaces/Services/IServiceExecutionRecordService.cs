using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IServiceExecutionRecordService
{
    Task<ServiceExecutionRecordDTO> RegisterExecutionAsync(int appointmentId, DateTime executedAt, string? observations, bool customerPresent, bool? customerSignedConfirmation, string? absentAuthorizationNote, CancellationToken cancellationToken = default);
    Task<ServiceExecutionRecordDTO?> GetExecutionForAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default);
}
