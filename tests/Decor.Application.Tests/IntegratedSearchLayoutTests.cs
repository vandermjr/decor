using System.Xml.Linq;
using Decor.AvaloniaUI.Controls;
using Decor.Core.Common;

namespace Decor.Application.Tests;

public sealed class IntegratedSearchLayoutTests
{
    [Theory]
    [InlineData("BrandsView.axaml", true)]
    [InlineData("ProductsView.axaml", true)]
    [InlineData("ServicesView.axaml", true)]
    [InlineData("CustomersView.axaml", true)]
    [InlineData("SuppliersView.axaml", true)]
    [InlineData("EmployeesView.axaml", true)]
    [InlineData("UsersView.axaml", true)]
    [InlineData("SalesView.axaml", true)]
    [InlineData("QuotesView.axaml", true)]
    [InlineData("PermissionsView.axaml", true)]
    [InlineData("ContextualSearchWindow.axaml", false)]
    public void Search_fields_use_shared_control_without_changing_bindings_or_placement(string view, bool hasClear)
    {
        var document = ReadView(view);
        var field = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField"
            && (string?)element.Attribute("Text") == "{Binding SearchText, Mode=TwoWay}");
        Assert.Equal("{Binding SearchCommand}", (string?)field.Attribute("SearchCommand"));
        Assert.Equal(hasClear ? "{Binding ClearSearchCommand}" : null, (string?)field.Attribute("ClearCommand"));
        Assert.NotNull(field.Attribute("PlaceholderText"));
        Assert.Null(field.Attribute("KeyDown"));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding SearchText}");
        if (view != "PermissionsView.axaml")
        {
            Assert.Equal("SearchTextBox", (string?)field.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
            var code = File.ReadAllText(Path.Combine(ViewDirectory(), view + ".cs"));
            Assert.Contains("SearchTextBox.FocusTextInput()", code);
            Assert.DoesNotContain("SearchTextBox.Focus()", code);
            Assert.DoesNotContain("SearchTextBox_KeyDown", code);
        }
        if (view is "BrandsView.axaml" or "ProductsView.axaml" or "ServicesView.axaml")
            Assert.Equal("0", (string?)field.Attribute("TabIndex"));
        if (view is "UsersView.axaml" or "PermissionsView.axaml")
        {
            Assert.Equal("3", (string?)field.Attribute("Grid.ColumnSpan"));
            var command = view == "UsersView.axaml" ? "NewUserCommand" : "AssignGroupsCommand";
            var action = Assert.Single(field.Parent!.Elements(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
            Assert.Equal("3", (string?)action.Attribute("Grid.Column"));
            if (view == "UsersView.axaml")
                Assert.Equal("4", (string?)Assert.Single(field.Parent.Elements(), element => (string?)element.Attribute("Command") == "{Binding EditUserCommand}").Attribute("Grid.Column"));
        }
        else if (view == "ContextualSearchWindow.axaml")
        {
            Assert.Equal("1", (string?)field.Attribute("Grid.Row"));
            Assert.Equal("{Binding SearchHint}", (string?)field.Attribute("PlaceholderText"));
        }
        else if (view == "SalesView.axaml")
        {
            Assert.Null(field.Attribute("Grid.Row"));
            Assert.Equal("0,0,0,12", (string?)field.Attribute("Margin"));
        }
        else
        {
            var visibility = view is "ServicesView.axaml" or "QuotesView.axaml" ? field.Parent! : field;
            Assert.Equal("{Binding !IsEditing}", (string?)visibility.Attribute("IsVisible"));
            Assert.Equal("*,Auto", (string?)field.Parent!.Attribute("ColumnDefinitions"));
            Assert.Equal("0,0,0,12", (string?)field.Parent.Attribute("Margin"));
            Assert.Equal("1", (string?)field.Parent.Elements().Last().Attribute("Grid.Column"));
        }
    }

    [Fact]
    public void Quote_catalog_uses_shared_pair_but_read_only_lookups_are_not_search_inputs()
    {
        var document = ReadView("QuotesView.axaml");
        var catalog = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField"
            && (string?)element.Attribute("Text") == "{Binding ProductSearchText, Mode=TwoWay}");
        Assert.Equal("{Binding SearchProductsCommand}", (string?)catalog.Attribute("SearchCommand"));
        Assert.Equal("{Binding ClearProductsCommand}", (string?)catalog.Attribute("ClearCommand"));
        foreach (var binding in new[] { "CustomerSummary", "EmployeeSummary", "CurrentQuoteId" })
        {
            var lookup = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == $"{{Binding {binding}}}");
            Assert.Equal("TextBox", lookup.Name.LocalName);
            Assert.Equal("True", (string?)lookup.Attribute("IsReadOnly"));
        }
    }

    [Fact]
    public void Products_describe_actual_goods_search_and_supported_stock_examples()
    {
        var field = Assert.Single(ReadView("ProductsView.axaml").Descendants(), element => element.Name.LocalName == "DecorSearchField");
        Assert.Equal("Pesquisar por código, descrição, marca ou referência", (string?)field.Attribute("PlaceholderText"));
        Assert.Equal("{x:Static controls:DecorSearchField.ProductSearchHelp}", (string?)field.Attribute("SearchHelp"));
        Assert.DoesNotContain("serviço", DecorSearchField.ProductSearchHelp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("servico", DecorSearchField.ProductSearchHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0m, ProductSearchQuery.Parse("sem estoque").StockEquals);
        Assert.Equal(0m, ProductSearchQuery.Parse("estoque negativo").StockLessThan);
        Assert.Equal(0m, ProductSearchQuery.Parse("piso com estoque").StockGreaterThan);
        Assert.Equal(5.5m, ProductSearchQuery.Parse("estoque acima de 5,5").StockGreaterThan);
        Assert.Equal(10m, ProductSearchQuery.Parse("estoque abaixo de 10").StockLessThan);
        Assert.Equal(new[] { "piso vinílico" }, ProductSearchQuery.Parse("\"piso vinílico\"").Tokens);
    }

    private static XDocument ReadView(string view) => XDocument.Load(Path.Combine(ViewDirectory(), view));

    private static string ViewDirectory()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        return Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views");
    }
}