using System.Xml.Linq;
using Avalonia.Media;
using Avalonia.Skia;
using Decor.AvaloniaUI.Controls.GridStyling;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class GridAppearanceOptionsTests
{
    private static readonly Lazy<bool> AvaloniaInitialized = new(() =>
    {
        SkiaPlatform.Initialize();
        return true;
    });

    [Fact]
    public void Editor_applies_color_and_typography_changes_and_restores_defaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decor-grid-editor-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new GridAppearanceSettingsService(Path.Combine(directory, "settings.json"));
            var theme = new Mock<IThemeService>();
            theme.SetupGet(service => service.CurrentTheme).Returns(DecorThemeStyle.Light);
            var editor = new GridAppearanceEditorViewModel(settings, theme.Object);

            editor.HeaderFontWeight.Should().Be("SemiBold");
            editor.Colors.Should().HaveCount(19);
            editor.Colors.Single(option => option.PropertyName == nameof(GridControlThemeColors.DataType_Text)).ColorHex = "#FF123456";
            editor.FontFamily = "Segoe UI";
            editor.FontSize = 15.5;
            editor.RowHeight = 42;
            editor.DescriptionFontWeight.Should().Be("SemiBold");
            editor.HeaderAlignment = "Direita";
            editor.RowAlignments.Should().HaveCount(6);
            editor.RowAlignments.Single(option => option.DataType == "Decimal").SelectedAlignment = "Esquerda";

            Assert.Equal(Color.Parse("#FF123456"), Assert.IsAssignableFrom<ISolidColorBrush>(settings.CreateTheme(false).DataType_Text).Color);
            Assert.Equal("Segoe UI", settings.FontFamily);
            Assert.Equal(15.5, settings.FontSize);
            Assert.Equal(42, settings.RowHeight);
            Assert.Equal("Right", settings.HeaderAlignment);
            Assert.Equal("Left", settings.GetRowAlignment("Decimal"));
            Assert.Equal("Auto", settings.GetRowAlignment("Text"));

            editor.SelectedPalette = "Escuro";
            Assert.True(editor.PreviewIsDark);
            editor.Colors.Single(option => option.PropertyName == nameof(GridControlThemeColors.HeaderBackground)).ColorHex = "#FF102030";
            Assert.Equal(Color.Parse("#FF102030"), Assert.IsAssignableFrom<ISolidColorBrush>(settings.CreateTheme(true).HeaderBackground).Color);

            editor.ResetToDefaults();
            Assert.Equal("Noto Sans", settings.FontFamily);
            Assert.Equal(13, settings.FontSize);
            Assert.Equal(32, settings.RowHeight);
            Assert.Equal("SemiBold", settings.HeaderFontWeight);
            Assert.All(editor.RowAlignments, option => Assert.Equal("Automático", option.SelectedAlignment));
            Assert.Equal(Color.Parse("#F0F2F5"), Assert.IsAssignableFrom<ISolidColorBrush>(settings.CreateTheme(false).HeaderBackground).Color);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Font_size_limit_tracks_row_height_and_normalizes_persisted_values()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decor-grid-font-limit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new GridAppearanceSettingsService(Path.Combine(directory, "settings.json"));
            settings.SetTypography("Noto Sans", 24, 20, "SemiBold", "Normal", "SemiBold", "Center",
                Avalonia.Controls.DataGridGridLinesVisibility.All, Avalonia.Controls.DataGridHeadersVisibility.Column);

            Assert.Equal(13, settings.MaximumFontSize);
            Assert.Equal(13, settings.FontSize);
            Assert.Equal(13, new GridAppearanceSettingsService(Path.Combine(directory, "settings.json")).FontSize);

            var theme = new Mock<IThemeService>();
            theme.SetupGet(service => service.CurrentTheme).Returns(DecorThemeStyle.Light);
            var editor = new GridAppearanceEditorViewModel(settings, theme.Object);
            editor.FontSize = 24;
            Assert.Equal(13, editor.FontSize);

            editor.RowHeight = 31;
            Assert.Equal(24, editor.MaximumFontSize);
            editor.FontSize = 24;
            Assert.Equal(24, editor.FontSize);

            editor.RowHeight = 20;
            Assert.Equal(13, editor.FontSize);
            Assert.Equal(13, settings.FontSize);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Invalid_hex_color_is_reported_without_replacing_the_saved_palette()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decor-grid-editor-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new GridAppearanceSettingsService(Path.Combine(directory, "settings.json"));
            var theme = new Mock<IThemeService>();
            theme.SetupGet(service => service.CurrentTheme).Returns(DecorThemeStyle.Dark);
            var editor = new GridAppearanceEditorViewModel(settings, theme.Object);
            var original = settings.GetColor(true, nameof(GridControlThemeColors.DataType_Int));

            editor.Colors.Single(option => option.PropertyName == nameof(GridControlThemeColors.DataType_Int)).ColorHex = "not-a-color";

            Assert.Contains("hexadecimal", editor.Status, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(original, settings.GetColor(true, nameof(GridControlThemeColors.DataType_Int)));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Options_view_contains_grid_preview_and_contextual_reset_action()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);

        var view = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "IconCatalogView.axaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "TreeViewItem"
            && (string?)element.Attribute("Tag") == "GridAppearance"
            && (string?)element.Attribute("Header") == "Grades");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "DecorDataGridControl"
            && (string?)element.Attribute(xaml + "Name") == "GridAppearancePreview"
            && ((string?)element.Attribute("ItemsSource"))?.Contains("GridAppearance.PreviewRows", StringComparison.Ordinal) == true
            && (string?)element.Parent?.Attribute("Grid.Row") == "1"
            && !element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "ScrollViewer"));
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Grid"
            && (string?)element.Attribute("RowDefinitions") == "*,Auto,Auto");
        var appearanceExpanders = view.Descendants().Where(element => element.Name.LocalName == "Expander"
            && new[] { "Aparência geral", "Superfícies e seleção", "Cores semânticas" }
                .Contains((string?)element.Attribute("Header"), StringComparer.Ordinal)).ToArray();
        Assert.Equal(3, appearanceExpanders.Length);
        Assert.All(appearanceExpanders, expander =>
        {
            Assert.Equal("Stretch", (string?)expander.Attribute("HorizontalAlignment"));
            Assert.Equal("Stretch", (string?)expander.Attribute("HorizontalContentAlignment"));
        });
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Grid.Column") == "1"
            && (string?)element.Attribute("Grid.RowSpan") == "3"
            && (string?)element.Attribute("VerticalAlignment") == "Stretch");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Expander"
            && (string?)element.Attribute("Header") == "Aparência geral");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Expander"
            && (string?)element.Attribute("Header") == "Superfícies e seleção");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Expander"
            && (string?)element.Attribute("Header") == "Cores semânticas");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding ResetSelectedSettingsCommand}"
            && (string?)element.Attribute("Content") == "{Binding ResetSelectedSettingsText}");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding GridAppearance.Status}");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Slider"
            && (string?)element.Attribute("TickFrequency") == "0.1"
            && ((string?)element.Attribute("Maximum"))?.Contains("GridAppearance.MaximumFontSize", StringComparison.Ordinal) == true
            && ((string?)element.Attribute("Value"))?.Contains("GridAppearance.FontSize", StringComparison.Ordinal) == true);
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Text"))?.Contains("GridAppearance.FontSize", StringComparison.Ordinal) == true
            && ((string?)element.Attribute("Text"))?.Contains("0.0", StringComparison.Ordinal) == true);
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "ComboBox"
            && ((string?)element.Attribute("ItemsSource"))?.Contains("GridAppearance.HeaderAlignmentOptions", StringComparison.Ordinal) == true
            && ((string?)element.Attribute("SelectedItem"))?.Contains("GridAppearance.HeaderAlignment", StringComparison.Ordinal) == true);
        var rowAlignmentEditor = Assert.Single(view.Descendants(), element => element.Name.LocalName == "ItemsControl"
            && ((string?)element.Attribute("ItemsSource"))?.Contains("GridAppearance.RowAlignments", StringComparison.Ordinal) == true);
        Assert.Contains(rowAlignmentEditor.Descendants(), element => element.Name.LocalName == "ComboBox"
            && ((string?)element.Attribute("ItemsSource"))?.Contains("AlignmentOptions", StringComparison.Ordinal) == true
            && ((string?)element.Attribute("SelectedItem"))?.Contains("SelectedAlignment", StringComparison.Ordinal) == true);
        Assert.Equal("Reposição", typeof(GridAppearancePreviewRow).GetProperty(nameof(GridAppearancePreviewRow.StockStatus))
            ?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>().Single().GetName());
    }

    [Fact]
    public void Selecting_grid_appearance_activates_the_section_and_restores_grid_defaults()
    {
        _ = AvaloniaInitialized.Value;
        var directory = Path.Combine(Path.GetTempPath(), "decor-grid-options-nav-" + Guid.NewGuid().ToString("N"));
        try
        {
            var appearance = new Mock<IIconAppearanceService>();
            appearance.SetupGet(service => service.CurrentAppearance).Returns(IconAppearance.Default);
            var theme = new Mock<IThemeService>();
            theme.SetupGet(service => service.CurrentTheme).Returns(DecorThemeStyle.Light);
            var settings = new GridAppearanceSettingsService(Path.Combine(directory, "settings.json"));
            settings.SetTypography("Segoe UI", 16, 42, "Bold", "Normal", "Normal", "Left",
                Avalonia.Controls.DataGridGridLinesVisibility.Horizontal, Avalonia.Controls.DataGridHeadersVisibility.All);
            settings.SetRowAlignment("Decimal", "Right");
            var viewModel = new IconCatalogViewModel(appearance.Object, theme.Object,
                gridAppearanceSettingsService: settings);

            viewModel.SelectedSettingsSection = "GridAppearance";

            Assert.True(viewModel.IsGridAppearanceSelected);
            Assert.Equal("Restaurar padrão", viewModel.ResetSelectedSettingsText);
            viewModel.ResetSelectedSettingsCommand.Execute(null);
            Assert.Equal("Noto Sans", settings.FontFamily);
            Assert.Equal(13, settings.FontSize);
            Assert.Equal(32, settings.RowHeight);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("GoodsReceiptsView.axaml")]
    [InlineData("ProductsView.axaml")]
    [InlineData("PurchaseOrdersView.axaml")]
    [InlineData("StockAdjustmentsView.axaml")]
    [InlineData("StockTransfersView.axaml")]
    [InlineData("StockView.axaml")]
    public void Grid_view_headers_do_not_override_global_font_weight(string viewName)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);

        var view = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", viewName));
        var headerStyles = view.Descendants().Where(element => element.Name.LocalName == "Style"
            && ((string?)element.Attribute("Selector"))?.Contains("DataGridColumnHeader", StringComparison.Ordinal) == true);
        Assert.All(headerStyles, style => Assert.DoesNotContain(style.Elements().Where(element => element.Name.LocalName == "Setter"),
            setter => (string?)setter.Attribute("Property") == "FontWeight"));
    }
}