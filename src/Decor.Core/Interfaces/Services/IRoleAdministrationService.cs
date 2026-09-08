using Decor.Core.DTOs;
namespace Decor.Core.Interfaces.Services;
public interface IRoleAdministrationService
{
    Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default);
    Task RestoreDefaultsAsync(int roleId, CancellationToken cancellationToken = default);
}
