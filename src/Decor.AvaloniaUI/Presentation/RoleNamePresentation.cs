using System.Globalization;
using Avalonia.Data.Converters;
using Decor.Core.Common;

namespace Decor.AvaloniaUI.Presentation;

public static class RoleNamePresentation
{
    public static string GetGroupDisplayName(string roleName)
    {
        if (string.Equals(roleName, SystemRoleDefaults.Administrator, StringComparison.OrdinalIgnoreCase))
            return "Administradores";

        if (string.Equals(roleName, SystemRoleDefaults.Supervisor, StringComparison.OrdinalIgnoreCase))
            return "Supervisores";

        return roleName;
    }
}

public sealed class RoleNamePresentationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string roleName ? RoleNamePresentation.GetGroupDisplayName(roleName) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}