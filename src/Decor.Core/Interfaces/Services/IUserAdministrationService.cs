using Decor.Core.DTOs;
namespace Decor.Core.Interfaces.Services;
public interface IUserAdministrationService
{
    Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default);
    Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<TemporaryPasswordResult> CreateAsync(string username, string displayName, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default);
    Task UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default);
    Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);
    Task ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default);
    Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default);
    Task RestorePermissionsAsync(int userId, CancellationToken cancellationToken = default);
    Task<TemporaryPasswordResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken = default);
}
public sealed record TemporaryPasswordResult(string TemporaryPassword);
