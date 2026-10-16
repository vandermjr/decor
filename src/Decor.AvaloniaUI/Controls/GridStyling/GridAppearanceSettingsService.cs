using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Media;

namespace Decor.AvaloniaUI.Controls.GridStyling;

public sealed class GridAppearanceSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] ColorProperties =
    [
        nameof(GridControlThemeColors.GridBackground),
        nameof(GridControlThemeColors.GridLines),
        nameof(GridControlThemeColors.EmptyGridMessageColor),
        nameof(GridControlThemeColors.HeaderBackground),
        nameof(GridControlThemeColors.HeaderText),
        nameof(GridControlThemeColors.CellBackground),
        nameof(GridControlThemeColors.CellText),
        nameof(GridControlThemeColors.ValueMatchBackground),
        nameof(GridControlThemeColors.FocusedCellBackground),
        nameof(GridControlThemeColors.DataType_Int),
        nameof(GridControlThemeColors.DataType_Text),
        nameof(GridControlThemeColors.DataType_DateTime),
        nameof(GridControlThemeColors.DataType_Decimal_Positive),
        nameof(GridControlThemeColors.DataType_Decimal_Negative),
        nameof(GridControlThemeColors.DataType_Boolean),
        nameof(GridControlThemeColors.StockStatusNormal),
        nameof(GridControlThemeColors.StockStatusAttention),
        nameof(GridControlThemeColors.StockStatusNegative),
        nameof(GridControlThemeColors.StockStatusMadeToOrder)
    ];

    private readonly string _settingsPath;
    private GridAppearanceSettings _settings;

    public GridAppearanceSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Decor", "grid-appearance.json"))
    {
    }

    public GridAppearanceSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
        _settings = ReadSettings();
    }

    public event EventHandler? SettingsChanged;

    public double FontSize => _settings.FontSize;
    public double RowHeight => _settings.RowHeight;
    public double MaximumFontSize => GetMaximumFontSize(RowHeight);
    public string FontFamily => _settings.FontFamily;
    public string HeaderFontWeight => _settings.HeaderFontWeight;
    public string CellFontWeight => _settings.CellFontWeight;
    public string DescriptionFontWeight => _settings.DescriptionFontWeight;
    public string HeaderAlignment => _settings.HeaderAlignment;
    public DataGridGridLinesVisibility GridLinesVisibility => _settings.GridLinesVisibility;
    public DataGridHeadersVisibility HeadersVisibility => _settings.HeadersVisibility;

    public static IReadOnlyList<string> AvailableColorProperties => ColorProperties;

    public string GetColor(bool isDarkTheme, string propertyName)
    {
        var palette = isDarkTheme ? _settings.Dark : _settings.Light;
        return palette.GetValueOrDefault(propertyName, GetDefaultColor(isDarkTheme, propertyName));
    }

    public GridControlThemeColors CreateTheme(bool isDarkTheme)
    {
        var theme = GridControlThemeColors.Create(isDarkTheme);
        foreach (var propertyName in ColorProperties)
        {
            var property = typeof(GridControlThemeColors).GetProperty(propertyName)!;
            property.SetValue(theme, new SolidColorBrush(Color.Parse(GetColor(isDarkTheme, propertyName))));
        }
        return theme;
    }

    public void SetColor(bool isDarkTheme, string propertyName, string color)
    {
        if (!ColorProperties.Contains(propertyName, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(propertyName));

        var normalized = Color.Parse(color).ToString();
        var palette = isDarkTheme ? _settings.Dark : _settings.Light;
        if (palette.GetValueOrDefault(propertyName) == normalized)
            return;

        palette[propertyName] = normalized;
        SaveAndNotify();
    }

    public string GetRowAlignment(string dataType) => _settings.RowAlignments.GetValueOrDefault(dataType, "Auto");

    public void SetRowAlignment(string dataType, string alignment)
    {
        if (!RowAlignmentTypes.Contains(dataType, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(dataType));
        if (!AvailableRowAlignments.Contains(alignment, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(alignment));

        _settings.RowAlignments[dataType] = alignment;
        SaveAndNotify();
    }

    public void SetTypography(string fontFamily, double fontSize, double rowHeight, string headerFontWeight, string cellFontWeight,
        string descriptionFontWeight, string headerAlignment, DataGridGridLinesVisibility gridLinesVisibility,
        DataGridHeadersVisibility headersVisibility)
    {
        if (!AvailableFontFamilies.Contains(fontFamily, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(fontFamily));
        if (fontSize is < 9 or > 24)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (rowHeight is < 20 or > 80)
            throw new ArgumentOutOfRangeException(nameof(rowHeight));
        if (!AvailableFontWeights.Contains(headerFontWeight, StringComparer.Ordinal)
            || !AvailableFontWeights.Contains(cellFontWeight, StringComparer.Ordinal)
            || !AvailableFontWeights.Contains(descriptionFontWeight, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(headerFontWeight));
        if (!AvailableAlignments.Contains(headerAlignment, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(headerAlignment));

        _settings = _settings with
        {
            FontFamily = fontFamily,
            FontSize = Math.Min(fontSize, GetMaximumFontSize(rowHeight)),
            RowHeight = rowHeight,
            HeaderFontWeight = headerFontWeight,
            CellFontWeight = cellFontWeight,
            DescriptionFontWeight = descriptionFontWeight,
            HeaderAlignment = headerAlignment,
            GridLinesVisibility = gridLinesVisibility,
            HeadersVisibility = headersVisibility
        };
        SaveAndNotify();
    }

    public static IReadOnlyList<string> AvailableFontWeights { get; } = ["Light", "Normal", "SemiBold", "Bold"];
    public static IReadOnlyList<string> AvailableFontFamilies { get; } = ["Noto Sans", "Segoe UI", "DejaVu Sans", "sans-serif"];
    public static IReadOnlyList<string> AvailableAlignments { get; } = ["Left", "Center", "Right"];
    public static IReadOnlyList<string> AvailableRowAlignments { get; } = ["Auto", .. AvailableAlignments];
    public static IReadOnlyList<string> RowAlignmentTypes { get; } = ["Integer", "Text", "DateTime", "Decimal", "Boolean", "Status"];

    public static double GetMaximumFontSize(double rowHeight) => Math.Clamp(rowHeight - 7, 9, 24);

    public void ResetToDefaults()
    {
        _settings = new GridAppearanceSettings();
        SaveAndNotify();
    }

    private static string GetDefaultColor(bool isDarkTheme, string propertyName)
    {
        var brush = typeof(GridControlThemeColors).GetProperty(propertyName)!.GetValue(GridControlThemeColors.Create(isDarkTheme)) as ISolidColorBrush;
        return (brush?.Color ?? Colors.Black).ToString();
    }

    private GridAppearanceSettings ReadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new GridAppearanceSettings();

            var settings = JsonSerializer.Deserialize<GridAppearanceSettings>(File.ReadAllText(_settingsPath), JsonOptions)
                ?? new GridAppearanceSettings();
            var rowHeight = Math.Clamp(settings.RowHeight, 20, 80);
            return settings with
            {
                RowHeight = rowHeight,
                FontSize = Math.Clamp(settings.FontSize, 9, GetMaximumFontSize(rowHeight))
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new GridAppearanceSettings();
        }
    }

    private void SaveAndNotify()
    {
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("O caminho das configurações não possui diretório.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_settings, JsonOptions));
        File.Move(temporaryPath, _settingsPath, overwrite: true);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed record GridAppearanceSettings
    {
        public Dictionary<string, string> Light { get; init; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Dark { get; init; } = new(StringComparer.Ordinal);
        public string FontFamily { get; init; } = "Noto Sans";
        public double FontSize { get; init; } = 13;
        public double RowHeight { get; init; } = 32;
        public string HeaderFontWeight { get; init; } = "SemiBold";
        public string CellFontWeight { get; init; } = "Normal";
        public string DescriptionFontWeight { get; init; } = "SemiBold";
        public string HeaderAlignment { get; init; } = "Center";
        public Dictionary<string, string> RowAlignments { get; init; } = RowAlignmentTypes.ToDictionary(type => type, _ => "Auto", StringComparer.Ordinal);
        public DataGridGridLinesVisibility GridLinesVisibility { get; init; } = DataGridGridLinesVisibility.All;
        public DataGridHeadersVisibility HeadersVisibility { get; init; } = DataGridHeadersVisibility.Column;
    }
}