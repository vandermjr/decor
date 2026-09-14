using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IPurchaseOrderInstallmentRepository : IRepository<PurchaseOrderInstallment>
{
    Task<IReadOnlyList<PurchaseOrderInstallment>> GetByPurchaseOrderIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderInstallment?> GetByIdAsync(int installmentId, CancellationToken cancellationToken = default);
}