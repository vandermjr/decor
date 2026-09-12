using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetCompleteOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default);
    Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId, CancellationToken cancellationToken = default);
    Task<int> SaveOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default);
    Task<int> SaveSpecificationValueAsync(OrderItemSpecificationValue value, CancellationToken cancellationToken = default);
}
