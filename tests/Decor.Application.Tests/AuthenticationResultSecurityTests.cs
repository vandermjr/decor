using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class AuthenticationResultSecurityTests
{
    [Fact]
    public void AuthenticationResult_DoesNotExposePasswordHash()
    {
        typeof(AuthenticationResult)
            .GetProperties()
            .Should()
            .NotContain(property => property.Name == "PasswordHash");
    }
}
