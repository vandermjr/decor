using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media;

namespace Decor.AvaloniaUI.Controls.GridStyling;

/// <summary>
/// Defines a contract for classes that apply styling to grid columns based on data types.
/// </summary>
public interface IGridControlStyler
{
    /// <summary>
    /// Applies theme styling to the grid.
    /// </summary>
    void ApplyTheme(GridControlThemeColors theme);

    /// <summary>
    /// Gets the appropriate foreground brush for a column based on its property type.
    /// </summary>
    IBrush GetColumnForeground(PropertyInfo propertyInfo);

    /// <summary>
    /// Gets the horizontal alignment for a column based on its property type.
    /// </summary>
    TextAlignment GetColumnAlignment(PropertyInfo propertyInfo);

    /// <summary>
    /// Gets the theme colors being used.
    /// </summary>
    GridControlThemeColors Theme { get; }
}
