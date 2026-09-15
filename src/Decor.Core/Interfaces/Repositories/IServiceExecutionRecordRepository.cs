using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IServiceExecutionRecordRepository : IRepository<ServiceExecutionRecord>
{
    Task<ServiceExecutionRecord?> GetByIdAsync(int executionId, CancellationToken cancellationToken = default);
    Task<ServiceExecutionRecord?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default);
}
