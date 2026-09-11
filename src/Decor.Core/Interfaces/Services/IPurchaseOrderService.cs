using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDTO> GetPurchaseOrderByIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PurchaseOrderDTO>> GetAllPurchaseOrdersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<PurchaseOrderDTO>> SearchPurchaseOrdersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SavePurchaseOrderAsync(PurchaseOrderDTO purchaseOrder, CancellationToken cancellationToken = default);
    Task DeletePurchaseOrderAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
}