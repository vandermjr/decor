using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class PasswordPolicy : IPasswordPolicy
{
    private const int MinimumLength = 12;
    private const int MaximumLength = 128;

    public bool IsValid(string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length >= MinimumLength &&
        password.Length <= MaximumLength;
}
