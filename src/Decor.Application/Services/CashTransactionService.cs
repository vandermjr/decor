using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class CashTransactionService(ICashTransactionRepository transactionRepository, ICashAccountRepository accountRepository, IAuthorizationService authorizationService) : ICashTransactionService
{
    public async Task<CashTransactionDTO> CreateAsync(int cashAccountId, decimal amount, CashTransactionType transactionType, string? sourceType, int? sourceId, int createdByEmployeeId, DateTime? transactionDate = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashTransactionsCreate);
        ValidateAmount(amount);
        if (transactionType == CashTransactionType.Transfer || transactionType is not (CashTransactionType.Income or CashTransactionType.Expense)) throw new ValidationException("Transferências devem ser criadas em par por CreateTransferAsync.");
        await RequireActiveAccountAsync(cashAccountId, cancellationToken);
        var transaction = new CashTransaction { CashAccountID = cashAccountId, Amount = amount, TransactionType = transactionType, TransferID = null, SourceType = sourceType, SourceID = sourceId, TransactionDate = transactionDate ?? DateTime.UtcNow, CreatedByEmployeeID = createdByEmployeeId, CreatedAt = DateTime.UtcNow };
        await transactionRepository.RegisterAsync(transaction, cancellationToken);
        return transaction.ToDTO();
    }

    public async Task<Guid> CreateTransferAsync(int fromAccountId, int toAccountId, decimal amount, int createdByEmployeeId, string? sourceType = null, int? sourceId = null, DateTime? transactionDate = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashTransactionsCreate);
        ValidateAmount(amount);
        if (fromAccountId == toAccountId) throw new ValidationException("A conta de origem deve ser diferente da conta de destino.");
        await RequireActiveAccountAsync(fromAccountId, cancellationToken);
        await RequireActiveAccountAsync(toAccountId, cancellationToken);
        var now = DateTime.UtcNow;
        var date = transactionDate ?? now;
        return await transactionRepository.RegisterTransferAsync(
            new CashTransaction { CashAccountID = fromAccountId, Amount = amount, TransactionType = CashTransactionType.Expense, SourceType = sourceType, SourceID = sourceId, TransactionDate = date, CreatedByEmployeeID = createdByEmployeeId, CreatedAt = now },
            new CashTransaction { CashAccountID = toAccountId, Amount = amount, TransactionType = CashTransactionType.Income, SourceType = sourceType, SourceID = sourceId, TransactionDate = date, CreatedByEmployeeID = createdByEmployeeId, CreatedAt = now }, cancellationToken);
    }

    public async Task<CashTransactionDTO> GetByIdAsync(int cashTransactionId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashTransactionsView);
        var item = await transactionRepository.GetByIdAsync(cashTransactionId, cancellationToken) ?? throw new KeyNotFoundException($"Lançamento {cashTransactionId} não encontrado.");
        return item.ToDTO();
    }

    public async Task<IReadOnlyList<CashTransactionDTO>> GetByCashAccountAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashTransactionsView);
        return (await transactionRepository.GetByCashAccountAsync(cashAccountId, cancellationToken)).Select(t => t.ToDTO()).ToList();
    }

    public async Task<decimal> GetBalanceAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashTransactionsView);
        return await transactionRepository.GetBalanceAsync(cashAccountId, cancellationToken);
    }

    private async Task RequireActiveAccountAsync(int accountId, CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null || !account.IsActive) throw new ValidationException("A conta de caixa deve existir e estar ativa.");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0) throw new ValidationException("O valor da transação deve ser maior que zero.");
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission)) throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}