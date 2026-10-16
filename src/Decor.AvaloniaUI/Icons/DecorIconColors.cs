using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Decor.Core.Configuration;

namespace Decor.AvaloniaUI.Icons;

public static class DecorIconColors
{
    public static IBrush GetBrush(DecorIconId id, IconAppearance appearance, ThemeVariant? themeVariant = null)
    {
        var color = appearance.GetColor(id.Value);
        if (color is not null && Color.TryParse(color, out var overrideColor))
            return new SolidColorBrush(overrideColor);

        return new SolidColorBrush(GetDefaultColor(id, themeVariant));
    }

    public static Color GetDefaultColor(DecorIconId id, ThemeVariant? themeVariant = null)
    {
        var isDark = (themeVariant ?? global::Avalonia.Application.Current?.RequestedThemeVariant) == ThemeVariant.Dark;
        var resourceKey = id.Value switch
        {
            "Actions.Create" or "Actions.Cart" => "DecorAddIndicatorBrush",
            "Actions.Edit" => "DecorEditIndicatorBrush",
            "Actions.Delete" or "Actions.Cancel" or "Actions.Close" => "DecorDangerBrush",
            "Actions.Save" => "DecorAccentBrush",
            "User.Profile" or "User.Preferences" or "User.ChangePassword" or "User.SignOut"
                or "Common.Calendar" or "Common.Clock" or "Common.Database"
                or "Navigation.FirstPage" or "Navigation.PreviousPage" or "Navigation.NextPage" or "Navigation.LastPage"
                or "Navigation.Dropdown" or "Navigation.SortAscending" or "Navigation.SortDescending" => "DecorTextMutedBrush",
            _ => null
        };

        return resourceKey switch
        {
            "DecorAddIndicatorBrush" => Color.Parse(isDark ? "#55B96B" : "#218739"),
            "DecorEditIndicatorBrush" => Color.Parse(isDark ? "#F3C969" : "#D99A00"),
            "DecorDangerBrush" => Color.Parse(isDark ? "#E16B73" : "#B32D35"),
            "DecorAccentBrush" => Color.Parse(isDark ? "#3D94D1" : "#2074B8"),
            "DecorTextMutedBrush" => Color.Parse(isDark ? "#AAB4C0" : "#68717D"),
            _ => isDark ? Colors.White : Colors.Black
        };
    }
}