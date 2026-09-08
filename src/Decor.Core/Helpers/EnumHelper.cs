namespace Decor.Core.Helpers;

public static class EnumHelper
{
    public static T? StringToEnum<T>(string? value, bool ignoreCase = true) where T : struct, Enum
    {
        if (string.IsNullOrEmpty(value)) return null;

        if (Enum.TryParse<T>(value, ignoreCase, out T result)) return result;

        return null;
    }

    public static string EnumToString<T>(T enumValue) where T : struct, Enum
    {
        return enumValue.ToString();
    }
}