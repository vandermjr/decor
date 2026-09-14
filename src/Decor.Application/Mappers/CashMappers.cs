using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Application.Mappers;

public static class CashMappers
{
    public static CashAccountDTO ToDTO(this CashAccount account) => new(account.CashAccountID, account.Name, account.AccountType, account.IsActive);
    public static CashTransactionDTO ToDTO(this CashTransaction transaction) => new(transaction.CashTransactionID, transaction.CashAccountID, transaction.Amount, transaction.TransactionType, transaction.TransferID, transaction.SourceType, transaction.SourceID, transaction.TransactionDate, transaction.CreatedByEmployeeID, transaction.CreatedAt);
}