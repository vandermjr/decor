using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IPaymentMethodService
{
    Task<PaymentMethodDTO> GetPaymentMethodByIdAsync(int paymentMethodId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentMethodDTO>> GetAllPaymentMethodsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentMethodDTO>> SearchPaymentMethodsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SavePaymentMethodAsync(PaymentMethodDTO paymentMethod, CancellationToken cancellationToken = default);
    Task DeletePaymentMethodAsync(int paymentMethodId, CancellationToken cancellationToken = default);
}
