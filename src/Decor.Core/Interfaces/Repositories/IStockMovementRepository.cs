using System.Data;
using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;
public interface IStockMovementRepository
{
    Task<int> RegisterMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task<Guid> RegisterTransferAsync(StockMovement outboundMovement, StockMovement inboundMovement, CancellationToken cancellationToken = default);
    Task<StockMovement?> GetByIdAsync(int stockMovementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockMovement>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockMovement>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default);
    Task<int> UpdateReviewAsync(Guid transferId, StockMovementReviewStatus reviewStatus, int reviewedByEmployeeId, DateTime reviewedAt, CancellationToken cancellationToken = default);
}

public interface ITransactionalStockMovementRepository
{
    Task<int> RegisterMovementAsync(StockMovement movement, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}