namespace Decor.Core.DTOs;

public sealed record AdministrativeRoleDTO(int RoleID, string RoleName, string? Description, int HierarchyLevel, bool IsSystemProtected);
