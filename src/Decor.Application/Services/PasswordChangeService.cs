using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class PasswordChangeService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPasswordPolicy passwordPolicy,
    IAuthenticatedUserContext authenticatedUserContext) : IPasswordChangeService
{
    public Task<PasswordChangeResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(currentPassword, newPassword, confirmPassword, requireCurrentPassword: true, cancellationToken);

    public Task<PasswordChangeResult> ChangeRequiredPasswordAsync(
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(null, newPassword, confirmPassword, requireCurrentPassword: false, cancellationToken);

    private async Task<PasswordChangeResult> ChangeAsync(
        string? currentPassword,
        string newPassword,
        string confirmPassword,
        bool requireCurrentPassword,
        CancellationToken cancellationToken)
    {
        var user = authenticatedUserContext.User;
        if (user is null || !user.IsActive)
            return new PasswordChangeResult(false, "É necessário estar autenticado para alterar a senha.");

        if (!requireCurrentPassword && !user.MustChangePassword)
            return new PasswordChangeResult(false, "A troca obrigatória de senha não está pendente.");

        if (requireCurrentPassword && string.IsNullOrEmpty(currentPassword))
            return new PasswordChangeResult(false, "Informe a senha atual.");

        if (newPassword != confirmPassword)
            return new PasswordChangeResult(false, "A confirmação da nova senha não confere.");

        if (!passwordPolicy.IsValid(newPassword))
            return new PasswordChangeResult(false, "A nova senha não atende à política de senha.");

        var currentPasswordHash = await userRepository.GetPasswordHashByUsernameAsync(user.Username, cancellationToken);
        if (currentPasswordHash is null)
            return new PasswordChangeResult(false, "Não foi possível alterar a senha.", IsError: true);

        if (requireCurrentPassword && !passwordHasher.Verify(currentPassword!, currentPasswordHash))
            return new PasswordChangeResult(false, "A senha atual está incorreta.");

        if (passwordHasher.Verify(newPassword, currentPasswordHash))
            return new PasswordChangeResult(false, "A nova senha deve ser diferente da senha atual.");

        var newPasswordHash = passwordHasher.Hash(newPassword);
        var updated = await userRepository.UpdatePasswordAsync(user.UserID, newPasswordHash, mustChangePassword: false, cancellationToken: cancellationToken);
        if (!updated)
            return new PasswordChangeResult(false, "Não foi possível alterar a senha.", IsError: true);

        authenticatedUserContext.SignIn(CreateUpdatedUser(user));
        return new PasswordChangeResult(true);
    }

    private static ApplicationUser CreateUpdatedUser(ApplicationUser user) => new()
    {
        UserID = user.UserID,
        Username = user.Username,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        MustChangePassword = false,
        Roles = user.Roles,
        Permissions = user.Permissions
    };
}
