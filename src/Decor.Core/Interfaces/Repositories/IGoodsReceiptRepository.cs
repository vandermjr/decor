using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IGoodsReceiptRepository
{
    Task<int> InsertAsync(GoodsReceipt goodsReceipt, CancellationToken cancellationToken = default);
    Task<GoodsReceipt?> GetByIdAsync(int goodsReceiptId, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoodsReceipt>> GetByPurchaseOrderItemIdAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default);
}