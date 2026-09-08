namespace Decor.Core.Interfaces.Services;

public interface IAuthorizationService
{
    bool HasPermission(string permissionCode);
    bool CanView(string resource);
    bool CanCreate(string resource);
    bool CanEdit(string resource);
    bool CanDelete(string resource);
}
