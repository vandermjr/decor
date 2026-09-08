namespace Decor.Core.DTOs;

public sealed record AdministrativeUserDTO(int UserID, string Username, string DisplayName, bool IsActive,
    IReadOnlyCollection<AdministrativeRoleDTO> Roles, IReadOnlyCollection<PermissionOverrideDTO> PermissionOverrides);
