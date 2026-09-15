using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IAccountsPayableRepository : IRepository<AccountsPayable>
{
    Task<AccountsPayable?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}