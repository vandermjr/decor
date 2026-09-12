using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IPaymentMethodRepository : IRepository<PaymentMethod>
{
    bool IsInUse(int paymentMethodId);
    Task<bool> IsInUseAsync(int paymentMethodId, CancellationToken cancellationToken = default);
    bool NameExists(string name, int currentPaymentMethodId);
    Task<bool> NameExistsAsync(string name, int currentPaymentMethodId, CancellationToken cancellationToken = default);
}
