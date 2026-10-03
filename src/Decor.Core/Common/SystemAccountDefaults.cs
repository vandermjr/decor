namespace Decor.Core.Common;

public static class SystemAccountDefaults
{
    public const string AdministratorUsername = "admin";
    public const string AdministratorName = "Administrador";

    public static bool IsAdministrator(string? username) =>
        string.Equals(username, AdministratorUsername, StringComparison.OrdinalIgnoreCase);
}