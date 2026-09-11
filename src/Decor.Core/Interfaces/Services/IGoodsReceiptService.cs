using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IGoodsReceiptService
{
    Task<int> RegisterReceiptAsync(int purchaseOrderItemId, decimal quantityReceived, int receivedByEmployeeId, bool hasDivergence, string? divergenceNotes = null, CancellationToken cancellationToken = default);
    Task<GoodsReceiptDTO> GetByIdAsync(int goodsReceiptId, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoodsReceiptDTO>> GetByPurchaseOrderItemIdAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default);
}