using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IOrderInstallmentService
{
    Task<OrderInstallmentDTO> GetInstallmentByIdAsync(int installmentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderInstallmentDTO>> GetInstallmentsByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderInstallmentDTO>> CreateInstallmentPlanAsync(int orderId, IEnumerable<InstallmentItemInputDTO> items, CancellationToken cancellationToken = default);
    Task RegisterPaymentAsync(int installmentId, int receivedByEmployeeId, CancellationToken cancellationToken = default);
    Task MarkOverdueAsync(int installmentId, CancellationToken cancellationToken = default);
    Task CancelInstallmentAsync(int installmentId, CancellationToken cancellationToken = default);
    Task CancelInstallmentsForOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
