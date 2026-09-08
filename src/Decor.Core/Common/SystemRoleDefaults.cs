namespace Decor.Core.Common;

/// <summary>Deterministic source for restoring system roles; it never depends on their current database state.</summary>
public static class SystemRoleDefaults
{
    public const string Administrator = "Administrador";
    public const string Supervisor = "Supervisor";
    public static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> Permissions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Administrator] = [DecorPermissions.ProductsView, DecorPermissions.ProductsCreate, DecorPermissions.ProductsEdit, DecorPermissions.BrandsView, DecorPermissions.BrandsCreate, DecorPermissions.BrandsEdit, DecorPermissions.BrandsDelete, DecorPermissions.ClassificationsView, DecorPermissions.TermDeliveryView, DecorPermissions.UsersView, DecorPermissions.UsersCreate, DecorPermissions.UsersEdit, DecorPermissions.UsersActivate, DecorPermissions.UsersDeactivate, DecorPermissions.UsersAssignRoles, DecorPermissions.UsersManagePermissions, DecorPermissions.UsersResetPassword, DecorPermissions.UsersRestorePermissions, DecorPermissions.RolesView, DecorPermissions.RolesEdit, DecorPermissions.RolesManagePermissions, DecorPermissions.RolesRestoreDefaults],
            [Supervisor] = [DecorPermissions.UsersView, DecorPermissions.UsersCreate, DecorPermissions.UsersEdit, DecorPermissions.UsersActivate, DecorPermissions.UsersDeactivate, DecorPermissions.UsersAssignRoles, DecorPermissions.UsersManagePermissions, DecorPermissions.UsersResetPassword, DecorPermissions.RolesView, DecorPermissions.RolesEdit, DecorPermissions.RolesManagePermissions]
        };
}
