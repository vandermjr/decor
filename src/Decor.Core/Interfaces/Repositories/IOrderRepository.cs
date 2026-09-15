using System.Data;
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

public interface ITransactionalOrderRepository
{
    Task<int> SaveAsync(Order entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<int> SaveOrderItemAsync(OrderItem item, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<int> SaveSpecificationValueAsync(OrderItemSpecificationValue value, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
