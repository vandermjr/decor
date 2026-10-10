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
    public void Document_tab_scroll_buttons_stay_visible_before_the_tab_strip()
    {
        var document = ReadXaml("MainWindow.axaml");
        var left = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "Button" && ((string?)element.Attribute("Name") == "DocumentTabsScrollLeft"
                || (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "DocumentTabsScrollLeft"));
        var right = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "Button" && ((string?)element.Attribute("Name") == "DocumentTabsScrollRight"
                || (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "DocumentTabsScrollRight"));

        Assert.NotEqual("False", (string?)left.Attribute("IsVisible"));
        Assert.NotEqual("False", (string?)right.Attribute("IsVisible"));
        Assert.Equal("0", (string?)left.Attribute("Grid.Column"));
        Assert.Equal("1", (string?)right.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)Assert.Single(document.Descendants(), element => element.Name.LocalName == "ScrollViewer"
            && (string?)element.Attribute("Name") == "DocumentTabsScroller").Attribute("Grid.Column"));
        var scroller = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ScrollViewer"
            && (string?)element.Attribute("Name") == "DocumentTabsScroller");
        var leftRule = Assert.Single(scroller.Elements(), element => element.Name.LocalName == "Border");
        Assert.Equal("1,0,0,0", (string?)leftRule.Attribute("BorderThickness"));
        Assert.Equal("{DynamicResource DecorBorderBrush}", (string?)leftRule.Attribute("BorderBrush"));
        Assert.Equal("ItemsControl", leftRule.Elements().Single().Name.LocalName);
        Assert.Equal("grid-page-button", (string?)left.Attribute("Classes"));
        Assert.Equal("grid-page-button", (string?)right.Attribute("Classes"));

        var arrowIcons = new[] { left, right }
            .SelectMany(button => button.Descendants().Where(element => element.Name.LocalName == "PathIcon"))
            .ToArray();
        Assert.Equal(2, arrowIcons.Length);
        Assert.All(arrowIcons, icon => Assert.Equal("16", (string?)icon.Attribute("Width")));
        Assert.All(arrowIcons, icon => Assert.Equal("16", (string?)icon.Attribute("Height")));
        Assert.All(arrowIcons, icon => Assert.Equal("{DynamicResource DecorTextBrush}", (string?)icon.Attribute("Foreground")));
        Assert.All(arrowIcons, icon => Assert.Equal("400", (string?)icon.Attributes().Single(attribute => attribute.Name.LocalName == "DecorIcon.Weight").Value));

        var app = ReadXaml("App.axaml");
        var buttonStyles = app.Descendants().Where(element => element.Name.LocalName == "Style").ToArray();
        var gridPageStyle = Assert.Single(buttonStyles, style => (string?)style.Attribute("Selector") == "Button.grid-page-button");
        Assert.Contains(gridPageStyle.Elements(), setter => (string?)setter.Attribute("Property") == "Width" && (string?)setter.Attribute("Value") == "26");
        Assert.Contains(gridPageStyle.Elements(), setter => (string?)setter.Attribute("Property") == "Height" && (string?)setter.Attribute("Value") == "24");
        var hoverStyle = Assert.Single(buttonStyles, style => (string?)style.Attribute("Selector") == "Button.grid-page-button:pointerover");
        Assert.Contains(hoverStyle.Elements(), setter => (string?)setter.Attribute("Property") == "Background"
            && (string?)setter.Attribute("Value") == "{DynamicResource DecorScrollTrackBrush}");
        var disabledStyle = Assert.Single(buttonStyles, style => (string?)style.Attribute("Selector") == "Button.grid-page-button:disabled");
        Assert.Contains(disabledStyle.Elements(), setter => (string?)setter.Attribute("Property") == "Opacity" && (string?)setter.Attribute("Value") == "0.35");
    }

    [Fact]
    public void Profile_flyout_header_is_rounded_and_inactive_document_tabs_keep_the_bottom_rule()
    {
        var document = ReadXaml("MainWindow.axaml");
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var userFlyout = Assert.Single(document.Descendants(), element => (string?)element.Attribute(xaml + "Key") == "UserFlyout");
        var profileName = Assert.Single(userFlyout.Descendants(), element => (string?)element.Attribute("Text") == "{Binding UserPresentationName}");
        Assert.Equal("7,7,0,0", (string?)profileName.Ancestors().First(element => element.Name.LocalName == "Border").Attribute("CornerRadius"));

        var tabBorder = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("PointerPressed") == "DocumentTab_PointerPressed");
        Assert.Contains(tabBorder.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Height") == "1"
            && (string?)element.Attribute("Background") == "{DynamicResource DecorBorderBrush}"
            && (string?)element.Attribute("IsVisible") == "{Binding !IsActive}");
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
        var search = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField");
        Assert.Equal("{Binding SearchText, Mode=TwoWay}", (string?)search.Attribute("Text"));
        Assert.Equal("{Binding SearchCommand}", (string?)search.Attribute("SearchCommand"));
        Assert.Equal("{Binding ClearSearchCommand}", (string?)search.Attribute("ClearCommand"));
        Assert.Equal("SearchTextBox", (string?)search.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
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
        var identity = AssertIdentityHeader(code, status);
        Assert.Equal("1", (string?)identity.Parent!.Attribute("Grid.Row"));
        var username = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding Username}");
        AssertRequiredInputWithNormalLabel(username, "Usuário");
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
        var identityBorder = header.Elements().ElementAt(1);
        var code = Assert.Single(identityBorder.Descendants(), element => (string?)element.Attribute("Text") == "{Binding UserCodeDisplay}");
        var status = Assert.Single(identityBorder.Descendants(), element => element.Name.LocalName == "ToggleSwitch");
        var identity = AssertIdentityHeader(code, status);
        Assert.Same(identityBorder, identity.Parent);
        Assert.DoesNotContain(identity.AncestorsAndSelf().Attributes(), attribute => attribute.Name.LocalName == "IsVisible");
        Assert.DoesNotContain(identity.Descendants(), element => (string?)element.Attribute("Text") == "Estado");
        Assert.Equal("{Binding IsActive, Mode=TwoWay}", (string?)status.Attribute("IsChecked"));
        Assert.Equal("Ativo", (string?)status.Attribute("OnContent"));
        Assert.Equal("Bloqueado", (string?)status.Attribute("OffContent"));
        Assert.DoesNotContain(identity.Descendants(), element => element.Name.LocalName is "Path" or "PathIcon");
        Assert.Equal("{Binding CanChangeActive}", (string?)status.Attribute("IsEnabled"));
        var groups = Assert.Single(document.Descendants(), element => (string?)element.Attribute("ItemsSource") == "{Binding Groups}"
            && (string?)element.Attribute("IsEnabled") == "{Binding CanEditGroups}");
        Assert.Equal("ListBox", groups.Name.LocalName);
        Assert.Contains(groups.Parent!.Elements(), element => (string?)element.Attribute("Text") == "Grupos associados");
        var username = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding Username, Mode=TwoWay}");
        AssertRequiredInputWithNormalLabel(username, "Usuário");
        Assert.DoesNotContain(groups.Attributes(), attribute => attribute.Name.LocalName == "DecorRequiredField.IsRequired");
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

    [Fact]
    public void Quote_header_stays_in_catalog_column_while_items_use_full_right_column()
    {
        var document = ReadXaml("Views/QuotesView.axaml");
        var editing = Assert.Single(document.Descendants(), element =>
            (string?)element.Attribute("IsVisible") == "{Binding IsEditing}");
        Assert.Equal("*,Auto,Auto", (string?)editing.Attribute("RowDefinitions"));
        var columns = editing.Elements().First();
        Assert.Equal("2*,3*", (string?)columns.Attribute("ColumnDefinitions"));
        var left = columns.Elements().First();
        var right = columns.Elements().Last();
        Assert.Equal("Auto,*", (string?)left.Attribute("RowDefinitions"));
        Assert.Equal("1", (string?)right.Attribute("Grid.Column"));
        Assert.Equal("*,Auto", (string?)right.Attribute("RowDefinitions"));
        Assert.Contains(left.Descendants(), element => (string?)element.Attribute("Text") == "{Binding CustomerSummary}");
        Assert.Contains(left.Descendants(), element => (string?)element.Attribute("Text") == "{Binding EmployeeSummary}");
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.Contains(left.Descendants(), element => (string?)element.Attribute(xaml + "Name") == "ProductsCatalogGrid");
        Assert.Contains(right.Descendants(), element => (string?)element.Attribute(xaml + "Name") == "QuoteItemsGrid");
        Assert.DoesNotContain(document.Descendants(), element =>
            (string?)element.Attribute("Text") is "Cliente" or "Vendedor" or "Lançamento do orçamento");
        AssertFooter(document, "SaveCommand");
    }

    [Fact]
    public void Quote_product_search_is_embedded_and_back_icon_has_standard_size()
    {
        var document = ReadXaml("Views/QuotesView.axaml");
        var search = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField"
            && (string?)element.Attribute("Text") == "{Binding ProductSearchText, Mode=TwoWay}");
        Assert.Equal("{Binding SearchProductsCommand}", (string?)search.Attribute("SearchCommand"));
        Assert.Equal("{Binding ClearProductsCommand}", (string?)search.Attribute("ClearCommand"));
        Assert.Equal("Pesquisar produtos e serviços", (string?)search.Attribute("PlaceholderText"));
        Assert.False(string.IsNullOrWhiteSpace((string?)search.Attribute("SearchHelp")));
        var back = Assert.Single(document.Descendants(), element =>
            (string?)element.Attribute("Command") == "{Binding CancelCommand}");
        Assert.Equal("36", (string?)back.Attribute("Height"));
        var classes = ((string?)back.Attribute("Classes") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("field-action", classes);
        Assert.Contains("decor-action", classes);
        var icon = Assert.Single(back.Elements());
        Assert.Equal("16", (string?)icon.Attribute("Width"));
        Assert.Equal("16", (string?)icon.Attribute("Height"));
        Assert.Equal("{DynamicResource DecorDangerBrush}", (string?)icon.Attribute("Fill"));
        var total = Assert.Single(document.Descendants(), element =>
            (string?)element.Attribute("Background") == "#0066CC");
        Assert.All(total.Descendants().Where(element => element.Name.LocalName == "TextBlock"),
            element => Assert.Equal("White", (string?)element.Attribute("Foreground")));
    }

    private static XElement AssertIdentityHeader(XElement code, XElement status)
    {
        var identity = code.Parent!.Parent!;
        Assert.Same(identity, status.Parent!.Parent);
        Assert.Equal("Grid", identity.Name.LocalName);
        Assert.Equal("Auto,*,Auto", (string?)identity.Attribute("ColumnDefinitions"));
        Assert.Null(identity.Attribute("RowDefinitions"));
        Assert.Null(code.Parent.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)status.Parent.Attribute("Grid.Column"));
        var border = identity.Parent!;
        Assert.Equal("Border", border.Name.LocalName);
        Assert.Equal("{DynamicResource DecorSurfaceRaisedBrush}", (string?)border.Attribute("Background"));
        Assert.Equal("1", (string?)border.Attribute("BorderThickness"));
        Assert.Same(identity, Assert.Single(border.Elements()));
        Assert.DoesNotContain(identity.Descendants(), element => element.Name.LocalName == "Border");
        Assert.Contains(code.Parent.Elements(), element => (string?)element.Attribute("Text") == "Código");
        return identity;
    }

    private static void AssertRequiredInputWithNormalLabel(XElement input, string text)
    {
        Assert.Contains(input.Attributes(), attribute => attribute.Name.LocalName == "DecorRequiredField.IsRequired" && attribute.Value == "True");
        var label = Assert.Single(input.Parent!.Elements(), element => (string?)element.Attribute("Text") == text);
        Assert.Equal("{DynamicResource DecorFontWeightBase}", (string?)label.Attribute("FontWeight"));
        Assert.DoesNotContain(label.Attributes(), attribute => attribute.Name.LocalName == "DecorRequiredField.IsRequired");
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
        public Task<TResult> ShowDialogAsync<TResult>(Window owner, Window dialog) => throw new InvalidOperationException();
    }

    private sealed class Theme : IThemeService
    {
        public DecorThemeStyle CurrentTheme => DecorThemeStyle.Light;
        public event Action<DecorThemeStyle>? ThemeChanged { add { } remove { } }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetThemeAsync(DecorThemeStyle newTheme) => Task.CompletedTask;
    }
}