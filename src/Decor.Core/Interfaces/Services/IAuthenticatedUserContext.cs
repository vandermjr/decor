using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Services;

public interface IAuthenticatedUserContext
{
    event EventHandler? SignedOut;
    bool IsAuthenticated { get; }
    ApplicationUser? User { get; }
    void SignIn(ApplicationUser user);
    void SignOut();
}
