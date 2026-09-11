using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IPurchaseOrderItemService
{
    Task<PurchaseOrderItemDTO> GetPurchaseOrderItemByIdAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PurchaseOrderItemDTO>> GetAllPurchaseOrderItemsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<PurchaseOrderItemDTO>> SearchPurchaseOrderItemsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SavePurchaseOrderItemAsync(PurchaseOrderItemDTO purchaseOrderItem, CancellationToken cancellationToken = default);
    Task DeletePurchaseOrderItemAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default);
}