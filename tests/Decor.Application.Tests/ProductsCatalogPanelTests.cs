using System.Xml.Linq;
using System.Reflection;
using System.Text;
using Decor.AvaloniaUI.Services;
using Decor.Core.DTOs;

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
        Assert.Equal("{Binding Products}", (string?)grid.Attribute("ItemsSource"));
        Assert.Equal("{Binding SelectedProduct, Mode=TwoWay}", (string?)grid.Attribute("SelectedItem"));
        Assert.Equal("True", (string?)innerGrid.Attribute("CanUserResizeColumns"));
        Assert.Equal("True", (string?)innerGrid.Attribute("CanUserSortColumns"));
    }

    [Fact]
    public void Product_details_have_only_the_three_consultation_tabs_and_existing_edit_command()
    {
        var document = ReadProductsView();
        var tabs = document.Descendants().Where(element => element.Name.LocalName == "TabItem")
            .Select(element => (string?)element.Attribute("Header")).ToArray();
        var details = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ProductDetailsPanel");

        Assert.Equal(new[] { "Resumo", "Especificações", "Fornecedores" }, tabs);
        Assert.Contains(details.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding SelectedProduct.Description}");
        Assert.Contains(details.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding SelectedSalePriceDisplay}");
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
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Click") == "ExportProductsClicked");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Click") == "ConfigureColumnsClicked");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Grid"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CatalogLayout");
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
            ("Descrição", typeof(ProductDTO).GetProperty(nameof(ProductDTO.Description))!),
            ("Preço de venda", typeof(ProductDTO).GetProperty(nameof(ProductDTO.SalePrice))!)
        };
        var product = ProductStockGridTests.CreateProduct(3, 0, 1) with
        {
            Description = "Vaso; azul \"alto\"",
            SalePrice = 1234.56m
        };

        await ProductCsvExporter.WriteAsync(output, [product], columns);
        var csv = Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');

        Assert.Contains("\"Descrição\";\"Preço de venda\"", csv);
        Assert.Contains("\"Vaso; azul \"\"alto\"\"\";\"1234,56\"", csv);
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