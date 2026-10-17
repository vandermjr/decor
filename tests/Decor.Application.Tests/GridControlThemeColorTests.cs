using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Decor.AvaloniaUI.Controls.GridStyling;

namespace Decor.Application.Tests;

public sealed class GridControlThemeColorTests
{
    public static IEnumerable<object[]> PropertyTypes()
    {
        foreach (var propertyType in new[]
        {
            typeof(int), typeof(long), typeof(string), typeof(DateTime), typeof(DateTimeOffset),
            typeof(decimal), typeof(double), typeof(float), typeof(bool), typeof(Guid), typeof(short)
        })
        {
            yield return new object[] { propertyType };
            if (propertyType.IsValueType)
                yield return new object[] { typeof(Nullable<>).MakeGenericType(propertyType) };
        }
    }

    [Theory]
    [MemberData(nameof(PropertyTypes))]
    public void Type_and_nullable_semantics_follow_the_active_palette(Type propertyType)
    {
        foreach (var isDark in new[] { false, true })
        {
            var theme = GridControlThemeColors.Create(isDark);
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var expected = underlyingType == typeof(int) || underlyingType == typeof(long) ? theme.DataType_Int
                : underlyingType == typeof(string) ? theme.DataType_Text
                : underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset) ? theme.DataType_DateTime
                : underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float) ? theme.DataType_Decimal_Positive
                : underlyingType == typeof(bool) ? theme.DataType_Boolean
                : theme.CellText;

