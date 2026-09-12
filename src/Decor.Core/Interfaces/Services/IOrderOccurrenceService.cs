using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IOrderOccurrenceService
{
    Task<OrderOccurrenceDTO> RegisterOccurrenceAsync(int orderId, int reasonId, int registeredByEmployeeId, string observation, DateTime? newManufacturingDeadline = null, DateTime? newInstallationDeadline = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderOccurrenceDTO>> GetHistoryForOrderAsync(int orderId, CancellationToken cancellationToken = default);
}