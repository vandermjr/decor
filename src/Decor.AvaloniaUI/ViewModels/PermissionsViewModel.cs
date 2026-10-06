using Decor.Core.Common;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class PermissionsViewModel(RolesViewModel groupPermissions, UserPermissionsViewModel userPermissions,
    IAuthorizationService authorization)
{
    public RolesViewModel GroupPermissions { get; } = groupPermissions;
    public UserPermissionsViewModel UserPermissions { get; } = userPermissions;
    public bool CanViewUserPermissions => authorization.HasPermission(DecorPermissions.UsersView);

    public async Task InitializeAsync()
    {
        await GroupPermissions.InitializeAsync();
        if (CanViewUserPermissions) await UserPermissions.InitializeAsync();
    }
}