using System.Xml.Linq;
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
            theme.DataType_Decimal_Positive, theme.DataType_Decimal_Negative, theme.DataType_Boolean
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
        Assert.Equal(Color.Parse("#FFFFB4"), BrushColor(theme.ValueMatchBackground));
        Assert.Equal(Color.Parse("#C9DEF5"), BrushColor(theme.FocusedCellBackground));
        Assert.Equal(Color.Parse("#30363E"), BrushColor(theme.CellText));
        Assert.Equal(Color.Parse("#30363E"), BrushColor(theme.DataType_Boolean));
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