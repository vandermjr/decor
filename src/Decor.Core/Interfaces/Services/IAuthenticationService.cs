using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Services;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(int userId, string password, CancellationToken cancellationToken = default);
}

public sealed record AuthenticationResult(bool Succeeded, ApplicationUser? User = null, string? ErrorMessage = null, bool IsError = false);
