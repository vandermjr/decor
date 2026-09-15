using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IPurchaseOrderInstallmentService
{
    Task<PurchaseOrderInstallmentDTO> GetInstallmentByIdAsync(int installmentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PurchaseOrderInstallmentDTO>> GetInstallmentsByPurchaseOrderIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PurchaseOrderInstallmentDTO>> CreateInstallmentPlanAsync(int purchaseOrderId, IEnumerable<InstallmentItemInputDTO> items, CancellationToken cancellationToken = default);
    Task RegisterPaymentAsync(int installmentId, int paidByEmployeeId, int paidFromCashAccountId, CancellationToken cancellationToken = default);
    Task MarkOverdueAsync(int installmentId, CancellationToken cancellationToken = default);
    Task CancelInstallmentAsync(int installmentId, CancellationToken cancellationToken = default);
    Task CancelInstallmentsForPurchaseOrderAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
}