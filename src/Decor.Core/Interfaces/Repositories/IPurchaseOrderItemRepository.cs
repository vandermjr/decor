using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IPurchaseOrderItemRepository : IRepository<PurchaseOrderItem>
{
    Task<IEnumerable<PurchaseOrderItem>> GetByPurchaseOrderIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
}