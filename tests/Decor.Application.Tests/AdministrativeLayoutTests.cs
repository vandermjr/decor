using System.Xml.Linq;
using Avalonia.Controls;
using Decor.Application.Services;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class AdministrativeLayoutTests
{
    [Theory]
    [InlineData("BrandsView.axaml")]
    [InlineData("ProductsView.axaml")]
    [InlineData("EmployeesView.axaml")]
    [InlineData("GroupsView.axaml")]
    public void Editors_have_a_fixed_separated_footer_outside_the_scrollable_fields(string view)
    {
        var document = ReadXaml($"Views/{view}");
        foreach (var command in new[] { "SaveCommand", "CancelCommand" })
        {
            var footer = AssertFooter(document, command);
            Assert.Equal("1", (string?)footer.Attribute("Grid.Row"));
            Assert.Equal("*,Auto", (string?)footer.Parent!.Attribute("RowDefinitions"));
            var scroll = Assert.Single(footer.Parent.Elements(), element => element.Name.LocalName == "ScrollViewer");
            Assert.Equal("Auto", (string?)scroll.Attribute("VerticalScrollBarVisibility"));
            Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
            Assert.Contains(footer.Ancestors(), element => (string?)element.Attribute("IsVisible") == "{Binding IsEditing}");
            Assert.Contains(scroll.Descendants(), element => element.Name.LocalName == "TextBox");
        }
    }

    [Theory]
    [InlineData("BrandsView.axaml")]
    [InlineData("ProductsView.axaml")]
    [InlineData("EmployeesView.axaml")]
    [InlineData("GroupsView.axaml")]
    public void New_and_edit_actions_remain_in_the_list_toolbar(string view)
    {
        var document = ReadXaml($"Views/{view}");
        var rootGrid = Assert.Single(document.Root!.Elements(), element => element.Name.LocalName == "Grid");
        var toolbar = rootGrid.Elements().First();
        Assert.Equal("Grid", toolbar.Name.LocalName);
        Assert.Null(toolbar.Attribute("Grid.Row"));
        foreach (var command in new[] { "NewCommand", "EditCommand" })
        {
            var button = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
            Assert.Contains(toolbar, button.Ancestors());
        }
        Assert.DoesNotContain(toolbar.Descendants(), element => (string?)element.Attribute("Command") is "{Binding SaveCommand}" or "{Binding CancelCommand}");
    }

    [Theory]
    [InlineData("RolesView.axaml")]
    [InlineData("PermissionsView.axaml")]
    public void Permission_actions_are_in_the_last_auto_row_below_bounded_lists(string view)
    {
        var document = ReadXaml($"Views/{view}");
        foreach (var command in new[] { "SaveCommand", "RestoreCommand" })
        {
            var footer = AssertFooter(document, command);
            Assert.Equal("3", (string?)footer.Attribute("Grid.Row"));
            Assert.Equal("Auto,*,Auto,Auto", (string?)footer.Parent!.Attribute("RowDefinitions"));
            var content = Assert.Single(footer.Parent.Elements(), element => (string?)element.Attribute("Grid.Row") == "1");
            Assert.Contains(content.Descendants(), element => element.Name.LocalName == "ListBox");
        }
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding CancelCommand}");
    }

    [Theory]
    [InlineData("ProductEditWindow.axaml", "SaveCommand", "CancelCommand")]
    [InlineData("ChangePasswordWindow.axaml", "ChangePasswordCommand", null)]
    [InlineData("CreateUserWindow.axaml", "CreateCommand", "CancelCommand")]
    [InlineData("EditUserWindow.axaml", "SaveCommand", "CancelCommand")]
    [InlineData("EditUserRolesWindow.axaml", "SaveCommand", "CancelCommand")]
    public void Modals_keep_actions_in_a_fixed_footer_and_all_fields_scrollable(string view, string saveCommand, string? cancelCommand)
    {
        var document = ReadXaml($"Views/{view}");
        var footer = AssertFooter(document, saveCommand);
        Assert.Equal("1", (string?)footer.Attribute("Grid.Row"));
        Assert.Equal("*,Auto", (string?)footer.Parent!.Attribute("RowDefinitions"));
        Assert.Same(document.Root, footer.Parent.Parent);
        var scroll = Assert.Single(footer.Parent.Elements(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal("Auto", (string?)scroll.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
        Assert.Contains(scroll.Descendants(), element => element.Name.LocalName is "TextBox" or "ItemsControl");
        Assert.DoesNotContain(footer.Parent.Elements(), element => element != footer && element != scroll);
        Assert.All(document.Descendants().Where(element => (string?)element.Attribute("IsVisible") is "{Binding HasError}" or "{Binding HasTemporaryPassword}"),
            element => Assert.Contains(scroll, element.Ancestors()));
        if (cancelCommand is not null)
            Assert.Same(footer, AssertFooter(document, cancelCommand));
        else
            Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding CancelCommand}");
        if (view == "CreateUserWindow.axaml")
            Assert.Same(footer, AssertFooter(document, "FinishCommand"));
    }

    [Fact]
    public void About_requests_a_dialog_without_opening_a_workspace_tab()
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(new ApplicationUser { UserID = 1, Username = "admin" });
        var viewModel = new MainViewModel(new Navigation(), new Theme(), new AuthorizationService(context), context);
        var requests = 0;
        viewModel.AboutRequested += (_, _) => requests++;
        viewModel.ShowAboutCommand.Execute(null);
        Assert.Equal(1, requests);
        Assert.Empty(viewModel.OpenDocuments);
        Assert.Null(viewModel.ActiveDocument);
    }

    [Fact]
    public void Settings_groups_access_and_database_maintenance_in_submenus()
    {
        var document = ReadXaml("MainWindow.axaml");
        var access = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Header") == "Acessos");
        Assert.Equal(new[] { "Usuários", "Grupos", "Permissões" }, access.Elements()
            .Where(element => element.Name.LocalName == "MenuItem").Select(element => (string?)element.Attribute("Header")));
        var maintenance = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Header") == "Manutenção do banco de dados");
        Assert.Contains(maintenance.Elements(), element => (string?)element.Attribute("Header") == "Backup e restauração");
    }

    [Fact]
    public void Users_listing_only_searches_and_shows_without_group_or_state_editing()
    {
        var document = ReadXaml("Views/UsersView.axaml");
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("ItemsSource") == "{Binding Groups}");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "CheckBox");
        Assert.DoesNotContain(document.Descendants().Attributes(), attribute => attribute.Value.Contains("SaveGroupsCommand", StringComparison.Ordinal)
            || attribute.Value.Contains("ToggleActiveCommand", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Text") == "{Binding StatusMessage}");
        Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding ClearSearchCommand}");
        var groups = Assert.Single(document.Descendants(), element => (string?)element.Attribute("ItemsSource") == "{Binding SelectedUser.Roles}");
        Assert.Equal("ListBox", groups.Name.LocalName);
        Assert.Contains(groups.Parent!.Elements(), element => (string?)element.Attribute("Text") == "Grupos associados");
    }

    [Fact]
    public void Groups_list_actions_remain_above_the_editor_and_hierarchy_is_not_visible()
    {
        var document = ReadXaml("Views/GroupsView.axaml");
        foreach (var command in new[] { "NewCommand", "EditCommand" })
        {
            var button = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
            Assert.Equal("2", (string?)button.Parent!.Attribute("Grid.Column"));
            Assert.Null(button.Parent!.Parent!.Attribute("Grid.Row"));
        }
        Assert.DoesNotContain(document.Descendants().Attributes(), attribute => attribute.Value.Contains("HierarchyLevel", StringComparison.Ordinal));
    }

    [Fact]
    public void Maintenance_tabs_share_margins_and_backup_has_no_duplicate_subtitle()
    {
        var tabs = ReadXaml("Views/DatabaseMaintenanceView.axaml").Descendants()
            .Where(element => element.Name.LocalName == "TabItem").ToArray();
        Assert.Equal(2, tabs.Length);
        Assert.All(tabs, tab => Assert.Equal("16", (string?)Assert.Single(tab.Elements()).Attribute("Margin")));
        Assert.DoesNotContain(tabs[0].Descendants(), element => (string?)element.Attribute("Text") == "Backup");
    }

    [Fact]
    public void Edit_user_keeps_code_and_status_in_one_row_and_icon_actions_in_the_footer()
    {
        var document = ReadXaml("Views/EditUserWindow.axaml");
        var code = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == "{Binding UserCodeDisplay}");
        var status = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ToggleSwitch");
        Assert.Same(code.Parent!.Parent!.Parent, status.Parent!.Parent!.Parent);
        Assert.Equal("1", (string?)code.Parent!.Parent!.Parent!.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)status.Parent!.Parent!.Attribute("Grid.Column"));
        foreach (var command in new[] { "SaveCommand", "CancelCommand" })
        {
            var button = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
            Assert.Contains(button.Descendants(), element => element.Name.LocalName == "Path");
            Assert.Equal("1", (string?)AssertFooter(document, command).Attribute("Grid.Row"));
        }
    }

    [Fact]
    public void Users_inline_create_and_edit_share_one_form_with_separated_footer()
    {
        var users = ReadXaml("Views/UsersView.axaml");
        var form = Assert.Single(users.Descendants(), element => element.Name.LocalName == "UserFormView");
        Assert.Equal("{Binding ActiveForm}", (string?)form.Attribute("DataContext"));
        Assert.Equal("{Binding IsEditing}", (string?)form.Parent!.Attribute("IsVisible"));
        Assert.Equal("2", (string?)form.Parent.Attribute("Grid.RowSpan"));
        var document = ReadXaml("Views/UserFormView.axaml");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "UserControl.DataTemplates"
            || element.Name.LocalName == "ContentControl");
        Assert.Equal("vm:UserFormViewModel", (string?)document.Root!.Attribute(XName.Get("DataType", "http://schemas.microsoft.com/winfx/2006/xaml")));
        Assert.DoesNotContain(document.Descendants().Attributes(), attribute => attribute.Value.Contains("HierarchyLevel", StringComparison.Ordinal)
            || attribute.Value.Contains("CreateUserViewModel", StringComparison.Ordinal)
            || attribute.Value.Contains("EditUserViewModel", StringComparison.Ordinal));
        var header = document.Root.Elements().Single().Elements().First();
        Assert.Equal("StackPanel", header.Name.LocalName);
        Assert.Equal("{Binding FormTitle}", (string?)header.Elements().First().Attribute("Text"));
        var identity = header.Elements().ElementAt(1);
        Assert.Equal("Grid", identity.Name.LocalName);
        Assert.Equal("Auto,*,Auto", (string?)identity.Attribute("ColumnDefinitions"));
        Assert.DoesNotContain(identity.AncestorsAndSelf().Attributes(), attribute => attribute.Name.LocalName == "IsVisible");
        var code = Assert.Single(identity.Descendants(), element => (string?)element.Attribute("Text") == "{Binding UserCodeDisplay}");
        Assert.Null(code.Parent!.Parent!.Attribute("Grid.Column"));
        var status = Assert.Single(identity.Descendants(), element => element.Name.LocalName == "ToggleSwitch");
        Assert.Equal("2", (string?)status.Parent!.Parent!.Attribute("Grid.Column"));
        Assert.Contains(status.Parent.Elements(), element => (string?)element.Attribute("Text") == "Estado");
        Assert.DoesNotContain(identity.Descendants(), element => element.Name.LocalName is "Path" or "PathIcon");
        Assert.Equal("{Binding CanChangeActive}", (string?)status.Attribute("IsEnabled"));
        var groups = Assert.Single(document.Descendants(), element => (string?)element.Attribute("ItemsSource") == "{Binding Groups}"
            && (string?)element.Attribute("IsEnabled") == "{Binding CanEditGroups}");
        Assert.Equal("ListBox", groups.Name.LocalName);
        Assert.Contains(groups.Parent!.Elements(), element => (string?)element.Attribute("Text") == "Grupos associados");
        Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding Username, Mode=TwoWay}");
        foreach (var command in new[] { "SaveCommand", "DismissCommand" })
        {
            var button = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
            var footer = button.Parent!.Parent!;
            Assert.Equal("Border", footer.Name.LocalName);
            Assert.Equal("0,1,0,0", (string?)footer.Attribute("BorderThickness"));
            Assert.Equal("2", (string?)footer.Attribute("Grid.Row"));
            Assert.Contains(button.Descendants(), element => element.Name.LocalName == "Path");
        }
    }

    private static XElement AssertFooter(XDocument document, string command)
    {
        var button = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
        var footer = button.Parent!.Parent!;
        Assert.Equal("Border", footer.Name.LocalName);
        Assert.Equal("{DynamicResource DecorBorderBrush}", (string?)footer.Attribute("BorderBrush"));
        Assert.Equal("0,1,0,0", (string?)footer.Attribute("BorderThickness"));
        Assert.Equal("Right", (string?)button.Parent.Attribute("HorizontalAlignment"));
        Assert.DoesNotContain(button.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        return footer;
    }

    private static XDocument ReadXaml(string relativePath)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln"))) root = root.Parent;
        Assert.NotNull(root);
        return XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", relativePath));
    }

    private sealed class Navigation : INavigationService
    {
        public T Resolve<T>() where T : class => throw new InvalidOperationException();
        public Task ShowDialogAsync(Window owner, Window dialog) => throw new InvalidOperationException();
    }

    private sealed class Theme : IThemeService
    {
        public DecorThemeStyle CurrentTheme => DecorThemeStyle.Light;
        public event Action<DecorThemeStyle>? ThemeChanged { add { } remove { } }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetThemeAsync(DecorThemeStyle newTheme) => Task.CompletedTask;
    }
}