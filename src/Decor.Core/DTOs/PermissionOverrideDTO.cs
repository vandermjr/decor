namespace Decor.Core.DTOs;

public sealed record PermissionOverrideDTO(int PermissionID, string PermissionCode, bool IsGranted);
