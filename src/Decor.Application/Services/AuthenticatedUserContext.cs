using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class AuthenticatedUserContext : IAuthenticatedUserContext
{
    public event EventHandler? SignedOut;
    public bool IsAuthenticated => User is not null;
    public ApplicationUser? User { get; private set; }

    public void SignIn(ApplicationUser user) => User = user ?? throw new ArgumentNullException(nameof(user));
    public void SignOut()
    {
        if (User is null) return;
        User = null;
        SignedOut?.Invoke(this, EventArgs.Empty);
    }
}
