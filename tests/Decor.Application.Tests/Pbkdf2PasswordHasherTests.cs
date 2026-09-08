using Decor.Application.Services;

namespace Decor.Application.Tests;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_UsesExpectedPbkdf2Sha256FormatAndSizes()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        var parts = hash.Split('$');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("PBKDF2-SHA256");
        parts[1].Should().Be("210000");
        Convert.FromBase64String(parts[2]).Should().HaveCount(16);
        Convert.FromBase64String(parts[3]).Should().HaveCount(32);
    }

    [Fact]
    public void Verify_ReturnsTrueForPasswordUsedToCreateHash()
    {
        var password = "correct horse battery staple";

        _hasher.Verify(password, _hasher.Hash(password)).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalseForDifferentPassword()
    {
        var hash = _hasher.Hash("correct password");

        _hasher.Verify("incorrect password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_GeneratesDifferentSaltsForSuccessiveHashes()
    {
        var firstHash = _hasher.Hash("same password");
        var secondHash = _hasher.Hash("same password");

        firstHash.Should().NotBe(secondHash);
        firstHash.Split('$')[2].Should().NotBe(secondHash.Split('$')[2]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("PBKDF2-SHA1$210000$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$invalid$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$210000$not-base64$also-not-base64")]
    public void Verify_ReturnsFalseForInvalidOrCorruptedHash(string passwordHash)
    {
        _hasher.Verify("password", passwordHash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalseForNullHash()
    {
        _hasher.Verify("password", null!).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_RejectsNullEmptyOrWhitespacePassword(string? password)
    {
        Action act = () => _hasher.Hash(password!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Verify_ReturnsFalseForEmptyPassword()
    {
        _hasher.Verify(string.Empty, _hasher.Hash("password")).Should().BeFalse();
    }
}
