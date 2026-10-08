using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class ServicesLayoutTests
{
    [Fact]
    public void Catalog_UsesSharedListingEmbeddedSearchAndInlineForm()
    {
        var root = SourceDirectory();
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "ServicesView.axaml"));
        var grid = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorDataGridControl");
        Assert.Equal("{Binding Services}", (string?)grid.Attribute("ItemsSource"));
        Assert.Equal("{Binding}", (string?)grid.Attribute("PaginationSource"));
        Assert.Equal("ServiceID", (string?)grid.Attribute("DefaultSortMemberPath"));
        var search = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == "{Binding SearchText}");
        Assert.Equal("*,36,36", (string?)search.Parent!.Attribute("ColumnDefinitions"));
        Assert.Equal("True", (string?)search.Parent.Parent!.Attribute("ClipToBounds"));
        foreach (var command in new[] { "SearchCommand", "ClearSearchCommand", "NewCommand", "EditCommand", "DeleteCommand", "SaveCommand", "CancelCommand", "ConfirmDeleteCommand", "CancelDeleteCommand" })
            Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
        foreach (var field in new[] { "ServiceCodeDisplay", "Description", "CostPrice", "SalePrice", "EmployeeCommissionValue", "ObservationsCounter" })
            Assert.Contains(document.Descendants().Attributes(), attribute => attribute.Value == $"{{Binding {field}}}");
        var description = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox" && (string?)element.Attribute("Text") == "{Binding Description}");
        Assert.Equal("255", (string?)description.Attribute("MaxLength"));
        foreach (var field in new[] { "CostPrice", "SalePrice", "EmployeeCommissionValue" })
        {
            var money = Assert.Single(document.Descendants(), element => element.Name.LocalName == "NumericUpDown" && (string?)element.Attribute("Value") == $"{{Binding {field}}}");
            Assert.Equal("0", (string?)money.Attribute("Minimum"));
            Assert.Equal("99999999.99", (string?)money.Attribute("Maximum"));
            Assert.Equal("0.01", (string?)money.Attribute("Increment"));
            Assert.Equal("N2", (string?)money.Attribute("FormatString"));
        }
        var observations = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox" && (string?)element.Attribute("Text") == "{Binding Observations, UpdateSourceTrigger=PropertyChanged}");
        Assert.Equal("65535", (string?)observations.Attribute("MaxLength"));
        Assert.Equal("True", (string?)observations.Attribute("AcceptsReturn"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "ToggleSwitch" && (string?)element.Attribute("IsChecked") == "{Binding IsActive}");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "DataTemplate");
    }

    [Fact]
    public void Navigation_CatalogIsDistinctFromAgendaAndPermissionGated()
    {
        var root = SourceDirectory();
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "MainWindow.axaml"));
        var catalog = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding ShowServicesCommand}");
        Assert.Equal("{Binding CanViewServices}", (string?)catalog.Attribute("IsVisible"));
        Assert.Equal("Servi\u00e7os", (string?)catalog.Attribute("Header"));
        Assert.Contains(catalog.Parent!.Elements(), element => (string?)element.Attribute("Header") == "Agenda" && (string?)element.Attribute("IsEnabled") == "False");
    }

    private static DirectoryInfo SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        new(Path.GetDirectoryName(source)!);
}