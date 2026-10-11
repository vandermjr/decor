using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class PermissionsSharedGridTests
{
    [Fact]
    public void Permissions_use_shared_automatic_pagination_and_keep_all_columns_and_customization()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "PermissionsView.axaml"));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "DataGrid");
        var grid = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorDataGridControl");
        Assert.Equal("using:Decor.AvaloniaUI.Controls", grid.Name.NamespaceName);
        Assert.Equal("{Binding Permissions}", (string?)grid.Attribute("ItemsSource"));
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Null(grid.Attribute("CanUserSortColumns"));
        Assert.Equal("FormName", (string?)grid.Attribute("DefaultSortMemberPath"));
        Assert.Equal("Horizontal", (string?)grid.Attribute("GridLinesVisibility"));
        Assert.Null(grid.Attribute("PaginationSource"));
        var columns = Assert.Single(grid.Elements()).Elements().ToArray();
        Assert.Equal(new[] { "Formulário", "Ação", "Dos grupos", "Personalizada", "Efetiva" },
            columns.Select(column => (string?)column.Attribute("Header")));
        Assert.Equal(new[] { "*", "*", "130", "180", "130" }, columns.Select(column => (string?)column.Attribute("Width")));
        Assert.All(columns, column => Assert.Null(column.Attribute("CanUserSort")));
        Assert.Equal(new[] { "FormName", "ActionName", "InheritedAccess", "Selection", "EffectiveAccess" },
            columns.Select(column => (string?)column.Attribute("SortMemberPath")));
        Assert.Equal(new[] { "{Binding FormName}", "{Binding ActionName}", "{Binding InheritedAccess}", "{Binding EffectiveAccess}" },
            columns.Where(column => column.Name.LocalName == "DataGridTextColumn").Select(column => (string?)column.Attribute("Binding")));
        var combo = Assert.Single(columns[3].Descendants(), element => element.Name.LocalName == "ComboBox");
        Assert.Equal("{x:Static vm:UserPermissionOption.Choices}", (string?)combo.Attribute("ItemsSource"));
        Assert.Equal("{Binding Selection, Mode=TwoWay}", (string?)combo.Attribute("SelectedItem"));
        Assert.Equal("{Binding CanCustomize}", (string?)combo.Attribute("IsEnabled"));
        Assert.Equal("Stretch", (string?)combo.Attribute("HorizontalAlignment"));
    }
}