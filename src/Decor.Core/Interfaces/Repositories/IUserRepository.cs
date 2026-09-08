using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<string?> GetPasswordHashByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default);
}
