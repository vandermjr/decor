using Decor.Core.Common;

namespace Decor.Core.DTOs;

public sealed record AdministrativeUserDTO(int UserID, string Username, string DisplayName, bool IsActive,
    IReadOnlyCollection<AdministrativeRoleDTO> Roles, IReadOnlyCollection<PermissionOverrideDTO> PermissionOverrides,
    string? EmployeeName = null)
{
    public bool IsSystemAdministrator => SystemAccountDefaults.IsAdministrator(Username);
    public string PresentationName => IsSystemAdministrator
        ? SystemAccountDefaults.AdministratorName
        : Username;
}
