using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Services;

public interface IStockMovementService
{
    Task<int> RegisterEntryAsync(int productId, int stockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default);
    Task<int> RegisterExitAsync(int productId, int stockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default);
    Task<int> RegisterAdjustmentAsync(int productId, int stockLocationId, decimal quantityDelta, StockAdjustmentReason reason, string justification, int authorizedByEmployeeId, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default);
    Task<Guid> RegisterTransferAsync(int productId, int sourceStockLocationId, int destinationStockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, int? orderItemId = null, CancellationToken cancellationToken = default);
    Task ReviewTransferAsync(Guid transferId, StockMovementReviewStatus reviewStatus, int reviewedByEmployeeId, CancellationToken cancellationToken = default);
    Task<StockMovementDTO> GetByIdAsync(int stockMovementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockMovementDTO>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockMovementDTO>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default);
}