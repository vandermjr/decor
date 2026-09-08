using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class AuthenticationService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAuthenticatedUserContext authenticatedUserContext) : IAuthenticationService
{
    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return new AuthenticationResult(false, ErrorMessage: "Informe o usuário e a senha.");

        var normalizedUsername = username.Trim();
        var passwordHash = await userRepository.GetPasswordHashByUsernameAsync(normalizedUsername, cancellationToken);
        if (passwordHash is null || !passwordHasher.Verify(password, passwordHash))
            return new AuthenticationResult(false, ErrorMessage: "Usuário ou senha inválidos.");

        var user = await userRepository.GetByUsernameAsync(normalizedUsername, cancellationToken);
        if (user is null || !user.IsActive)
            return new AuthenticationResult(false, ErrorMessage: "Usuário ou senha inválidos.");

        authenticatedUserContext.SignIn(user);
        return new AuthenticationResult(true, user);
    }
}
