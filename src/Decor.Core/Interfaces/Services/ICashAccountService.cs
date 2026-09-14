using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Services;

public interface ICashAccountService
{
    Task<CashAccountDTO> CreateAsync(string name, CashAccountType accountType, CancellationToken cancellationToken = default);
    Task<CashAccountDTO> UpdateAsync(int cashAccountId, string name, CashAccountType accountType, CancellationToken cancellationToken = default);
    Task<CashAccountDTO> GetByIdAsync(int cashAccountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashAccountDTO>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeactivateAsync(int cashAccountId, CancellationToken cancellationToken = default);
}