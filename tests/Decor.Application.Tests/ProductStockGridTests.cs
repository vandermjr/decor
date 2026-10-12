using System.Xml.Linq;
using Avalonia.Media;
using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Application.Tests;

public sealed class ProductStockGridTests
{
    [Theory]
    [InlineData(42, 12, 10, 30, "Positivo")]
    [InlineData(8, 5, 4, 3, "Atenção")]
    [InlineData(0, 0, 6, 0, "Repor")]
    [InlineData(-2, 0, 2, -2, "Negativo")]
    public void Stock_summary_separates_physical_reserved_available_and_status(
        decimal physical, decimal reserved, decimal minimum, decimal available, string status)
    {
        var product = CreateProduct(physical, reserved, minimum);

        product.CurrentStockQuantity.Should().Be(physical);
        product.ReservedStockQuantity.Should().Be(reserved);
        product.AvailableStockQuantity.Should().Be(available);
        product.StockStatus.Should().Be(status);
    }

    [Fact]
    public void Made_to_order_product_uses_the_sob_demanda_status_regardless_of_balance()
    {
        var product = CreateProduct(0m, 0m, 0m) with { AcquisitionMode = ProductAcquisitionMode.SobEncomenda };

        product.StockStatus.Should().Be("Sob Demanda");
    }

    [Fact]
    public void Shared_grid_selection_is_yellow_and_value_match_is_blue()
    {
        var root = FindWorkspaceRoot();
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var app = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "App.axaml"));
        var resources = app.Descendants().Single(element => element.Name.LocalName == "ResourceDictionary"
            && (string?)element.Attribute(xaml + "Key") == "Light");
        var darkResources = app.Descendants().Single(element => element.Name.LocalName == "ResourceDictionary"
            && (string?)element.Attribute(xaml + "Key") == "Dark");
        var selected = resources.Elements().Single(element => (string?)element.Attribute(xaml + "Key") == "DataGridRowSelectedBackgroundBrush");
        var selectionAccent = resources.Elements().Single(element => (string?)element.Attribute(xaml + "Key") == "DecorGridSelectionBrush");
        var rowStyle = app.Descendants().Single(element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "DataGridRow");
        var selectedRowStyle = app.Descendants().Single(element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "DataGridRow:selected");
        var cellStyle = app.Descendants().Single(element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "DataGridCell");
        var gridColors = File.ReadAllText(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Controls", "GridStyling", "GridControlThemeColors.cs"));

        Assert.Equal(Color.Parse("#FFF7D8"), Color.Parse(selected.Value));
        Assert.Equal(Color.Parse("#D8AF3E"), Color.Parse(selectionAccent.Value));
        Assert.Contains(rowStyle.Elements(), setter => (string?)setter.Attribute("Property") == "BorderThickness"
            && (string?)setter.Attribute("Value") == "3,0,0,0");
        Assert.Contains(rowStyle.Elements(), setter => (string?)setter.Attribute("Property") == "BorderBrush"
            && (string?)setter.Attribute("Value") == "Transparent");
        Assert.DoesNotContain(selectedRowStyle.Elements(), setter =>
            (string?)setter.Attribute("Property") == "BorderThickness");
        Assert.Contains(selectedRowStyle.Elements(), setter => (string?)setter.Attribute("Property") == "BorderBrush"
            && (string?)setter.Attribute("Value") == "{DynamicResource DecorGridSelectionBrush}");
        Assert.Contains(cellStyle.Elements(), setter => (string?)setter.Attribute("Property") == "VerticalContentAlignment"
            && (string?)setter.Attribute("Value") == "Stretch");
        foreach (var theme in new[] { resources, darkResources })
        {
            Assert.Equal("Transparent", (string?)theme.Elements().Single(element => (string?)element.Attribute(xaml + "Key")
                == "DataGridCellFocusVisualPrimaryBrush").Value);
            Assert.Equal("Transparent", (string?)theme.Elements().Single(element => (string?)element.Attribute(xaml + "Key")
                == "DataGridCellFocusVisualSecondaryBrush").Value);
        }
        Assert.Contains("#D9EFFA", gridColors);
        Assert.Contains("#174A63", gridColors);
    }

    [Fact]
    public void Product_grid_includes_brand_and_measure_unit_columns()
    {
        var root = FindWorkspaceRoot();
        var source = File.ReadAllText(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "ProductsView.axaml.cs"));

        Assert.Contains("nameof(ProductDTO.BrandName)", source);
        Assert.Contains("nameof(ProductDTO.StockUnitCode)", source);
    }

    [Fact]
    public void Product_stock_inputs_use_responsive_columns_without_spinner_buttons()
    {
        var root = FindWorkspaceRoot();
        var document = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "ProductsView.axaml"));
        var minimum = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == "Estoque Mínimo");
        var minimumInput = Assert.Single(minimum.Parent!.Elements().SelectMany(element => element.DescendantsAndSelf()),
            element => element.Name.LocalName == "NumericUpDown");
        var stockGrid = minimum.Parent!.Parent!;

        Assert.Equal("*,*", (string?)stockGrid.Attribute("ColumnDefinitions"));
        Assert.Equal("420", (string?)stockGrid.Attribute("MaxWidth"));
        Assert.Equal("False", (string?)minimumInput.Attribute("ShowButtonSpinner"));
        Assert.Equal("False", (string?)minimumInput.Attribute("AllowSpin"));
    }

    internal static ProductDTO CreateProduct(decimal physical, decimal reserved, decimal minimum) => new(
        ProductID: 1,
        Barcode: null,
        IsActive: true,
        Description: "Produto de teste",
        StockQuantity: 0,
        BrandID: 1,
        BrandName: "Marca",
        SubgroupID: 1,
        SubgroupName: "Subgrupo",
        GroupID: 1,
        GroupName: "Grupo",
        FamilyID: 1,
        FamilyName: "Família",
        ClassID: 1,
        ClassName: "Classe",
        ManufacturerRef: null,
        AuxiliarRef: null,
        Dimensions: null,
        Observations: null,
        MinimumStock: minimum,
        ProductType: 1,
        CostPrice: null,
        SalePrice: null,
        EmployeeCommissionValue: null,
        DefaultInstallationServiceID: null,
        StockUnitID: 1,
        StockUnitCode: "un",
        CurrentStockQuantity: physical,
        ReservedStockQuantity: reserved,
        AcquisitionMode: ProductAcquisitionMode.Estocado);

    private static DirectoryInfo FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln")))
            directory = directory.Parent;
        return directory ?? throw new DirectoryNotFoundException("Decor.sln was not found.");
    }
}