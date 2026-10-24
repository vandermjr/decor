using System.IO.Compression;
using System.Xml.Linq;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.Views;
using Decor.Core.DTOs;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;

namespace Decor.Application.Tests;

public sealed class ProductsCatalogPanelTests
{
    [Fact]
    public void Product_catalog_keeps_the_shared_search_and_grid_selection_contract()
    {
        var document = ReadProductsView();
        var search = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField");
        var grid = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorDataGridControl"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ProductsGrid");
        var gridControlPath = Path.Combine(Path.GetDirectoryName(ProductsViewPath())!, "..", "Controls", "DecorDataGridControl.axaml");
        var gridControl = XDocument.Load(gridControlPath);
        var innerGrid = Assert.Single(gridControl.Descendants(), element => element.Name.LocalName == "DataGrid"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "InnerDataGrid");

        Assert.Equal("{Binding SearchText, Mode=TwoWay}", (string?)search.Attribute("Text"));
        Assert.Equal("{Binding SearchCommand}", (string?)search.Attribute("SearchCommand"));
        Assert.Equal("{Binding ClearSearchCommand}", (string?)search.Attribute("ClearCommand"));
        Assert.Contains(search.Ancestors(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "SearchToolsFrame");
        Assert.Equal("{Binding Products}", (string?)grid.Attribute("ItemsSource"));
        Assert.Equal("{Binding SelectedProduct, Mode=TwoWay}", (string?)grid.Attribute("SelectedItem"));
        Assert.Equal("True", (string?)innerGrid.Attribute("CanUserResizeColumns"));
        Assert.Equal("True", (string?)innerGrid.Attribute("CanUserSortColumns"));
    }

    [Theory]
    [InlineData(1280, 3, 2, 0, 1, 2, 0, 1, 1, 3)]
    [InlineData(900, 4, 1, 0, 1, 2, 3, 0, 1, 1)]
    public void Product_catalog_layout_keeps_summary_search_grid_and_details_in_distinct_responsive_cells(
        double width, int rowCount, int columnCount, int summaryRow, int searchRow, int gridRow, int detailsRow, int detailsColumn, int summaryRowSpan, int detailsRowSpan)
    {
        var layout = new Grid { ColumnSpacing = 12 };
        var summary = new Expander();
        var searchField = new Border { Height = 36 };
        var searchFrame = new Border { Padding = new Thickness(10), Child = searchField };
        var productsGrid = new Border();
        var details = new Border();
        layout.Children.Add(summary);
        layout.Children.Add(searchFrame);
        layout.Children.Add(productsGrid);
        layout.Children.Add(details);
        typeof(ProductsView).GetMethod("ConfigureCatalogLayout", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [layout, summary, searchFrame, productsGrid, details, width]);

        Assert.Equal(rowCount, layout.RowDefinitions.Count);
        Assert.Equal(columnCount, layout.ColumnDefinitions.Count);
        Assert.Equal(summaryRow, Grid.GetRow(summary));
        Assert.Equal(0, Grid.GetColumn(summary));
        Assert.Equal(summaryRowSpan, Grid.GetRowSpan(summary));
        Assert.Equal(searchRow, Grid.GetRow(searchFrame));
        Assert.Equal(0, Grid.GetColumn(searchFrame));
        Assert.Equal(gridRow, Grid.GetRow(productsGrid));
        Assert.Equal(0, Grid.GetColumn(productsGrid));
        Assert.Equal(detailsRow, Grid.GetRow(details));
        Assert.Equal(detailsColumn, Grid.GetColumn(details));
        Assert.Equal(detailsRowSpan, Grid.GetRowSpan(details));
        Assert.Equal(0, Grid.GetColumn(productsGrid));
        Assert.Equal(0, Grid.GetColumn(searchFrame));
        Assert.Same(layout, searchFrame.Parent);
        layout.Measure(new Size(width, 800));
        layout.Arrange(new Rect(0, 0, width, 800));

        Assert.True(searchFrame.Bounds.Height > 0);
        Assert.True(searchField.Bounds.Height > 0);
        Assert.True(productsGrid.Bounds.Height > 0);
        Assert.True(details.Bounds.Height > 0);
        Assert.True(searchFrame.Bounds.Bottom <= productsGrid.Bounds.Top);
        if (width >= 1120)
            Assert.Equal(12, details.Bounds.Left - productsGrid.Bounds.Right);
    }

