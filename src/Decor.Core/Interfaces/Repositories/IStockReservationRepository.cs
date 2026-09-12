using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IStockReservationRepository : IRepository<StockReservation>
{
    Task<StockReservation?> GetByIdAsync(int reservationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockReservation>> GetByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockReservation>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockReservation>> GetActiveByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<StockReservation?> GetActiveByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockReservation>> GetActiveByProductAndLocationAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalActiveReservedQuantityAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default);
}
