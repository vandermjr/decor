using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class AuthorizationService(IAuthenticatedUserContext authenticatedUserContext) : IAuthorizationService
{
    public bool HasPermission(string permissionCode) =>
        authenticatedUserContext.User?.Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase) == true;

    public bool CanView(string resource) => HasPermission($"{resource}.View");
    public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
    public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
    public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
}
