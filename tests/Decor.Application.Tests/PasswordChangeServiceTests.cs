using Decor.Application.Services;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class PasswordChangeServiceTests
{
    private const string CurrentPassword = "current-password";
    private const string NewPassword = "new-valid-pass";

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_UpdatesHashAndAuthenticatedContext()
    {
        var (service, repository, hasher, context) = CreateService();

        var result = await service.ChangePasswordAsync(CurrentPassword, NewPassword, NewPassword);

        result.Succeeded.Should().BeTrue();
        repository.UpdatePasswordCalls.Should().Be(1);
        repository.UpdatedPasswordHash.Should().NotBe(repository.PasswordHash);
        hasher.Verify(NewPassword, repository.UpdatedPasswordHash!).Should().BeTrue();
        context.User!.MustChangePassword.Should().BeFalse();
        typeof(PasswordChangeResult).GetProperties().Should().NotContain(property => property.Name == "PasswordHash");
    }

    [Fact]
    public async Task ChangePasswordAsync_WithIncorrectCurrentPassword_DoesNotChangeAnything()
    {
        var (service, repository, _, context) = CreateService();
        var originalUser = context.User;

        var result = await service.ChangePasswordAsync("wrong-password", NewPassword, NewPassword);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("A senha atual está incorreta.");
        repository.UpdatePasswordCalls.Should().Be(0);
        context.User.Should().BeSameAs(originalUser);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidNewPassword_DoesNotPersist()
    {
        var (service, repository, _, _) = CreateService();

        var result = await service.ChangePasswordAsync(CurrentPassword, "short", "short");

        result.Succeeded.Should().BeFalse();
        repository.UpdatePasswordCalls.Should().Be(0);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithDifferentConfirmation_DoesNotPersist()
    {
        var (service, repository, _, _) = CreateService();

        var result = await service.ChangePasswordAsync(CurrentPassword, NewPassword, "another-password");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("A confirmação da nova senha não confere.");
        repository.UpdatePasswordCalls.Should().Be(0);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCurrentPasswordAsNewPassword_DoesNotPersist()
    {
        var (service, repository, _, _) = CreateService();

        var result = await service.ChangePasswordAsync(CurrentPassword, CurrentPassword, CurrentPassword);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("A nova senha deve ser diferente da senha atual.");
        repository.UpdatePasswordCalls.Should().Be(0);
    }

    [Fact]
    public async Task ChangeRequiredPasswordAsync_ClearsMandatoryFlagAndDoesNotRequireCurrentPassword()
    {
        var (service, repository, hasher, context) = CreateService(mustChangePassword: true);

        var result = await service.ChangeRequiredPasswordAsync(NewPassword, NewPassword);

        result.Succeeded.Should().BeTrue();
        repository.UpdatedMustChangePassword.Should().BeFalse();
        context.User!.MustChangePassword.Should().BeFalse();
        hasher.Verify(CurrentPassword, repository.UpdatedPasswordHash!).Should().BeFalse();
        hasher.Verify(NewPassword, repository.UpdatedPasswordHash!).Should().BeTrue();
    }

    [Fact]
    public async Task ChangeRequiredPasswordAsync_WithoutMandatoryFlag_DoesNotPersist()
    {
        var (service, repository, _, _) = CreateService(mustChangePassword: false);

        var result = await service.ChangeRequiredPasswordAsync(NewPassword, NewPassword);

        result.Succeeded.Should().BeFalse();
        repository.UpdatePasswordCalls.Should().Be(0);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenPersistenceFails_LeavesContextUntouched()
    {
        var (service, repository, _, context) = CreateService(mustChangePassword: true);
        repository.UpdatePasswordResult = false;
        var originalUser = context.User;

        var result = await service.ChangePasswordAsync(CurrentPassword, NewPassword, NewPassword);

        result.Succeeded.Should().BeFalse();
        context.User.Should().BeSameAs(originalUser);
        context.User!.MustChangePassword.Should().BeTrue();
    }

    private static (PasswordChangeService Service, FakeUserRepository Repository, Pbkdf2PasswordHasher Hasher, RecordingAuthenticatedUserContext Context) CreateService(bool mustChangePassword = false)
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeUserRepository { PasswordHash = hasher.Hash(CurrentPassword) };
        var context = new RecordingAuthenticatedUserContext();
        context.SignIn(new ApplicationUser
        {
            UserID = 1,
            Username = "admin",
            DisplayName = "Administrador",
            IsActive = true,
            MustChangePassword = mustChangePassword,
            Roles = ["Administrador"],
            Permissions = ["Products.View"]
        });
        return (new PasswordChangeService(repository, hasher, new PasswordPolicy(), context), repository, hasher, context);
    }
}
