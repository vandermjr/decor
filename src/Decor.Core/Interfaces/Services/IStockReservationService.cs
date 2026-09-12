using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IStockReservationService
{
    Task<StockReservationDTO> CreateReservationAsync(int orderItemId, int stockLocationId, int createdByEmployeeId, decimal quantity, CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(int reservationId, int? releasedByEmployeeId = null, CancellationToken cancellationToken = default);
    Task ReleaseActiveReservationsForOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<StockReservationDTO> GetByIdAsync(int reservationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockReservationDTO>> GetByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockReservationDTO>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockReservationDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockReservationDTO>> SearchAsync(string? searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}
