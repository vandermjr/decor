using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IOrderOccurrenceRepository : IRepository<OrderOccurrence>
{
    Task<int> RegisterAsync(OrderOccurrence occurrence, Order order, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderOccurrence>> GetHistoryForOrderAsync(int orderId, CancellationToken cancellationToken = default);
}