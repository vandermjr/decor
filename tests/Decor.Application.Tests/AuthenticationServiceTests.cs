using Decor.Application.Services;
using Decor.Core.Entities;

namespace Decor.Application.Tests;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsUserAndSignsIn()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository { PasswordHash = "hash", User = user };
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync("admin", "password");

        result.Succeeded.Should().BeTrue();
        result.User.Should().BeSameAs(user);
        context.User.Should().BeSameAs(user);
        context.SignInCalls.Should().Be(1);
        repository.PasswordHashRequests.Should().Be(1);
        repository.UserRequests.Should().Be(1);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUserRequiringPasswordChange_PreservesStateInResultAndContext()
    {
        var user = CreateUser(mustChangePassword: true);
        var repository = new FakeUserRepository { PasswordHash = "hash", User = user };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, new FakePasswordHasher { VerificationResult = true }, context);

        var result = await service.AuthenticateAsync("admin", "password");

        result.Succeeded.Should().BeTrue();
        result.User!.MustChangePassword.Should().BeTrue();
        context.User!.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithIncorrectPassword_DoesNotLoadUserOrSignIn()
    {
        var repository = new FakeUserRepository { PasswordHash = "hash", User = CreateUser() };
        var passwordHasher = new FakePasswordHasher { VerificationResult = false };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync("admin", "wrong-password");

        result.Succeeded.Should().BeFalse();
        result.User.Should().BeNull();
        repository.UserRequests.Should().Be(0);
        context.SignInCalls.Should().Be(0);
        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonexistentUser_DoesNotVerifyOrSignIn()
    {
        var repository = new FakeUserRepository { PasswordHash = null };
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync("missing", "password");

        result.Succeeded.Should().BeFalse();
        passwordHasher.VerifyCalls.Should().Be(0);
        repository.UserRequests.Should().Be(0);
        context.SignInCalls.Should().Be(0);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveUser_DoesNotSignIn()
    {
        var repository = new FakeUserRepository { PasswordHash = "hash", User = CreateUser(isActive: false) };
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync("inactive", "password");

        result.Succeeded.Should().BeFalse();
        repository.UserRequests.Should().Be(1);
        context.SignInCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("   ", "password")]
    [InlineData("admin", "")]
    public async Task AuthenticateAsync_WithEmptyUsernameOrPassword_DoesNotAccessRepository(string username, string password)
    {
        var repository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher();
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync(username, password);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("Informe o usuário e a senha.");
        repository.PasswordHashRequests.Should().Be(0);
        repository.UserRequests.Should().Be(0);
        context.SignInCalls.Should().Be(0);
    }

    [Fact]
    public async Task AuthenticateAsync_TrimsUsernameBeforeQueryingRepository()
    {
        var repository = new FakeUserRepository { PasswordHash = "hash", User = CreateUser() };
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var service = new AuthenticationService(repository, passwordHasher, new RecordingAuthenticatedUserContext());

        await service.AuthenticateAsync("  admin  ", "password");

        repository.RequestedPasswordHashUsername.Should().Be("admin");
        repository.RequestedUserUsername.Should().Be("admin");
    }

    [Fact]
    public async Task AuthenticateAsync_WhenUserCannotBeLoadedAfterPasswordVerification_DoesNotSignIn()
    {
        var repository = new FakeUserRepository { PasswordHash = "hash", User = null };
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync("admin", "password");

        result.Succeeded.Should().BeFalse();
        context.SignInCalls.Should().Be(0);
    }

    private static ApplicationUser CreateUser(bool isActive = true, bool mustChangePassword = false) => new()
    {
        UserID = 1,
        Username = "admin",
        DisplayName = "Administrador",
        IsActive = isActive,
        MustChangePassword = mustChangePassword,
        Roles = ["Administrador"],
        Permissions = ["Products.View"]
    };
}
