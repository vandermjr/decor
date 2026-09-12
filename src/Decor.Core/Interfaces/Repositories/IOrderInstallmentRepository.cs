using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IOrderInstallmentRepository : IRepository<OrderInstallment>
{
    Task<IReadOnlyList<OrderInstallment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<OrderInstallment?> GetByIdAsync(int installmentId, CancellationToken cancellationToken = default);
}
