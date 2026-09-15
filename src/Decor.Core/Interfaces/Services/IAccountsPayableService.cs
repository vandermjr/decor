using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IAccountsPayableService
{
    Task<AccountsPayableDTO> CreateAsync(AccountsPayableDTO dto, CancellationToken cancellationToken = default);
    Task<AccountsPayableDTO> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task RegisterPaymentAsync(int id, int paidByEmployeeId, CancellationToken cancellationToken = default);
    Task CancelAsync(int id, CancellationToken cancellationToken = default);
}