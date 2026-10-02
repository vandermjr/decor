using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class AuthenticationService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAuthenticatedUserContext authenticatedUserContext) : IAuthenticationService
{
    public async Task<AuthenticationResult> AuthenticateAsync(int userId, string password, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || string.IsNullOrEmpty(password))
            return new AuthenticationResult(false, ErrorMessage: "Informe o ID do usuário e a senha.");

        var passwordHash = await userRepository.GetPasswordHashByUserIdAsync(userId, cancellationToken);
        if (passwordHash is null || !passwordHasher.Verify(password, passwordHash))
            return new AuthenticationResult(false, ErrorMessage: "Usuário ou senha inválidos.");

        var user = await userRepository.GetByUserIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            return new AuthenticationResult(false, ErrorMessage: "Usuário ou senha inválidos.");

        authenticatedUserContext.SignIn(user);
        return new AuthenticationResult(true, user);
    }
}
