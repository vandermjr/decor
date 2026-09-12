using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IOrderService
{
    Task<IEnumerable<OrderDTO>> SearchOrdersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderDTO>> GetAllOrdersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<OrderDTO> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<OrderDTO> ConvertFromQuoteAsync(int quoteSectionId, CancellationToken cancellationToken = default);
    Task ApproveOrderAsync(int orderId, bool requiresDownPayment, DateTime? manufacturingDeadline = null, DateTime? installationDeadline = null, CancellationToken cancellationToken = default);
    Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task SendItemToProductionAsync(int orderItemId, int sentToProductionByEmployeeID, CancellationToken cancellationToken = default);
}
