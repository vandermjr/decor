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
        var search = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField");
        Assert.Equal("{Binding SearchText, Mode=TwoWay}", (string?)search.Attribute("Text"));
        Assert.Equal("{Binding SearchCommand}", (string?)search.Attribute("SearchCommand"));
        Assert.Equal("{Binding ClearSearchCommand}", (string?)search.Attribute("ClearCommand"));
        Assert.Equal("SearchTextBox", (string?)search.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
        Assert.Equal("Pesquisar por código ou descrição", (string?)search.Attribute("PlaceholderText"));
        foreach (var command in new[] { "NewCommand", "EditCommand", "DeleteCommand", "SaveCommand", "CancelCommand", "ConfirmDeleteCommand", "CancelDeleteCommand" })
            Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
        foreach (var field in new[] { "ServiceCodeDisplay", "Description", "CostPrice", "SalePrice", "EmployeeCommissionValue", "ObservationsCounter" })
            Assert.Contains(document.Descendants().Attributes(), attribute => attribute.Value == $"{{Binding {field}}}");
        var description = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox" && (string?)element.Attribute("Text") == "{Binding Description}");
        Assert.Equal("255", (string?)description.Attribute("MaxLength"));
        Assert.Contains(description.Attributes(), attribute => attribute.Name.LocalName == "DecorRequiredField.IsRequired" && attribute.Value == "True");
        var label = Assert.Single(description.Parent!.Elements(), element => element.Name.LocalName == "TextBlock");
        Assert.Equal("{DynamicResource DecorFontWeightBase}", (string?)label.Attribute("FontWeight"));
        Assert.DoesNotContain((string?)label.Attribute("Text") ?? string.Empty, character => character == '*');
        foreach (var field in new[] { "CostPrice", "SalePrice", "EmployeeCommissionValue" })
        {
            var money = Assert.Single(document.Descendants(), element => element.Name.LocalName == "NumericUpDown" && (string?)element.Attribute("Value") == $"{{Binding {field}}}");
            Assert.Equal("0", (string?)money.Attribute("Minimum"));
            Assert.Equal("99999999.99", (string?)money.Attribute("Maximum"));
            Assert.Equal("0.01", (string?)money.Attribute("Increment"));
            Assert.Equal("N2", (string?)money.Attribute("FormatString"));
            Assert.DoesNotContain(money.Attributes(), attribute => attribute.Name.LocalName == "DecorRequiredField.IsRequired");
        }
        var observations = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox" && (string?)element.Attribute("Text") == "{Binding Observations, UpdateSourceTrigger=PropertyChanged}");
        Assert.Equal("65535", (string?)observations.Attribute("MaxLength"));
        Assert.Equal("True", (string?)observations.Attribute("AcceptsReturn"));
        var state = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ToggleSwitch" && (string?)element.Attribute("IsChecked") == "{Binding IsActive}");
        var code = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == "{Binding ServiceCodeDisplay}");
        Assert.Same(code.Parent!.Parent, state.Parent!.Parent);
        var identity = code.Parent.Parent!;
        Assert.Equal("Auto,*,Auto", (string?)identity.Attribute("ColumnDefinitions"));
        Assert.Equal("2", (string?)state.Parent.Attribute("Grid.Column"));
        Assert.Equal("Border", identity.Parent!.Name.LocalName);
        Assert.Equal("{DynamicResource DecorSurfaceRaisedBrush}", (string?)identity.Parent.Attribute("Background"));
        Assert.DoesNotContain(identity.Descendants(), element => element.Name.LocalName == "Border");
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