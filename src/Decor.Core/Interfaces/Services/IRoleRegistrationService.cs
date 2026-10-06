using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IRoleRegistrationService
{
    Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetEditableRoleIdsAsync(CancellationToken cancellationToken = default);
    Task<AdministrativeRoleDTO> SaveAsync(int roleId, string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default);
}