using Avalonia.Media;

namespace Decor.AvaloniaUI.Controls.GridStyling;

/// <summary>
/// Defines theme colors for the DecorDataGridControl.
/// Colors are based on data types to provide visual differentiation.
/// </summary>
public class GridControlThemeColors
{
    public static GridControlThemeColors Create(bool isDarkTheme) => isDarkTheme
        ? new GridControlThemeColors
        {
            ValueMatchBackground = new SolidColorBrush(Color.Parse("#5A5310")),
            FocusedCellBackground = new SolidColorBrush(Color.Parse("#1C5C85")),
            DataType_Int = new SolidColorBrush(Color.Parse("#71B7FF")),
            DataType_Text = new SolidColorBrush(Color.Parse("#6DDB87")),
            DataType_DateTime = new SolidColorBrush(Color.Parse("#FFAF66")),
            DataType_Decimal_Positive = new SolidColorBrush(Color.Parse("#6DDB87")),
            DataType_Decimal_Negative = new SolidColorBrush(Color.Parse("#FF7272")),
            DataType_Boolean = new SolidColorBrush(Color.Parse("#F0F0F0"))
        }
        : new GridControlThemeColors();

    /// <summary>
    /// Background color for the grid area.
    /// </summary>
    public IBrush GridBackground { get; set; } = Brushes.White;

    /// <summary>
    /// Color for grid lines (borders).
    /// </summary>
    public IBrush GridLines { get; set; } = Brushes.LightGray;

    /// <summary>
    /// Color for the "no data" message text.
    /// </summary>
    public IBrush EmptyGridMessageColor { get; set; } = Brushes.Gray;

    /// <summary>
    /// Background color for column headers.
    /// </summary>
    public IBrush HeaderBackground { get; set; } = Brushes.LightGray;

    /// <summary>
    /// Text color for column headers.
    /// </summary>
    public IBrush HeaderText { get; set; } = Brushes.Black;

    /// <summary>
    /// Background color for cells when grid is empty.
    /// </summary>
    public IBrush CellBackground { get; set; } = Brushes.White;

    /// <summary>
    /// Default text color for cells.
    /// </summary>
    public IBrush CellText { get; set; } = Brushes.Black;

    /// <summary>
    /// Background for cells with the same value in the active column.
    /// </summary>
    public IBrush ValueMatchBackground { get; set; } = new SolidColorBrush(Color.Parse("#FFFFB4"));

    /// <summary>
    /// Background for the cell used as the value-match reference.
    /// </summary>
    public IBrush FocusedCellBackground { get; set; } = new SolidColorBrush(Color.Parse("#C9DEF5"));

    /// <summary>
    /// Text color for integer/long columns (identifiers, counts).
    /// </summary>
    public IBrush DataType_Int { get; set; } = new SolidColorBrush(Color.Parse("#0066CC")); // Blue

    /// <summary>
    /// Text color for string/text columns.
    /// </summary>
    public IBrush DataType_Text { get; set; } = new SolidColorBrush(Color.Parse("#009900")); // Green

    /// <summary>
    /// Text color for DateTime columns.
    /// </summary>
    public IBrush DataType_DateTime { get; set; } = new SolidColorBrush(Color.Parse("#FF6600")); // Orange

    /// <summary>
    /// Text color for decimal/money columns (positive values).
    /// </summary>
    public IBrush DataType_Decimal_Positive { get; set; } = new SolidColorBrush(Color.Parse("#009900")); // Green

    /// <summary>
    /// Text color for decimal/money columns (negative values).
    /// </summary>
    public IBrush DataType_Decimal_Negative { get; set; } = new SolidColorBrush(Color.Parse("#CC0000")); // Red

    /// <summary>
    /// Text color for boolean columns.
    /// </summary>
    public IBrush DataType_Boolean { get; set; } = Brushes.Black;

    /// <summary>
    /// Gets the appropriate brush color for a given property type.
    /// </summary>
    public IBrush GetBrushForType(Type? propertyType)
    {
        if (propertyType == null)
            return CellText;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return DataType_Int;

        if (underlyingType == typeof(string))
            return DataType_Text;

        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
            return DataType_DateTime;

        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
            return DataType_Decimal_Positive;

        if (underlyingType == typeof(bool))
            return DataType_Boolean;

        return CellText;
    }
}
