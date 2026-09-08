namespace Decor.Core.Interfaces.Services;

public interface IPasswordChangeService
{
    Task<PasswordChangeResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);

    Task<PasswordChangeResult> ChangeRequiredPasswordAsync(
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);
}

public sealed record PasswordChangeResult(bool Succeeded, string? ErrorMessage = null, bool IsError = false);
