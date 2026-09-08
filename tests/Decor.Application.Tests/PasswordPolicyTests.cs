using Decor.Application.Services;

namespace Decor.Application.Tests;

public sealed class PasswordPolicyTests
{
    private readonly PasswordPolicy _policy = new();

    [Theory]
    [InlineData("123456789012", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("12345678901", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("", false)]
    [InlineData("            ", false)]
    [InlineData("valid password", true)]
    [InlineData("alllowercasepassword", true)]
    [InlineData("ALLUPPERCASEPASSWORD", true)]
    [InlineData("NoDigitsOrSymbols", true)]
    [InlineData("123456789012345", true)]
    public void IsValid_AppliesCurrentPasswordPolicy(string password, bool expected)
    {
        _policy.IsValid(password).Should().Be(expected);
    }
}
