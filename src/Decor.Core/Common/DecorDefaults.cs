using System.Globalization;

namespace Decor.Core.Common;
public static class DecorDefaults
{
    public const DecorThemeStyle Theme = DecorThemeStyle.Dark;
    public static readonly CultureInfo DefaultLanguage = new("pt-BR");

    public static class PropertyCategory
    {
        public const string Appearance = "Appearance";
        public const string Behavior = "Behavior";
        public const string Data = "Data";
    }
}
