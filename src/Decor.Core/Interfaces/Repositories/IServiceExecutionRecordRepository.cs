using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IServiceExecutionRecordRepository : IRepository<ServiceExecutionRecord>
{
    Task<ServiceExecutionRecord?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default);
}
