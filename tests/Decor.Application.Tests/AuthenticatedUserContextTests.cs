using Decor.Application.Services;
using Decor.Core.Entities;

namespace Decor.Application.Tests;

public sealed class AuthenticatedUserContextTests
{
    [Fact]
    public void NewContext_IsNotAuthenticated()
    {
        var context = new AuthenticatedUserContext();

        context.IsAuthenticated.Should().BeFalse();
        context.User.Should().BeNull();
    }

    [Fact]
    public void SignIn_StoresUserAndMarksContextAsAuthenticated()
    {
        var context = new AuthenticatedUserContext();
        var user = new ApplicationUser { Username = "admin", IsActive = true };

        context.SignIn(user);

        context.IsAuthenticated.Should().BeTrue();
        context.User.Should().BeSameAs(user);
    }

    [Fact]
    public void SignOut_ClearsAuthenticatedUser()
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(new ApplicationUser { Username = "admin", IsActive = true });

        context.SignOut();

        context.IsAuthenticated.Should().BeFalse();
        context.User.Should().BeNull();
    }

    [Fact]
    public void SignOut_WhenAuthenticated_RaisesLocalSessionEndedOnce()
    {
        var context = new AuthenticatedUserContext();
        var raised = 0;
        context.SignedOut += (_, _) => raised++;
        context.SignIn(new ApplicationUser { Username = "admin", IsActive = true });

        context.SignOut();
        context.SignOut();

        raised.Should().Be(1);
        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void SignIn_PreservesMustChangePasswordStateWithoutCredentials()
    {
        var context = new AuthenticatedUserContext();
        var user = new ApplicationUser { Username = "admin", IsActive = true, MustChangePassword = true };

        context.SignIn(user);

        context.User.Should().NotBeNull();
        context.User!.MustChangePassword.Should().BeTrue();
        typeof(ApplicationUser).GetProperty("PasswordHash").Should().BeNull();
    }

    [Fact]
    public void ApplicationUser_DoesNotExposePasswordHashToAuthenticatedContext()
    {
        typeof(ApplicationUser).GetProperty("PasswordHash").Should().BeNull();
        typeof(AuthenticatedUserContext).GetProperty(nameof(AuthenticatedUserContext.User))!
            .PropertyType.Should().Be(typeof(ApplicationUser));
    }
}