            Assert.Same(expected, theme.GetBrushForType(propertyType));
            Assert.Same(theme.CellText, theme.GetBrushForType(null));
        }
    }

    [Theory]
    [InlineData(false, "#0057A8", "#176B35", "#9A4100", "#B32D35", "#30363E")]
    [InlineData(true, "#71B7FF", "#6DDB87", "#FFAF66", "#FF7272", "#F0F0F0")]
    public void Theme_factory_preserves_semantic_colors(bool isDark, string integer, string green,
        string dateTime, string negative, string boolean)
    {
        var theme = GridControlThemeColors.Create(isDark);

        Assert.Equal(Color.Parse(integer), BrushColor(theme.DataType_Int));
        Assert.Equal(Color.Parse(green), BrushColor(theme.DataType_Text));
        Assert.Equal(Color.Parse(dateTime), BrushColor(theme.DataType_DateTime));
        Assert.Equal(Color.Parse(green), BrushColor(theme.DataType_Decimal_Positive));
        Assert.Equal(Color.Parse(negative), BrushColor(theme.DataType_Decimal_Negative));
        Assert.Equal(Color.Parse(boolean), BrushColor(theme.DataType_Boolean));
    }

    [Theory]
    [InlineData(false, "Normal", "#1E5E50")]
    [InlineData(false, "Atenção", "#7A4B00")]
    [InlineData(false, "Negativo", "#9F3027")]
    [InlineData(false, "Sob Demanda", "#0057A8")]
    [InlineData(true, "Normal", "#6DDB87")]
    [InlineData(true, "Repor", "#FFC266")]
    [InlineData(true, "Negativo", "#FF7272")]
    [InlineData(true, "Sob Demanda", "#71B7FF")]
    public void Stock_status_uses_semantic_color_for_each_theme(bool isDark, string status, string expected)
    {
        var theme = GridControlThemeColors.Create(isDark);

        Assert.Equal(Color.Parse(expected), BrushColor(theme.GetStockStatusBrush(status)));
    }

    [Theory]
    [InlineData(typeof(decimal), -0.01, true)]
    [InlineData(typeof(decimal?), -24.5, true)]
    [InlineData(typeof(double), -1.0, true)]
    [InlineData(typeof(float), -0.5, true)]
    [InlineData(typeof(decimal), 0.0, false)]
    [InlineData(typeof(decimal), 0.01, false)]
    public void Numeric_cell_foreground_uses_sign_specific_decimal_color(Type valueType, double value, bool isNegative)
    {
        var theme = GridControlThemeColors.Create(isDarkTheme: false);

        var brush = theme.GetBrushForValue(valueType, valueType == typeof(float) ? (object)(float)value : (decimal)value);

        Assert.Same(isNegative ? theme.DataType_Decimal_Negative : theme.DataType_Decimal_Positive, brush);
    }

    [Fact]
    public void Applying_themes_at_runtime_replaces_the_stylers_type_brushes()
    {
        var styler = new DefaultGridControlStyler();

        foreach (var isDark in new[] { false, true, false })
        {
            var theme = GridControlThemeColors.Create(isDark);
            styler.ApplyTheme(theme);

            Assert.Same(theme, styler.Theme);
            foreach (var property in typeof(ColorRow).GetProperties())
                Assert.Same(theme.GetBrushForType(property.PropertyType), styler.GetColumnForeground(property));
        }
    }

    [Fact]
    public void Light_type_colors_have_normal_text_contrast_on_cells_and_highlights()
    {
        var theme = GridControlThemeColors.Create(false);
        var foregrounds = new[]
        {
            theme.DataType_Int, theme.DataType_Text, theme.DataType_DateTime,
            theme.DataType_Decimal_Positive, theme.DataType_Decimal_Negative, theme.DataType_Boolean,
            theme.StockStatusNormal, theme.StockStatusAttention, theme.StockStatusNegative, theme.StockStatusMadeToOrder
        };
        var backgrounds = new[] { theme.CellBackground, theme.ValueMatchBackground, theme.FocusedCellBackground }
            .Concat(LightSelectionBackgrounds());

        foreach (var foreground in foregrounds)
        foreach (var background in backgrounds)
        {
            var foregroundLuminance = Luminance(BrushColor(foreground));
            var backgroundLuminance = Luminance(BrushColor(background));
            var contrast = (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05)
                / (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05);

            Assert.True(contrast >= 4.5,
                $"{BrushColor(foreground)} on {BrushColor(background)} has contrast {contrast:F2}, below 4.5:1.");
        }
    }

    [Fact]
    public void Light_backgrounds_and_neutral_text_are_unchanged()
    {
        var theme = GridControlThemeColors.Create(false);

        Assert.Equal(Color.Parse("#FFFFFF"), BrushColor(theme.CellBackground));
        Assert.Equal(Color.Parse("#D9EFFA"), BrushColor(theme.ValueMatchBackground));
        Assert.Equal(Color.Parse("#D6EEF8"), BrushColor(theme.FocusedCellBackground));
        Assert.Equal(Color.Parse("#30363E"), BrushColor(theme.CellText));
        Assert.Equal(Color.Parse("#30363E"), BrushColor(theme.DataType_Boolean));
    }

    [Fact]
    public void Grid_appearance_settings_persist_colors_and_restore_defaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decor-grid-appearance-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var service = new GridAppearanceSettingsService(path);
            service.SetColor(false, nameof(GridControlThemeColors.DataType_Text), "#FF123456");
            service.SetTypography("Segoe UI", 15, 40, "Bold", "SemiBold",
                "Normal", "Right", DataGridGridLinesVisibility.Horizontal, DataGridHeadersVisibility.All);
            service.SetRowAlignment("Decimal", "Left");
            service.SetRowAlignment("Text", "Right");

            var restored = new GridAppearanceSettingsService(path);
            Assert.Equal(Color.Parse("#FF123456"), BrushColor(restored.CreateTheme(false).DataType_Text));
            Assert.Equal("Segoe UI", restored.FontFamily);
            Assert.Equal(15, restored.FontSize);
            Assert.Equal(40, restored.RowHeight);
            Assert.Equal(38, restored.HeaderHeight);
            Assert.Equal("Bold", restored.HeaderFontWeight);
            Assert.Equal("SemiBold", restored.CellFontWeight);
            Assert.Equal("Normal", restored.DescriptionFontWeight);
            Assert.Equal("Right", restored.HeaderAlignment);
            Assert.Equal("Left", restored.GetRowAlignment("Decimal"));
            Assert.Equal("Right", restored.GetRowAlignment("Text"));
            Assert.Equal("Auto", restored.GetRowAlignment("Boolean"));
            Assert.Equal(DataGridGridLinesVisibility.Horizontal, restored.GridLinesVisibility);
            Assert.Equal(DataGridHeadersVisibility.All, restored.HeadersVisibility);
            restored.ResetToDefaults();
            var defaults = new GridAppearanceSettingsService(path);
            Assert.Equal(BrushColor(GridControlThemeColors.Create(false).DataType_Text),
                BrushColor(defaults.CreateTheme(false).DataType_Text));
            Assert.Equal(13, defaults.FontSize);
            Assert.Equal("Noto Sans", defaults.FontFamily);
            Assert.Equal(32, defaults.RowHeight);
            Assert.Equal(38, defaults.HeaderHeight);
            Assert.Equal("SemiBold", defaults.HeaderFontWeight);
            Assert.Equal("Normal", defaults.CellFontWeight);
            Assert.Equal("SemiBold", defaults.DescriptionFontWeight);
            Assert.Equal("Center", defaults.HeaderAlignment);
            Assert.All(GridAppearanceSettingsService.RowAlignmentTypes,
                type => Assert.Equal("Auto", defaults.GetRowAlignment(type)));
            Assert.Equal(DataGridGridLinesVisibility.All, defaults.GridLinesVisibility);
            Assert.Equal(DataGridHeadersVisibility.Column, defaults.HeadersVisibility);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Grid_appearance_service_exposes_every_semantic_color()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decor-grid-colors-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new GridAppearanceSettingsService(Path.Combine(directory, "settings.json"));
            foreach (var isDark in new[] { false, true })
            {
                var expected = GridControlThemeColors.Create(isDark);
                var actual = service.CreateTheme(isDark);
                foreach (var propertyName in GridAppearanceSettingsService.AvailableColorProperties)
                {
                    var property = typeof(GridControlThemeColors).GetProperty(propertyName)!;
                    Assert.Equal(BrushColor((IBrush)property.GetValue(expected)!),
                        BrushColor((IBrush)property.GetValue(actual)!));
                }
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static Color BrushColor(IBrush brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static IEnumerable<IBrush> LightSelectionBackgrounds()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var document = XDocument.Load(Path.Combine(directory.FullName, "src", "Decor.AvaloniaUI", "App.axaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var light = Assert.Single(document.Descendants(), element => (string?)element.Attribute(xaml + "Key") == "Light");

        foreach (var resourceKey in new[]
        {
            "DataGridRowSelectedBackgroundBrush", "DataGridRowSelectedHoveredBackgroundBrush",
            "DataGridRowSelectedUnfocusedBackgroundBrush", "DataGridRowSelectedHoveredUnfocusedBackgroundBrush"
        })
        {
            var resource = Assert.Single(light.Elements(), element => (string?)element.Attribute(xaml + "Key") == resourceKey);
            yield return new SolidColorBrush(Color.Parse(resource.Value));
        }
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte component)
        {
            var channel = component / 255.0;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    private sealed class ColorRow
    {
        public int? Code { get; init; }
        public string? Text { get; init; }
        public DateTimeOffset? Date { get; init; }
        public decimal? Amount { get; init; }
        public bool? Enabled { get; init; }
        public Guid? Other { get; init; }
    }
}