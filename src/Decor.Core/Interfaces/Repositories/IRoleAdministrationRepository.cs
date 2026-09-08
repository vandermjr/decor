using Decor.Core.DTOs;
namespace Decor.Core.Interfaces.Repositories;
public interface IRoleAdministrationRepository
{
    Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default);
}
