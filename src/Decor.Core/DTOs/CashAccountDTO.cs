using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record CashAccountDTO(int CashAccountID, string? Name, CashAccountType AccountType, bool IsActive);