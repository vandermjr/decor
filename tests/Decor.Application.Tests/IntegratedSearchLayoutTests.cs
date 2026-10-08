using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class IntegratedSearchLayoutTests
{
    [Theory]
    [InlineData("BrandsView.axaml", "36", true)]
    [InlineData("ProductsView.axaml", "36", true)]
    [InlineData("CustomersView.axaml", "36", true)]
    [InlineData("SuppliersView.axaml", "36", true)]
    [InlineData("EmployeesView.axaml", "36", true)]
    [InlineData("UsersView.axaml", "36", true)]
    [InlineData("SalesView.axaml", "36", true)]
    [InlineData("PermissionsView.axaml", "36", true)]
    [InlineData("ContextualSearchWindow.axaml", "42", false)]
    public void Search_controls_share_one_clipped_field(string view, string height, bool hasClear)
    {
        var document = ReadView(view);
        var textBox = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding SearchText}");
        var grid = textBox.Parent!;
        var border = grid.Parent!;
        Assert.Equal("Grid", grid.Name.LocalName);
        Assert.Equal(hasClear ? "*,36,36" : "*,36", (string?)grid.Attribute("ColumnDefinitions"));
        Assert.Null(grid.Attribute("ColumnSpacing"));
        Assert.Equal("Border", border.Name.LocalName);
        Assert.Equal(height, (string?)border.Attribute("Height"));
        Assert.Equal("True", (string?)border.Attribute("ClipToBounds"));
        Assert.Equal("1", (string?)border.Attribute("BorderThickness"));
        Assert.Equal("4", (string?)border.Attribute("CornerRadius"));
        Assert.Equal("{DynamicResource DecorControlBrush}", (string?)border.Attribute("Background"));
        Assert.Equal("{DynamicResource DecorBorderBrush}", (string?)border.Attribute("BorderBrush"));
        Assert.Equal("0", (string?)textBox.Attribute("BorderThickness"));
        Assert.Equal(hasClear ? 3 : 2, grid.Elements().Count());
        AssertButton(grid, "SearchCommand", "search-button", "1", "Pesquisar");
        if (hasClear)
            AssertButton(grid, "ClearSearchCommand", "clear-search-button", "2", "Limpar pesquisa");
        else
            Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding ClearSearchCommand}");
        if (view != "PermissionsView.axaml")
        {
            Assert.Equal("SearchTextBox", (string?)textBox.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
            Assert.Equal("SearchTextBox_KeyDown", (string?)textBox.Attribute("KeyDown"));
        }
        if (view is "BrandsView.axaml" or "ProductsView.axaml")
        {
            Assert.Equal("0", (string?)textBox.Attribute("TabIndex"));
            Assert.Equal("1", (string?)grid.Elements().ElementAt(1).Attribute("TabIndex"));
        }
        if (view is "UsersView.axaml" or "PermissionsView.axaml")
        {
            Assert.Equal("3", (string?)border.Attribute("Grid.ColumnSpan"));
            var command = view == "UsersView.axaml" ? "NewUserCommand" : "AssignGroupsCommand";
            var action = Assert.Single(border.Parent!.Elements(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
            Assert.Equal("3", (string?)action.Attribute("Grid.Column"));
            if (view == "UsersView.axaml")
                Assert.Equal("4", (string?)Assert.Single(border.Parent.Elements(), element => (string?)element.Attribute("Command") == "{Binding EditUserCommand}").Attribute("Grid.Column"));
        }
        else if (view == "ContextualSearchWindow.axaml")
        {
            Assert.Equal("1", (string?)border.Attribute("Grid.Row"));
            Assert.Equal("40", (string?)textBox.Attribute("MinHeight"));
            Assert.Equal("16", (string?)textBox.Attribute("FontSize"));
            Assert.Equal("{Binding SearchHint}", (string?)textBox.Attribute("PlaceholderText"));
        }
        else if (view == "SalesView.axaml")
        {
            Assert.Null(border.Attribute("Grid.Row"));
            Assert.Equal("0,0,0,12", (string?)border.Attribute("Margin"));
        }
        else
        {
            Assert.Equal("{Binding !IsEditing}", (string?)border.Attribute("IsVisible"));
            Assert.Equal("*,Auto", (string?)border.Parent!.Attribute("ColumnDefinitions"));
            Assert.Equal("0,0,0,12", (string?)border.Parent.Attribute("Margin"));
            Assert.Equal("1", (string?)border.Parent.Elements().Last().Attribute("Grid.Column"));
        }
    }

    private static void AssertButton(XElement grid, string command, string classes, string column, string tooltip)
    {
        var button = Assert.Single(grid.Elements(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
        Assert.Equal("Button", button.Name.LocalName);
        Assert.Equal(classes, (string?)button.Attribute("Classes"));
        Assert.Equal(column, (string?)button.Attribute("Grid.Column"));
        foreach (var property in new[] { "Margin", "Padding", "MinHeight", "BorderThickness", "CornerRadius" })
            Assert.Equal("0", (string?)button.Attribute(property));
        Assert.Equal("36", (string?)button.Attribute("Width"));
        Assert.Equal("36", (string?)button.Attribute("MinWidth"));
        Assert.Equal("NaN", (string?)button.Attribute("Height"));
        Assert.Equal("Stretch", (string?)button.Attribute("VerticalAlignment"));
        Assert.Equal("Stretch", (string?)button.Attribute("HorizontalAlignment"));
        Assert.Equal(tooltip, (string?)button.Attribute("ToolTip.Tip"));
        var icon = Assert.Single(button.Elements());
        Assert.Equal("Path", icon.Name.LocalName);
        Assert.Equal("16", (string?)icon.Attribute("Width"));
    }

    private static XDocument ReadView(string view)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        return XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", view));
    }
}