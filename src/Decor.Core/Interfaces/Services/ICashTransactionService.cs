using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Services;

public interface ICashTransactionService
{
    Task<CashTransactionDTO> CreateAsync(int cashAccountId, decimal amount, CashTransactionType transactionType, string? sourceType, int? sourceId, int createdByEmployeeId, DateTime? transactionDate = null, CancellationToken cancellationToken = default);
    Task<Guid> CreateTransferAsync(int fromAccountId, int toAccountId, decimal amount, int createdByEmployeeId, string? sourceType = null, int? sourceId = null, DateTime? transactionDate = null, CancellationToken cancellationToken = default);
    Task<CashTransactionDTO> GetByIdAsync(int cashTransactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashTransactionDTO>> GetByCashAccountAsync(int cashAccountId, CancellationToken cancellationToken = default);
    Task<decimal> GetBalanceAsync(int cashAccountId, CancellationToken cancellationToken = default);
}