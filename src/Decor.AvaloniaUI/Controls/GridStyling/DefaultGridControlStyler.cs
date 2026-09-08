using System.Reflection;
using Avalonia.Media;

namespace Decor.AvaloniaUI.Controls.GridStyling;

/// <summary>
/// Default implementation of grid control styling based on data types.
/// Applies consistent visual differentiation for different column types.
/// </summary>
public class DefaultGridControlStyler : IGridControlStyler
{
    private GridControlThemeColors _theme = new();

    public GridControlThemeColors Theme => _theme;

    public void ApplyTheme(GridControlThemeColors theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    /// <summary>
    /// Gets the foreground brush for a column based on its property type.
    /// Integer/Long = Blue, String = Green, DateTime = Orange, etc.
    /// </summary>
    public IBrush GetColumnForeground(PropertyInfo propertyInfo)
    {
        if (propertyInfo == null)
            return _theme.CellText;

        return _theme.GetBrushForType(propertyInfo.PropertyType);
    }

    /// <summary>
    /// Gets text alignment for a column based on its property type.
    /// Numbers are right-aligned, others are left-aligned.
    /// </summary>
    public TextAlignment GetColumnAlignment(PropertyInfo propertyInfo)
    {
        if (propertyInfo == null)
            return TextAlignment.Left;

        var underlyingType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

        // Right-align numbers and dates
        if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
            underlyingType == typeof(decimal) || underlyingType == typeof(double) ||
            underlyingType == typeof(float) || underlyingType == typeof(DateTime) ||
            underlyingType == typeof(DateTimeOffset))
        {
            return TextAlignment.Right;
        }

        // Center-align booleans (though they're usually checkboxes)
        if (underlyingType == typeof(bool))
        {
            return TextAlignment.Center;
        }

        // Left-align text and everything else
        return TextAlignment.Left;
    }
}
