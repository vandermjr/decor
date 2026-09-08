using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

internal sealed class FakeUserRepository : IUserRepository
{
    public string? PasswordHash { get; set; }
    public ApplicationUser? User { get; set; }
    public int PasswordHashRequests { get; private set; }
    public int UserRequests { get; private set; }
    public string? RequestedPasswordHashUsername { get; private set; }
    public string? RequestedUserUsername { get; private set; }
    public int UpdatePasswordCalls { get; private set; }
    public int? UpdatedUserId { get; private set; }
    public string? UpdatedPasswordHash { get; private set; }
    public bool? UpdatedMustChangePassword { get; private set; }
    public bool UpdatePasswordResult { get; set; } = true;

    public Task<string?> GetPasswordHashByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        PasswordHashRequests++;
        RequestedPasswordHashUsername = username;
        return Task.FromResult(PasswordHash);
    }

    public Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        UserRequests++;
        RequestedUserUsername = username;
        return Task.FromResult(User);
    }

    public Task<bool> UpdatePasswordAsync(int userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default)
    {
        UpdatePasswordCalls++;
        UpdatedUserId = userId;
        UpdatedPasswordHash = passwordHash;
        UpdatedMustChangePassword = mustChangePassword;
        return Task.FromResult(UpdatePasswordResult);
    }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public bool VerificationResult { get; set; }
    public int VerifyCalls { get; private set; }

    public string Hash(string password) => password;

    public bool Verify(string password, string passwordHash)
    {
        VerifyCalls++;
        return VerificationResult;
    }
}

internal sealed class RecordingAuthenticatedUserContext : IAuthenticatedUserContext
{
    public event EventHandler? SignedOut;
    public bool IsAuthenticated => User is not null;
    public ApplicationUser? User { get; private set; }
    public int SignInCalls { get; private set; }

    public void SignIn(ApplicationUser user)
    {
        SignInCalls++;
        User = user;
    }

    public void SignOut() { User = null; SignedOut?.Invoke(this, EventArgs.Empty); }
}
