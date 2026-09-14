using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface ICashAccountRepository
{
    Task<int> SaveAsync(CashAccount account, CancellationToken cancellationToken = default);
    Task<CashAccount?> GetByIdAsync(int cashAccountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, int currentCashAccountId, CancellationToken cancellationToken = default);
    Task<int> DeactivateAsync(int cashAccountId, CancellationToken cancellationToken = default);
}