using Decor.Application.Services;
using Decor.Core.Common;
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

        var result = await service.AuthenticateAsync(1, "password");

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

        var result = await service.AuthenticateAsync(1, "password");

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

        var result = await service.AuthenticateAsync(1, "wrong-password");

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

        var result = await service.AuthenticateAsync(999, "password");

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

        var result = await service.AuthenticateAsync(1, "password");

        result.Succeeded.Should().BeFalse();
        repository.UserRequests.Should().Be(1);
        context.SignInCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(0, "password")]
    [InlineData(-1, "password")]
    [InlineData(1, "")]
    public async Task AuthenticateAsync_WithInvalidUserIdOrEmptyPassword_DoesNotAccessRepository(int userId, string password)
    {
        var repository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher();
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync(userId, password);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("Informe o ID do usuário e a senha.");
        repository.PasswordHashRequests.Should().Be(0);
        repository.UserRequests.Should().Be(0);
        context.SignInCalls.Should().Be(0);
    }

    [Fact]
    public async Task AuthenticateAsync_QueriesByUserId()
    {
        var repository = new FakeUserRepository { PasswordHash = "hash", User = CreateUser() };
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var service = new AuthenticationService(repository, passwordHasher, new RecordingAuthenticatedUserContext());

        await service.AuthenticateAsync(152, "password");

        repository.RequestedPasswordHashUserId.Should().Be(152);
        repository.RequestedUserId.Should().Be(152);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenUserCannotBeLoadedAfterPasswordVerification_DoesNotSignIn()
    {
        var repository = new FakeUserRepository { PasswordHash = "hash", User = null };
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync(1, "password");

        result.Succeeded.Should().BeFalse();
        context.SignInCalls.Should().Be(0);
    }

    [Fact]
    public async Task AuthenticateAsync_WithDuplicateDisplayNamesAuthenticatesOnlyRequestedUserId()
    {
        var firstUser = CreateUser(userId: 152, displayName: "João");
        var secondUser = CreateUser(userId: 187, displayName: "João");
        var repository = new FakeUserRepository();
        repository.Users.Add(firstUser.UserID, firstUser);
        repository.Users.Add(secondUser.UserID, secondUser);
        repository.PasswordHashes.Add(firstUser.UserID, "first-hash");
        repository.PasswordHashes.Add(secondUser.UserID, "second-hash");
        var passwordHasher = new FakePasswordHasher { VerificationResult = true };
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, passwordHasher, context);

        var result = await service.AuthenticateAsync(187, "password");

        result.Succeeded.Should().BeTrue();
        result.User.Should().BeSameAs(secondUser);
        result.User!.UserID.Should().Be(187);
        repository.RequestedPasswordHashUserId.Should().Be(187);
        repository.RequestedUserId.Should().Be(187);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmployeeIdWithoutUserAccount_DoesNotAuthenticateEmployee()
    {
        var employee = new Employee { EmployeeID = 37, Name = "Faxineira", UserID = null };
        var repository = new FakeUserRepository();
        var context = new RecordingAuthenticatedUserContext();
        var service = new AuthenticationService(repository, new FakePasswordHasher(), context);

        var result = await service.AuthenticateAsync(employee.EmployeeID, "password");

        result.Succeeded.Should().BeFalse();
        repository.RequestedPasswordHashUserId.Should().Be(employee.EmployeeID);
        repository.UserRequests.Should().Be(0);
        context.SignInCalls.Should().Be(0);
    }

    private static ApplicationUser CreateUser(bool isActive = true, bool mustChangePassword = false, int userId = 1, string displayName = "Administrador") => new()
    {
        UserID = userId,
        Username = "admin",
        DisplayName = displayName,
        IsActive = isActive,
        MustChangePassword = mustChangePassword,
        Roles = [SystemRoleDefaults.Administrators],
        Permissions = ["Products.View"]
    };
}
