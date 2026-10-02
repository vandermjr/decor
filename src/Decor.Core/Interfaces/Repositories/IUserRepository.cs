using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<string?> GetPasswordHashByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default);
}