    [Fact]
    public void Product_details_have_only_the_three_consultation_tabs_and_existing_edit_command()
    {
        var document = ReadProductsView();
        var tabs = document.Descendants().Where(element => element.Name.LocalName == "TabItem")
            .Select(element => (string?)element.Attribute("Header")).ToArray();
        var details = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ProductDetailsPanel");

        Assert.Equal(new[] { "Resumo", "Especificações", "Fornecedores", "Classificação" }, tabs);
        Assert.Contains(details.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding SelectedProduct.ProductName}");
        Assert.Contains(details.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding SelectedSalePriceDisplay}");
        Assert.Contains(details.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding SelectedProductCategoryDisplay}"
            && (string?)element.Attribute("FontSize") == "{DynamicResource DecorFontSizeBase}");
        Assert.All(details.Descendants().Where(element => element.Name.LocalName == "TabItem"), tab =>
            Assert.Equal("{DynamicResource DecorFontSizeBase}", (string?)tab.Attribute("FontSize")));
        Assert.Contains(details.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding EditCommand}");

        var markup = File.ReadAllText(ProductsViewPath());
        Assert.DoesNotContain("Ajustar estoque", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Movimentações", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Histórico de estoque", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Product_listing_exposes_export_and_column_visibility_actions()
    {
        var document = ReadProductsView();
        var newProductButton = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding NewCommand}");
        var heading = Assert.Single(newProductButton.Ancestors(), element => element.Name.LocalName == "Grid"
            && element.Elements().Any(child => child.Name.LocalName == "StackPanel"
                && child.Descendants().Any(text => (string?)text.Attribute("Text") == "Catálogo de produtos")));
        Assert.Contains(heading, newProductButton.Ancestors());
        Assert.DoesNotContain(newProductButton.Ancestors(), element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "SearchToolsFrame");

        var summary = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Grid"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CatalogSummaryLayout");
        Assert.Equal("{Binding !IsEditing}", (string?)summary.Ancestors().First(element => element.Name.LocalName == "Grid"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CatalogLayout").Attribute("IsVisible"));
        var summaryExpander = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Expander"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CatalogSummaryExpander");
        Assert.Equal("Indicadores", (string?)summaryExpander.Attribute("Header"));
        Assert.Equal("Stretch", (string?)summaryExpander.Attribute("HorizontalAlignment"));
        Assert.Equal("False", (string?)summaryExpander.Attribute("IsExpanded"));
        Assert.Equal("CatalogSummaryExpander_Expanded", (string?)summaryExpander.Attribute("Expanded"));
        Assert.Contains(summaryExpander.Descendants(), element => ReferenceEquals(element, summary));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Flyout"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CatalogIndicatorsFlyout");
        foreach (var indicator in new[]
                 {
                     "ShowTotalProductsIndicator", "ShowActiveProductsIndicator", "ShowInactiveProductsIndicator",
                     "ShowOutOfStockProductsIndicator", "ShowLowStockProductsIndicator"
                 })
            Assert.Contains(document.Descendants(), element => element.Name.LocalName == "CheckBox"
                && (string?)element.Attribute("IsChecked") == $"{{Binding {indicator}, Mode=TwoWay}}");
        Assert.Equal(4, document.Descendants().Count(element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Click") == "CatalogIndicatorCard_Click"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Click") == "ExportProductsCsvClicked");
        foreach (var formatClick in new[]
                 {
                     "ExportProductsCsvClicked", "ExportProductsXlsClicked",
                     "ExportProductsXlsxClicked", "ExportProductsOdtClicked"
                 })
            Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Click") == formatClick);
        var viewMarkup = File.ReadAllText(ProductsViewPath());
        Assert.Contains("viewModel.Listing.AllItems", File.ReadAllText(Path.ChangeExtension(ProductsViewPath(), ".axaml.cs")));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Click") == "ConfigureColumnsClicked");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "Ordenar colunas");
        var columnMenuCodeBehind = File.ReadAllText(Path.ChangeExtension(ProductsViewPath(), ".axaml.cs"));
        Assert.Contains("MoveColumnAsync", columnMenuCodeBehind, StringComparison.Ordinal);
        Assert.Contains("Mover coluna para cima", columnMenuCodeBehind, StringComparison.Ordinal);
        Assert.Contains("Mover coluna para baixo", columnMenuCodeBehind, StringComparison.Ordinal);
        Assert.Contains("DecorUserSettings.ProductGridColumnOrder", columnMenuCodeBehind, StringComparison.Ordinal);
        var searchFrame = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "SearchToolsFrame");
        Assert.Equal("{DynamicResource DecorSurfaceBrush}", (string?)searchFrame.Attribute("Background"));
        Assert.Equal("{DynamicResource DecorBorderBrush}", (string?)searchFrame.Attribute("BorderBrush"));
        Assert.Equal("1", (string?)searchFrame.Attribute("BorderThickness"));
        var searchField = Assert.Single(searchFrame.Descendants(), element => element.Name.LocalName == "DecorSearchField");
        Assert.Equal("False", (string?)searchField.Attribute("ShowFilterButton"));
        Assert.Null(searchField.Attribute("FieldBorderThickness"));
        Assert.Contains(searchFrame.Descendants(), element => element.Name.LocalName == "Border"
            && ((string?)element.Attribute("Classes"))?.Split(' ').Contains("catalog-filter-chip") == true
            && (string?)element.Attribute("IsVisible") == "{Binding HasClassificationFilter}");
        var filterButton = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ClassificationFilterButton");
        Assert.Equal("{StaticResource ClassificationFilterFlyout}", (string?)filterButton.Attribute("Flyout"));
        Assert.Contains(filterButton.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "Filtrar");
        foreach (var button in new[]
                 {
                     filterButton,
                     Assert.Single(document.Descendants(), element => element.Name.LocalName == "Button"
                         && element.Descendants().Any(child => child.Name.LocalName == "TextBlock" && (string?)child.Attribute("Text") == "Exportar")),
                     Assert.Single(document.Descendants(), element => element.Name.LocalName == "Button"
                         && element.Descendants().Any(child => child.Name.LocalName == "TextBlock" && (string?)child.Attribute("Text") == "Ordenar colunas"))
                 })
            Assert.Contains(button.Ancestors(), ancestor => ReferenceEquals(ancestor, searchFrame));
        Assert.Contains(searchFrame.Descendants(), element => element.Name.LocalName == "Border"
            && ((string?)element.Attribute("Classes"))?.Split(' ').Contains("catalog-filter-chip") == true);
        var filterFlyout = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Flyout"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ClassificationFilterFlyout");
        Assert.Contains(filterFlyout.Descendants(), element => element.Name.LocalName == "TreeView"
            && (string?)element.Attribute("ItemsSource") == "{Binding FilteredClassificationTreeNodes}");
        Assert.Contains(filterFlyout.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Content") == "OK"
            && (string?)element.Attribute("Click") == "ClassificationFilterConfirm_Click");
        Assert.DoesNotContain(filterFlyout.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding IncludeEditorClassificationCommand}");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TreeView"
            && (string?)element.Attribute("ItemsSource") == "{Binding FilteredEditorClassificationTreeNodes}");
        Assert.Contains("controls:DecorRequiredField.IsRequired=\"True\"", viewMarkup, StringComparison.Ordinal);
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding IncludeEditorClassificationCommand}");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Grid"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CatalogLayout");
        var catalog = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Grid"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CatalogLayout");
        Assert.Equal("12", (string?)catalog.Attribute("ColumnSpacing"));
        Assert.Equal("12", (string?)catalog.Attribute("RowSpacing"));
        Assert.Equal("0", (string?)summaryExpander.Attribute("Grid.Row"));
        Assert.Equal("0", (string?)summaryExpander.Attribute("Grid.Column"));
        Assert.Equal("1", (string?)Assert.Single(catalog.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "SearchToolsFrame").Attribute("Grid.Row"));
        Assert.Equal("2", (string?)Assert.Single(catalog.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ProductsGridFrame").Attribute("Grid.Row"));
        var columns = catalog.Elements().Single(element => element.Name.LocalName == "Grid.ColumnDefinitions").Elements().ToArray();
        Assert.Equal("2*", (string?)columns[0].Attribute("Width"));
        Assert.Equal("420", (string?)columns[1].Attribute("Width"));
        var details = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ProductDetailsPanel");
        Assert.Equal("380", (string?)details.Attribute("MinWidth"));
        Assert.Equal("460", (string?)details.Attribute("MaxWidth"));
        Assert.Equal("Stretch", (string?)details.Attribute("HorizontalAlignment"));
        Assert.Equal("3", (string?)details.Attribute("Grid.RowSpan"));
        foreach (var buttonText in new[] { "Novo produto", "Editar produto" })
            Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == buttonText
                && (string?)element.Attribute("FontWeight") == "SemiBold");
    }

    [Fact]
    public void Stock_level_gradient_uses_shared_light_and_dark_theme_resources()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);

        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var app = XDocument.Load(Path.Combine(root!.FullName, "src", "Decor.AvaloniaUI", "App.axaml"));
        foreach (var themeName in new[] { "Light", "Dark" })
        {
            var theme = Assert.Single(app.Descendants(), element => element.Name.LocalName == "ResourceDictionary"
                && (string?)element.Attribute(xaml + "Key") == themeName);
            Assert.Contains(theme.Elements(), element => element.Name.LocalName == "LinearGradientBrush"
                && (string?)element.Attribute(xaml + "Key") == "DecorStockLevelBrush");
        }

        var view = ReadProductsView();
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Value") == "{DynamicResource DecorStockLevelBrush}");
    }

    [Fact]
    public async Task Product_csv_export_quotes_fields_and_formats_values_for_brazilian_locale()
    {
        await using var output = new MemoryStream();
        var columns = new (string Header, PropertyInfo Property)[]
        {
            ("Produto", typeof(ProductDTO).GetProperty(nameof(ProductDTO.ProductName))!),
            ("Preço de venda", typeof(ProductDTO).GetProperty(nameof(ProductDTO.SalePrice))!)
        };
        var product = ProductStockGridTests.CreateProduct(3, 0, 1) with
        {
            ProductName = "Vaso; azul \"alto\"",
            SalePrice = 1234.56m
        };

        await ProductExporter.WriteAsync(output, [product], columns, ProductExportFormat.Csv);
        var csv = Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');

        Assert.Contains("\"Produto\";\"Preço de venda\"", csv);
        Assert.Contains("\"Vaso; azul \"\"alto\"\"\";\"1234,56\"", csv);
    }

    [Theory]
    [InlineData(ProductExportFormat.Csv)]
    [InlineData(ProductExportFormat.Xls)]
    [InlineData(ProductExportFormat.Xlsx)]
    [InlineData(ProductExportFormat.Odt)]
    public async Task Product_export_formats_contain_all_query_results_beyond_grid_page(ProductExportFormat format)
    {
        var columns = new (string Header, PropertyInfo Property)[]
        {
            ("ID", typeof(ProductDTO).GetProperty(nameof(ProductDTO.ProductID))!),
            ("Produto", typeof(ProductDTO).GetProperty(nameof(ProductDTO.ProductName))!),
            ("Preço", typeof(ProductDTO).GetProperty(nameof(ProductDTO.SalePrice))!)
        };
        var products = Enumerable.Range(1, 27)
            .Select(index => ProductStockGridTests.CreateProduct(index, 0, 1) with
            {
                ProductName = $"Produto {index}",
                SalePrice = index + 0.5m
            })
            .ToArray();
        await using var output = new MemoryStream();

        await ProductExporter.WriteAsync(output, products, columns, format);

        output.Position = 0;
        switch (format)
        {
            case ProductExportFormat.Csv:
            {
                using var reader = new StreamReader(output, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var lines = (await reader.ReadToEndAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(products.Length + 1, lines.Length);
                Assert.Contains("Produto 27", lines[^1]);
                break;
            }
            case ProductExportFormat.Xls:
            {
                var workbook = new HSSFWorkbook(output);
                var sheet = workbook.GetSheetAt(0);
                Assert.Equal(products.Length + 1, sheet.LastRowNum + 1);
                Assert.Equal("Produto 27", sheet.GetRow(products.Length).GetCell(1).StringCellValue);
                Assert.Equal(27.5, sheet.GetRow(products.Length).GetCell(2).NumericCellValue);
                workbook.Close();
                break;
            }
            case ProductExportFormat.Xlsx:
            {
                var workbook = new XSSFWorkbook(output);
                var sheet = workbook.GetSheetAt(0);
                Assert.Equal(products.Length + 1, sheet.LastRowNum + 1);
                Assert.Equal("Produto 27", sheet.GetRow(products.Length).GetCell(1).StringCellValue);
                Assert.Equal(27.5, sheet.GetRow(products.Length).GetCell(2).NumericCellValue);
                workbook.Close();
                break;
            }
            case ProductExportFormat.Odt:
            {
                using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
                Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", await ReadEntryAsync(archive, "mimetype"));
                var content = XDocument.Parse(await ReadEntryAsync(archive, "content.xml"));
                var rows = content.Descendants().Where(element => element.Name.LocalName == "table-row").ToArray();
                Assert.Equal(products.Length + 1, rows.Length);
                Assert.Contains("Produto 27", rows[^1].Value, StringComparison.Ordinal);
                var numericCell = rows[^1].Elements().ElementAt(2);
                Assert.Equal("float", (string?)numericCell.Attribute(XName.Get("value-type", "urn:oasis:names:tc:opendocument:xmlns:office:1.0")));
                Assert.Equal("27.5", (string?)numericCell.Attribute(XName.Get("value", "urn:oasis:names:tc:opendocument:xmlns:office:1.0")));
                Assert.Contains(archive.Entries, entry => entry.FullName == "META-INF/manifest.xml");
                break;
            }
        }
    }

    private static async Task<string> ReadEntryAsync(ZipArchive archive, string name)
    {
        await using var stream = archive.GetEntry(name)!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static XDocument ReadProductsView() => XDocument.Load(ProductsViewPath());

    private static string ProductsViewPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Decor.sln was not found."),
            "src", "Decor.AvaloniaUI", "Views", "ProductsView.axaml");
    }
}
