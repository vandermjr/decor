using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

internal sealed class FakeUserRepository : IUserRepository
{
    public string? PasswordHash { get; set; }
    public ApplicationUser? User { get; set; }
    public Dictionary<int, string> PasswordHashes { get; } = [];
    public Dictionary<int, ApplicationUser> Users { get; } = [];
    public int PasswordHashRequests { get; private set; }
    public int UserRequests { get; private set; }
    public int? RequestedPasswordHashUserId { get; private set; }
    public int? RequestedUserId { get; private set; }
    public int UpdatePasswordCalls { get; private set; }
    public int? UpdatedUserId { get; private set; }
    public string? UpdatedPasswordHash { get; private set; }
    public bool? UpdatedMustChangePassword { get; private set; }
    public bool UpdatePasswordResult { get; set; } = true;

    public Task<string?> GetPasswordHashByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        PasswordHashRequests++;
        RequestedPasswordHashUserId = userId;
        return Task.FromResult(PasswordHashes.TryGetValue(userId, out var hash) ? hash : PasswordHash);
    }

    public Task<ApplicationUser?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        UserRequests++;
        RequestedUserId = userId;
        return Task.FromResult(Users.TryGetValue(userId, out var user) ? user : User?.UserID == userId ? User : null);
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
