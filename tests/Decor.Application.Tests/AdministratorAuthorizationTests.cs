using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.Entities;
using FluentAssertions;

namespace Decor.Application.Tests;

public sealed class AdministratorAuthorizationTests
{
    [Fact]
    public void ReservedAdministrator_HasAllPermissionsWithoutExplicitGrants()
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(new ApplicationUser { UserID = 1, Username = SystemAccountDefaults.AdministratorUsername });
        var authorization = new AuthorizationService(context);
        authorization.HasPermission(DecorPermissions.RolesEdit).Should().BeTrue();
        authorization.HasPermission("FutureModule.Edit").Should().BeTrue();
        context.SignOut();
        authorization.HasPermission(DecorPermissions.RolesEdit).Should().BeFalse();
    }

    [Fact]
    public void RegularUser_StillRequiresExplicitPermissions()
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(new ApplicationUser { UserID = 2, Username = "operador", Permissions = [DecorPermissions.RolesView] });
        var authorization = new AuthorizationService(context);
        authorization.HasPermission(DecorPermissions.RolesView).Should().BeTrue();
        authorization.HasPermission(DecorPermissions.RolesEdit).Should().BeFalse();
    }
}