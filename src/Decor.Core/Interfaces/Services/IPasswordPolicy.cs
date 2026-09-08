namespace Decor.Core.Interfaces.Services;

public interface IPasswordPolicy
{
    bool IsValid(string? password);
}
