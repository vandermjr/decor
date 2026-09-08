using Decor.Application.Services;
using Decor.Core.Entities;

namespace Decor.Application.Tests;

public sealed class AuthorizationServiceTests
{
    [Fact]
    public void HasPermission_ReturnsFalseWhenNoUserIsAuthenticated()
    {
        var service = new AuthorizationService(new AuthenticatedUserContext());

        service.HasPermission("Products.View").Should().BeFalse();
    }

    [Theory]
    [InlineData("View")]
    [InlineData("Create")]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public void ResourceOperations_ReturnFalseWhenNoUserIsAuthenticated(string operation)
    {
        var service = new AuthorizationService(new AuthenticatedUserContext());

        var allowed = operation switch
        {
            "View" => service.CanView("Products"),
            "Create" => service.CanCreate("Products"),
            "Edit" => service.CanEdit("Products"),
            "Delete" => service.CanDelete("Products"),
            _ => throw new InvalidOperationException()
        };

        allowed.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_MatchesPermissionCaseInsensitively()
    {
        var context = CreateContext("Products.View");
        var service = new AuthorizationService(context);

        service.HasPermission("products.view").Should().BeTrue();
        service.HasPermission("Products.Delete").Should().BeFalse();
    }

    [Theory]
    [InlineData("Products.View", "Products", "View")]
    [InlineData("Products.Create", "Products", "Create")]
    [InlineData("Products.Edit", "Products", "Edit")]
    [InlineData("Products.Delete", "Products", "Delete")]
    public void ResourceOperations_CheckExpectedPermission(string permission, string resource, string operation)
    {
        var service = new AuthorizationService(CreateContext(permission));

        var allowed = operation switch
        {
            "View" => service.CanView(resource),
            "Create" => service.CanCreate(resource),
            "Edit" => service.CanEdit(resource),
            "Delete" => service.CanDelete(resource),
            _ => throw new InvalidOperationException()
        };

        allowed.Should().BeTrue();
    }

    private static AuthenticatedUserContext CreateContext(params string[] permissions)
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(new ApplicationUser
        {
            Username = "admin",
            IsActive = true,
            Permissions = permissions
        });
        return context;
    }
}
