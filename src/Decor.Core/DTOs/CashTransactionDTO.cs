using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record CashTransactionDTO(int CashTransactionID, int CashAccountID, decimal Amount, CashTransactionType TransactionType, Guid? TransferID, string? SourceType, int? SourceID, DateTime TransactionDate, int CreatedByEmployeeID, DateTime CreatedAt);