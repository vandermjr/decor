using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface ICashTransactionRepository
{
    Task<int> RegisterAsync(CashTransaction transaction, CancellationToken cancellationToken = default);
    Task<Guid> RegisterTransferAsync(CashTransaction expense, CashTransaction income, CancellationToken cancellationToken = default);
    Task<CashTransaction?> GetByIdAsync(int cashTransactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashTransaction>> GetByCashAccountAsync(int cashAccountId, CancellationToken cancellationToken = default);
    Task<decimal> GetBalanceAsync(int cashAccountId, CancellationToken cancellationToken = default);
}