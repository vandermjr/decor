using System.ComponentModel;
using System.Reflection;
using System.Xml.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Decor.AvaloniaUI.Controls;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.Application.Tests;

public sealed partial class DecorGridPresentationTests
{
    [Fact]
    public void Quote_notes_update_the_counter_while_typing()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var notes = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding Notes, UpdateSourceTrigger=PropertyChanged}");
        Assert.Equal("{Binding NotesLimit}", (string?)notes.Attribute("MaxLength"));
        Assert.Contains(notes.Parent!.Elements(), element => (string?)element.Attribute("Text") == "{Binding NotesCounter}");
    }

    [Fact]
    public void Quote_listing_search_and_clear_are_embedded_like_the_catalog_search()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var listing = AssertSearchField(document, "SearchText", "SearchCommand", "ClearSearchCommand");
        var catalog = AssertSearchField(document, "ProductSearchText", "SearchProductsCommand", "ClearProductsCommand");
        Assert.Equal("SearchTextBox", (string?)listing.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
        Assert.Equal(listing.Name, catalog.Name);
        Assert.Null(listing.Attribute("KeyDown"));
        Assert.Null(catalog.Attribute("KeyDown"));
    }

    [Fact]
    public void Empty_grid_overlay_preserves_headers_and_pointer_interaction()
    {
        var document = ReadXaml("Controls", "DecorDataGridControl.axaml");
        var grid = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DataGrid"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "InnerDataGrid");
        var overlay = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Panel"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EmptyOverlay");

        Assert.Equal("Column", (string?)grid.Attribute("HeadersVisibility"));
        Assert.Equal("Auto", (string?)grid.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("Transparent", (string?)overlay.Attribute("Background"));
        Assert.Equal("False", (string?)overlay.Attribute("IsHitTestVisible"));
    }

    [Fact]
    public void Shared_search_actions_follow_search_clear_filter_information_order()
    {
        var document = ReadXaml("Controls", "DecorSearchField.axaml");
        var actions = new[] { "SearchButton", "ClearButton", "FilterButton", "InfoButton" }
            .Select(name => Assert.Single(document.Descendants(), element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == name))
            .ToArray();

        Assert.Equal(new[] { "1", "2", "3", "4" }, actions.Select(action => (string?)action.Attribute("Grid.Column")));
        Assert.Equal("False", (string?)actions[2].Attribute("IsEnabled"));
        Assert.Contains(actions[2].Attributes(), attribute => attribute.Name.LocalName.Contains("HideWhenDisabled", StringComparison.Ordinal)
            && attribute.Value == "False");
        Assert.Null(actions[3].Element("Button.Flyout"));
        Assert.Equal("Informações da pesquisa", (string?)actions[3].Attribute("AutomationProperties.Name"));
        Assert.Contains("SearchHelp", (string?)actions[3].Attribute("ToolTip.Tip"));
    }

    [Fact]
    public void Shared_search_runs_while_typing_with_a_short_debounce()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "src", "Decor.AvaloniaUI", "Controls", "DecorSearchField.axaml.cs"));

        Assert.Contains("DispatcherTimer", source);
        Assert.Contains("FromMilliseconds(350)", source);
        Assert.Contains("_searchTimer.Start()", source);
        Assert.Contains("ExecuteSearch()", source);
    }

    [Theory]
    [InlineData("EmployeesView.axaml.cs")]
    [InlineData("QuotesView.axaml.cs")]
    [InlineData("UnitsOfMeasureView.axaml.cs")]
    [InlineData("UsersView.axaml.cs")]
    [InlineData("GroupsView.axaml.cs")]
    public void Primary_listings_do_not_search_automatically_when_opened(string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "src", "Decor.AvaloniaUI", "Views", file));
        if (file == "UsersView.axaml.cs")
            source = source.Split("private async Task OpenEmployeeLookupAsync()", StringSplitOptions.None)[0];
        Assert.DoesNotContain("viewModel.InitializeAsync()", source);
    }

    [Fact]
    public void Security_groups_listing_has_the_shared_search_field()
    {
        var document = ReadXaml("Views", "GroupsView.axaml");
        AssertSearchField(document, "SearchText", "SearchCommand", "ClearSearchCommand");
        var grid = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorDataGridControl"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "RolesGrid");
        Assert.Equal("{Binding Roles}", (string?)grid.Attribute("ItemsSource"));
        Assert.Equal("2", (string?)grid.Parent?.Attribute("Grid.Row"));
    }

    [Fact]
    public void Employee_grid_hides_user_id_and_employee_form_keeps_it_read_only()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var code = File.ReadAllText(Path.Combine(root!.FullName, "src", "Decor.AvaloniaUI", "Views", "EmployeesView.axaml.cs"));
        var view = ReadXaml("Views", "EmployeesView.axaml");

        Assert.DoesNotContain(nameof(EmployeeDTO.UserID), code);
        var userIdField = Assert.Single(view.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding UserIdInput}");
        Assert.Equal("True", (string?)userIdField.Attribute("IsReadOnly"));
    }

    [Fact]
    public void User_form_employee_lookup_uses_read_only_display_and_search_and_clear_actions()
    {
        var document = ReadXaml("Views", "UserFormView.axaml");
        var employeeName = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding EmployeeName}");
        Assert.Equal("True", (string?)employeeName.Attribute("IsReadOnly"));
        Assert.Single(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding ClearEmployeeCommand}");
        Assert.Single(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Click") == "SearchEmployee_Click");
    }

    [Fact]
    public void Generated_state_column_is_always_last()
    {
        var control = new DecorDataGridControl();
        var grid = new DataGrid();
        typeof(DecorDataGridControl).GetField("_innerGrid", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(control, grid);

        control.InitializeColumns(typeof(AdministrativeUserDTO), propertyNames:
            [nameof(AdministrativeUserDTO.IsActive), nameof(AdministrativeUserDTO.EmployeeName), nameof(AdministrativeUserDTO.UserID)]);

        Assert.Equal("Estado", grid.Columns.Last().Header);
    }

    [Fact]
    public void Additional_actions_are_inserted_before_the_state_column()
    {
        var control = new DecorDataGridControl();
        var grid = new DataGrid();
        typeof(DecorDataGridControl).GetField("_innerGrid", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(control, grid);
        control.InitializeColumns(typeof(AdministrativeUserDTO), propertyNames:
            [nameof(AdministrativeUserDTO.UserID), nameof(AdministrativeUserDTO.IsActive)]);
        var action = new DataGridTemplateColumn { Header = "Ação", CanUserSort = false };

        control.AddColumn(action);

        Assert.Same(action, grid.Columns[^2]);
        Assert.Equal("Estado", grid.Columns[^1].Header);
    }

    [Fact]
    public void Quote_listing_exposes_creation_date_from_the_quote_dto()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root!.FullName, "Decor.sln"))) root = root.Parent;
        var code = File.ReadAllText(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "QuotesView.axaml.cs"));

        Assert.Contains(nameof(QuoteListItem.CreatedAt), code);
        Assert.Contains(nameof(QuoteDTO.CreatedAt), code);
        Assert.Equal("Data de criação", typeof(QuoteDTO).GetProperty(nameof(QuoteDTO.CreatedAt))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>().Single().GetName());
    }

    [Fact]
    public void Quote_listing_has_no_delete_action_or_confirmation()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding DeleteCommand}"
            || (string?)element.Attribute("IsVisible") == "{Binding ShowDeleteConfirmation}");
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Text") == "Excluir");
    }

    [Theory]
    [InlineData("ID", "Código")]
    [InlineData("ID da Marca", "Código da Marca")]
    [InlineData("Id do Usuário", "Código do Usuário")]
    [InlineData("Descrição", "Descrição")]
    [InlineData("Identity", "Identity")]
    [InlineData("Código de Barras", "EAN")]
    [InlineData("Código de barras", "EAN")]
    public void Visible_ids_are_normalized_without_changing_other_words(string label, string expected)
    {
        Assert.Equal(expected, DecorGridHeader.NormalizeLabel(label));
    }

    [Theory]
    [InlineData("Código")]
    [InlineData("ID da Marca")]
    [InlineData("Código do Funcionário")]
    public void Code_headers_share_the_catalog_icon(string label)
    {
        Assert.Equal(DecorIconId.Common.Code, DecorGridHeader.GetIconId(label));
    }

    [Fact]
    public void Status_has_an_icon_and_ordinary_headers_do_not()
    {
        Assert.Equal(DecorIconId.Common.Status, DecorGridHeader.GetIconId("Estado"));
        Assert.Equal(DecorIconId.Common.Barcode, DecorGridHeader.GetIconId("Código de barras"));
        Assert.Equal(DecorIconId.Common.Barcode, DecorGridHeader.GetIconId("EAN"));
        Assert.Equal(default, DecorGridHeader.GetIconId("Nome"));
    }

    [Fact]
    public void Generated_templates_keep_numeric_sort_paths_and_header_space()
    {
        var control = new DecorDataGridControl();
        var grid = new DataGrid();
        typeof(DecorDataGridControl).GetField("_innerGrid", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(control, grid);

        control.InitializeColumns(typeof(EmployeeDTO));

        var code = Assert.Single(grid.Columns, column => column.SortMemberPath == nameof(EmployeeDTO.EmployeeID));
        Assert.IsType<DataGridTemplateColumn>(code);
        Assert.IsType<DecorGridHeader>(code.HeaderTemplate);
        Assert.Equal("Código do Funcionário", code.Header);
        Assert.Equal(DataGridLengthUnitType.Auto, code.Width.UnitType);
        Assert.True(code.MinWidth >= 130);
        var status = Assert.Single(grid.Columns, column => column.SortMemberPath == nameof(EmployeeDTO.IsActive));
        Assert.Equal("Estado", status.Header);
        Assert.True(status.MinWidth >= 130);
        Assert.All(grid.Columns, column => Assert.False(string.IsNullOrEmpty(column.SortMemberPath)));
    }

    [Fact]
    public void Additional_action_column_remains_last_without_duplicates_after_reinitialization()
    {
        var control = new DecorDataGridControl();
        var grid = new DataGrid();
        typeof(DecorDataGridControl).GetField("_innerGrid", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(control, grid);
        control.InitializeColumns(typeof(BrandDTO));
        var generatedCount = grid.Columns.Count;
        var action = new DataGridTemplateColumn { Header = "Actions", CanUserSort = false };

        control.AddColumn(action);
        Assert.Same(action, grid.Columns.Last());
        Assert.Equal(generatedCount + 1, grid.Columns.Count);

        for (var iteration = 0; iteration < 2; iteration++)
        {
            control.InitializeColumns(typeof(BrandDTO));
            control.AddColumn(action);

            Assert.Equal(generatedCount + 1, grid.Columns.Count);
            Assert.Same(action, Assert.Single(grid.Columns, column => ReferenceEquals(column, action)));
            Assert.Same(action, grid.Columns.Last());
        }
    }

    [Theory]
    [InlineData(ListSortDirection.Ascending, 2, 10, 100)]
    [InlineData(ListSortDirection.Descending, 100, 10, 2)]
    public void Native_collection_sorts_codes_numerically(ListSortDirection direction, int first, int second, int third)
    {
        var items = new[] { new BrandDTO(10, "B"), new BrandDTO(100, "C"), new BrandDTO(2, "A") };
        var collection = new DataGridCollectionView(items);
        var grid = new DataGrid { ItemsSource = collection, CanUserSortColumns = true };
        var column = new DataGridTemplateColumn { Header = "Código", SortMemberPath = nameof(BrandDTO.BrandID) };
        grid.Columns.Add(column);
        var headerCell = (DataGridColumnHeader)typeof(DataGridColumn)
            .GetProperty("HeaderCell", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(column)!;

        collection.SortDescriptions.Add(DataGridSortDescription.FromPath(column.SortMemberPath, direction));
        var updateHeader = typeof(DataGridColumnHeader)
            .GetMethod("UpdatePseudoClasses", BindingFlags.NonPublic | BindingFlags.Instance)!;
        updateHeader.Invoke(headerCell, null);

        Assert.Equal(new[] { first, second, third }, collection.Cast<BrandDTO>().Select(item => item.BrandID));
        Assert.Contains(direction == ListSortDirection.Ascending ? ":sortascending" : ":sortdescending", headerCell.Classes);
        collection.SortDescriptions[0] = collection.SortDescriptions[0].SwitchSortDirection();
        updateHeader.Invoke(headerCell, null);
        Assert.Equal(new[] { third, second, first }, collection.Cast<BrandDTO>().Select(item => item.BrandID));
        Assert.Contains(direction == ListSortDirection.Ascending ? ":sortdescending" : ":sortascending", headerCell.Classes);
        collection.SortDescriptions.Clear();
        updateHeader.Invoke(headerCell, null);
        Assert.Empty(collection.SortDescriptions);
        Assert.DoesNotContain(":sortascending", headerCell.Classes);
        Assert.DoesNotContain(":sortdescending", headerCell.Classes);
    }

    [Theory]
    [InlineData("BrandsView.axaml")]
    [InlineData("CustomersView.axaml")]
    [InlineData("SuppliersView.axaml")]
    [InlineData("QuotesView.axaml")]
    [InlineData("SalesView.axaml")]
    [InlineData("ProductsView.axaml")]
    [InlineData("EmployeesView.axaml")]
    [InlineData("UsersView.axaml")]
    [InlineData("PermissionsView.axaml")]
    [InlineData("GroupsView.axaml")]
    public void Search_buttons_use_shared_accessible_style_and_only_the_search_icon(string file)
    {
        var document = ReadXaml("Views", file);
        var field = AssertSearchField(document, "SearchText", "SearchCommand", "ClearSearchCommand");
        Assert.Equal(file == "PermissionsView.axaml" ? null : "SearchTextBox",
            (string?)field.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
        var shared = ReadXaml("Controls", "DecorSearchField.axaml");
        var search = Assert.Single(shared.Descendants(), element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "SearchButton");
        Assert.Equal("search-action", (string?)search.Attribute("Classes"));
        Assert.Equal("Pesquisar", (string?)search.Attribute("ToolTip.Tip"));
        Assert.Equal("Pesquisar", (string?)search.Attribute("AutomationProperties.Name"));
        Assert.DoesNotContain(search.Descendants(), element => element.Name.LocalName == "TextBlock");
        var icon = Assert.Single(search.Elements());
        Assert.Equal("PathIcon", icon.Name.LocalName);
        Assert.Equal("search-icon", (string?)icon.Attribute("Classes"));
        var style = Assert.Single(shared.Descendants(), element => (string?)element.Attribute("Selector") == "PathIcon.search-icon");
        Assert.Contains(style.Descendants().Attributes(), attribute => attribute.Value.Contains("Actions.Search", StringComparison.Ordinal));
    }

    [Fact]
    public void Shared_styles_keep_accessibility_dimensions_and_native_sort_states()
    {
        var document = ReadXaml("", "App.axaml");
        var styles = document.Descendants().Where(element => element.Name.LocalName == "Style").ToArray();
        var search = Assert.Single(styles, style => (string?)style.Attribute("Selector") == "Button.search-button");
        AssertSetter(search, "ToolTip.Tip", "Pesquisar");
        AssertSetter(search, "AutomationProperties.Name", "Pesquisar");
        AssertSetter(search, "Width", "36");
        AssertSetter(search, "Height", "32");
        var clear = Assert.Single(styles, style => (string?)style.Attribute("Selector") == "Button.clear-search-button");
        AssertSetter(clear, "ToolTip.Tip", "Limpar pesquisa");
        AssertSetter(clear, "AutomationProperties.Name", "Limpar pesquisa");
        foreach (var selector in new[] { "", ":sortascending", ":sortdescending" })
        {
            var style = Assert.Single(styles, style => (string?)style.Attribute("Selector")
                == $"DataGridColumnHeader{selector} /template/ Path#SortIcon");
            AssertSetter(style, "IsVisible", "False");
        }
    }

    [Theory]
    [InlineData("BrandsView.axaml")]
    [InlineData("CustomersView.axaml")]
    [InlineData("SuppliersView.axaml")]
    [InlineData("QuotesView.axaml")]
    [InlineData("SalesView.axaml")]
    [InlineData("ProductsView.axaml")]
    [InlineData("EmployeesView.axaml")]
    [InlineData("UsersView.axaml")]
    [InlineData("PermissionsView.axaml")]
    [InlineData("GroupsView.axaml")]
    public void Searches_can_be_cleared_next_to_the_search_button(string file)
    {
        var document = ReadXaml("Views", file);
        AssertSearchField(document, "SearchText", "SearchCommand", "ClearSearchCommand");
        var shared = ReadXaml("Controls", "DecorSearchField.axaml");
        var search = Assert.Single(shared.Descendants(), element => (string?)element.Attribute("Command") == "{Binding SearchActionCommand, ElementName=Root}");
        var clear = Assert.Single(shared.Descendants(), element => (string?)element.Attribute("Command") == "{Binding ClearActionCommand, ElementName=Root}");
        Assert.Equal("search-action", (string?)clear.Attribute("Classes"));
        Assert.Equal("{Binding HasClearCommand, ElementName=Root}", (string?)clear.Attribute("IsVisible"));
        Assert.Equal("Limpar pesquisa", (string?)clear.Attribute("AutomationProperties.Name"));
        Assert.Same(search.Parent, clear.Parent);
        Assert.Equal(int.Parse((string?)search.Attribute("Grid.Column") ?? "0") + 1, int.Parse((string?)clear.Attribute("Grid.Column") ?? "0"));
        Assert.Equal("clear-icon", (string?)Assert.Single(clear.Elements()).Attribute("Classes"));
        var style = Assert.Single(shared.Descendants(), element => (string?)element.Attribute("Selector") == "PathIcon.clear-icon");
        Assert.Contains(style.Descendants().Attributes(), attribute => attribute.Value.Contains("Actions.Clear", StringComparison.Ordinal));
    }

    [Fact]
    public void Quote_opening_errors_are_visible_before_the_editor_is_opened()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding ErrorMessage}"
            && element.Ancestors().Any(parent => (string?)parent.Attribute("IsVisible") == "{Binding !IsEditing}")
            && !element.Ancestors().Any(parent => (string?)parent.Attribute("IsVisible") == "{Binding IsEditing}"));
    }

    [Fact]
    public void Quote_editor_is_a_single_entry_screen_with_catalog_tabs_and_footer_actions()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var bindings = document.Descendants().Attributes()
            .Where(attribute => attribute.Name.LocalName == "IsVisible" || attribute.Name.LocalName == "Command")
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.DoesNotContain("{Binding ContinueCommand}", bindings);
        Assert.DoesNotContain("{Binding FinishCommand}", bindings);
        Assert.Contains("{Binding SaveCommand}", bindings);
        Assert.Contains("{Binding GeneratePdfCommand}", bindings);
        Assert.Contains("{Binding CancelQuoteCommand}", bindings);
        Assert.Contains("{Binding ConvertToOrderCommand}", bindings);
        Assert.Equal(new[] { "Produtos", "Serviços" }, document.Descendants()
            .Where(element => element.Name.LocalName == "TabItem")
            .Select(element => (string?)element.Descendants().First(child => child.Name.LocalName == "TextBlock").Attribute("Text")));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "Lançamento do orçamento");
    }

    [Fact]
    public void Quote_entry_uses_numeric_controls_lookup_fields_and_shared_catalog_paging()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var numeric = document.Descendants().Where(element => element.Name.LocalName == "NumericUpDown").ToArray();
        Assert.Equal(3, numeric.Length);
        Assert.All(numeric, element => Assert.Equal("Right", (string?)element.Attribute("HorizontalContentAlignment")));
        Assert.All(numeric, element =>
        {
            Assert.Equal("False", (string?)element.Attribute("AllowSpin"));
            Assert.Equal("False", (string?)element.Attribute("ShowButtonSpinner"));
            Assert.Contains(element.Attributes(), attribute => attribute.Name.LocalName == "QuoteNumericInput.Enabled" && attribute.Value == "True");
        });
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "CalendarDatePicker");
        var calendar = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Calendar"
            && (string?)element.Attribute("SelectedDate") == "{Binding SelectedQuoteDate, Mode=TwoWay}");
        Assert.Equal("QuoteDateCalendar_SelectedDatesChanged", (string?)calendar.Attribute("SelectedDatesChanged"));
        Assert.Contains(calendar.Ancestors(), element => element.Name.LocalName == "Flyout");
        var dateButton = Assert.Single(calendar.Ancestors(), element => element.Name.LocalName == "Button");
        Assert.Equal("field-action", (string?)dateButton.Attribute("Classes"));
        Assert.Contains(dateButton.Descendants().Attributes(), attribute => attribute.Value.Contains("DecorIconId+Common.Calendar", StringComparison.Ordinal));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TextBox"
            && (string?)element.Attribute("Text") == "{Binding Notes, UpdateSourceTrigger=PropertyChanged}"
            && (string?)element.Attribute("MaxLength") == "{Binding NotesLimit}");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding CatalogPreviousCommand}");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding CatalogNextCommand}");
        Assert.All(document.Descendants().Where(element => element.Name.LocalName == "TabItem"), tab =>
            Assert.Contains(tab.Descendants().Attributes(), attribute => attribute.Value.Contains("DecorIconId", StringComparison.Ordinal)));
        foreach (var binding in new[] { "{Binding CustomerSummary}", "{Binding EmployeeSummary}" })
            Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TextBox"
                && (string?)element.Attribute("Text") == binding && (string?)element.Attribute("IsReadOnly") == "True");
        Assert.Equal(typeof(DateTime?), typeof(QuotesViewModel).GetProperty(nameof(QuotesViewModel.SelectedQuoteDate))!.PropertyType);
    }

    [Fact]
    public void Quote_header_keeps_identity_on_one_row_and_parties_aligned_with_the_catalog()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var back = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding CancelCommand}");
        var identity = back.Parent!;
        Assert.Equal("36,*,*,90", (string?)identity.Attribute("ColumnDefinitions"));
        Assert.Null(identity.Attribute("RowDefinitions"));
        Assert.All(identity.Elements(), element => Assert.Null(element.Attribute("Grid.Row")));
        Assert.Equal("Bottom", (string?)back.Attribute("VerticalAlignment"));
        Assert.Equal("36", (string?)back.Attribute("Height"));
        var header = identity.Parent!;
        Assert.Equal("8", (string?)header.Attribute("RowSpacing"));
        Assert.Equal("10", (string?)header.Parent!.Attribute("Padding"));
        Assert.Equal("8", (string?)header.Parent.Parent!.Attribute("RowSpacing"));
        var parties = Assert.Single(header.Elements(), element => (string?)element.Attribute("Grid.Row") == "1");
        Assert.Equal("Auto,Auto,Auto", (string?)parties.Attribute("RowDefinitions"));
        Assert.Null(parties.Attribute("ColumnDefinitions"));
        foreach (var binding in new[] { "{Binding CustomerSummary}", "{Binding EmployeeSummary}" })
        {
            var field = Assert.Single(parties.Descendants(), element => (string?)element.Attribute("Text") == binding);
            var panel = field.Ancestors().First(element => element.Name.LocalName == "Border");
            Assert.Same(parties, panel.Parent);
            Assert.Null(panel.Attribute("Grid.Column"));
            Assert.Null(panel.Attribute("Grid.ColumnSpan"));
            Assert.Null(panel.Attribute("Margin"));
            Assert.Equal(binding.Contains("Customer", StringComparison.Ordinal) ? null : "1", (string?)panel.Attribute("Grid.Row"));
            Assert.Equal("36", (string?)field.Parent!.Parent!.Attribute("Height"));
        }
        var origin = Assert.Single(parties.Elements(), element => (string?)element.Attribute("Grid.Row") == "2");
        Assert.Equal("Auto,*", (string?)origin.Attribute("ColumnDefinitions"));
        Assert.Contains(origin.Elements(), element => element.Name.LocalName == "ComboBox"
            && (string?)element.Attribute("Grid.Column") == "1");
    }

    [Fact]
    public void Quote_summary_is_below_items_with_full_width_notes_after_the_totals()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var items = Assert.Single(document.Descendants(), element => (string?)element.Attribute(
            XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuoteItemsGrid");
        var right = items.Parent!.Parent!;
        Assert.Equal("*,Auto", (string?)right.Attribute("RowDefinitions"));
        Assert.All(right.Elements(), element => Assert.Equal("Border", element.Name.LocalName));
        var summary = Assert.Single(right.Elements(), element => (string?)element.Attribute("Grid.Row") == "1");
        Assert.Equal("230", (string?)summary.Attribute("MaxHeight"));
        Assert.Equal("10", (string?)summary.Attribute("Padding"));
        Assert.Equal((string?)items.Parent.Attribute("Background"), (string?)summary.Attribute("Background"));
        var total = Assert.Single(summary.Descendants(), element => (string?)element.Attribute("Text") == "Total");
        var totalValue = Assert.Single(total.Parent!.Elements(), element => (string?)element.Attribute("Grid.Column") == "1");
        foreach (var text in new[] { total, totalValue })
        {
            Assert.Null(text.Attribute("FontSize"));
            Assert.Equal("White", (string?)text.Attribute("Foreground"));
        }
        Assert.Equal("#0066CC", (string?)total.Parent.Parent!.Attribute("Background"));
        var notes = Assert.Single(summary.Descendants(), element => (string?)element.Attribute("Text") == "{Binding Notes, UpdateSourceTrigger=PropertyChanged}");
        Assert.Equal("60", (string?)notes.Attribute("MinHeight"));
        Assert.Equal("80", (string?)notes.Attribute("MaxHeight"));
        Assert.Equal("{Binding NotesLimit}", (string?)notes.Attribute("MaxLength"));
        var separator = notes.Parent!.Parent!;
        Assert.Equal("0,1,0,0", (string?)separator.Attribute("BorderThickness"));
        Assert.Equal("1", (string?)separator.Attribute("Grid.Row"));
        Assert.Same(total.Parent.Parent.Parent!.Parent, separator.Parent);
        Assert.Null(separator.Attribute("Grid.Column"));
        Assert.Contains(separator.Descendants(), element => (string?)element.Attribute("Text") == "{Binding NotesCounter}");
    }

    [Fact]
    public void Quote_sale_entry_has_one_cart_action_and_catalog_uses_shared_footer()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var sale = Assert.Single(document.Descendants(), element => element.Name.LocalName == "NumericUpDown"
            && (string?)element.Attribute("Value") == "{Binding SaleValue}");
        Assert.Contains(sale.Parent!.Parent!.Parent!.Elements(), element => (string?)element.Attribute("Text") == "Valor de Venda");
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Value") == "{Binding UnitPriceValue}");
        var add = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding SaveLineCommand}");
        Assert.Equal("1", (string?)add.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)add.Attribute("Grid.ColumnSpan"));
        Assert.Equal("Right", (string?)add.Attribute("HorizontalAlignment"));
        Assert.Contains(add.Descendants(), element => (string?)element.Attribute("Text") == "Adicionar item");
        Assert.Contains(add.Descendants().Attributes(), attribute => attribute.Value.Contains("DecorIconId+Actions.Cart", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding NewLineCommand}");
        var quantity = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Value") == "{Binding QuantityValue}");
        Assert.Equal("130,*", (string?)quantity.Parent!.Parent!.Parent!.Parent!.Attribute("ColumnDefinitions"));
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Selector") == "Button.catalog-page"
            || (string?)element.Attribute("Classes") == "catalog-page");
        foreach (var command in new[] { "{Binding CatalogPreviousCommand}", "{Binding CatalogNextCommand}" })
            Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Command") == command);
        var entry = quantity.Parent!.Parent!.Parent!.Parent!;
        Assert.Equal("2", (string?)entry.Attribute("Grid.Row"));
        Assert.Equal("Auto,*,Auto", (string?)entry.Parent!.Attribute("RowDefinitions"));
        var catalogs = document.Descendants().Where(element => element.Name.LocalName == "DecorDataGridControl"
            && ((string?)element.Attribute("ItemsSource") is "{Binding CatalogProducts}" or "{Binding CatalogServices}")).ToArray();
        Assert.Equal(2, catalogs.Length);
        Assert.All(catalogs, catalog => Assert.Equal("{Binding CatalogPagination}", (string?)catalog.Attribute("PaginationSource")));
    }

    [Fact]
    public void Quote_lookup_uses_existing_views_inside_a_select_cancel_modal()
    {
        var quote = ReadXaml("Views", "QuotesView.axaml");
        var selectionWindow = ReadXaml("Views", "LookupSelectionWindow.axaml");
        var quoteTextBindings = quote.Descendants().Where(element => element.Name.LocalName == "TextBox")
            .Select(element => (string?)element.Attribute("Text")).ToArray();

        Assert.DoesNotContain("{Binding CustomerSearchText}", quoteTextBindings);
        Assert.DoesNotContain("{Binding EmployeeSearchText}", quoteTextBindings);
        Assert.DoesNotContain("{Binding PartnerSearchText}", quoteTextBindings);
        Assert.Contains(quote.Descendants(), element => element.Name.LocalName == "Button" && (string?)element.Attribute("Click") == "SearchCustomer_Click");
        Assert.Contains(quote.Descendants(), element => element.Name.LocalName == "Button" && (string?)element.Attribute("Click") == "SearchEmployee_Click");
        Assert.Contains(quote.Descendants(), element => element.Name.LocalName == "DecorDataGridControl" && (string?)element.Attribute("ItemsSource") == "{Binding CatalogProducts}");
        Assert.Contains(selectionWindow.Descendants(), element => element.Name.LocalName == "ContentControl"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "LookupContent");
        Assert.Contains(selectionWindow.Descendants(), element => element.Name.LocalName == "Button" && (string?)element.Attribute("Content") == "Selecionar");
        Assert.Contains(selectionWindow.Descendants(), element => element.Name.LocalName == "Button" && (string?)element.Attribute("Content") == "Cancelar");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root!.FullName, "Decor.sln"))) root = root.Parent;
        var code = File.ReadAllText(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "QuotesView.axaml.cs"));
        foreach (var view in new[] { "CustomersView", "EmployeesView", "PartnersView", "ProductsView", "ServicesView" })
            Assert.Contains($"new {view}(viewModel)", code);
    }

    [Fact]
    public void Reloaded_results_show_the_query_sort_and_empty_results_clear_it()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<BrandDTO> { new(2, "B"), new(1, "A") };
        var grid = new DataGrid { ItemsSource = items };
        grid.Columns.Add(new DataGridTextColumn { Header = "Nome", SortMemberPath = nameof(BrandDTO.BrandName) });
        DecorGridSorting.SetDefaultSortMemberPath(grid, nameof(BrandDTO.BrandName));

        grid.CollectionView.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(BrandDTO.BrandID), ListSortDirection.Descending));
        DecorGridSorting.ApplyDefault(grid);
        var sort = Assert.Single(grid.CollectionView.SortDescriptions);
        Assert.Equal(nameof(BrandDTO.BrandName), sort.PropertyPath);
        Assert.Equal(ListSortDirection.Ascending, sort.Direction);

        items.Clear();
        DecorGridSorting.ApplyDefault(grid);
        Assert.Empty(grid.CollectionView.SortDescriptions);
    }

    [Theory]
    [InlineData("ProductsView", "ProductsGrid")]
    [InlineData("BrandsView", "BrandsGrid")]
    [InlineData("EmployeesView", "EmployeesGrid")]
    [InlineData("UsersView", "UsersGrid")]
    [InlineData("GroupsView", "RolesGrid")]
    public void Listing_views_share_the_decor_grid_with_value_match_and_query_sort(string view, string grid)
    {
        var document = ReadXaml("Views", view + ".axaml");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "DataGrid");
        var control = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorDataGridControl");
        Assert.Equal(grid, (string?)control.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
        Assert.False(string.IsNullOrEmpty((string?)control.Attribute("DefaultSortMemberPath")));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root!.FullName, "Decor.sln"))) root = root.Parent;
        var codeBehind = File.ReadAllText(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", view + ".axaml.cs"));
        Assert.Contains($"{grid}.InitializeColumns(", codeBehind);
        Assert.Contains($"{grid}.ValueMatchChanged", codeBehind);
    }

    [Fact]
    public void Selected_columns_follow_the_requested_order_with_state_label()
    {
        var control = new DecorDataGridControl();
        var grid = new DataGrid();
        typeof(DecorDataGridControl).GetField("_innerGrid", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(control, grid);

        control.InitializeColumns(typeof(AdministrativeUserDTO), name => name switch
            {
                nameof(AdministrativeUserDTO.UserID) => "Código",
                nameof(AdministrativeUserDTO.PresentationName) => "Usuário",
                _ => name
            },
            propertyNames: [nameof(AdministrativeUserDTO.UserID), nameof(AdministrativeUserDTO.PresentationName), nameof(AdministrativeUserDTO.IsActive)]);

        Assert.Equal(["Código", "Usuário", "Estado"], grid.Columns.Select(column => (string)column.Header!).ToArray());
        Assert.Equal(nameof(AdministrativeUserDTO.UserID), DecorGridSorting.GetDefaultSortMemberPath(grid));
    }

    [Fact]
    public void Headers_center_content_and_sort_indicators_without_a_windowing_platform()
    {
        var panel = Assert.IsType<Grid>(new DecorGridHeader().Build("Nome"));
        Assert.Equal(Avalonia.Layout.VerticalAlignment.Center, panel.VerticalAlignment);
        Assert.Equal(2, panel.Children.Count);
        var text = Assert.IsType<TextBlock>(panel.Children[0]);
        Assert.Equal(Avalonia.Layout.VerticalAlignment.Center, text.VerticalAlignment);
        var sort = Assert.IsType<PathIcon>(panel.Children[1]);
        Assert.Equal(1, Grid.GetColumn(sort));
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Right, sort.HorizontalAlignment);
        Assert.Equal(0, sort.Opacity);
        var styles = ReadXaml("", "App.axaml").Descendants().Where(element => element.Name.LocalName == "Style").ToArray();
        AssertSetter(Assert.Single(styles, element => (string?)element.Attribute("Selector") == "DataGridColumnHeader"), "VerticalContentAlignment", "Center");
        AssertSetter(Assert.Single(styles, element => (string?)element.Attribute("Selector") == "DataGridColumnHeader /template/ ContentPresenter"), "VerticalAlignment", "Center");
    }

    [Fact]
    public void Declarative_and_added_columns_receive_shared_headers_and_default_sort()
    {
        var control = new DecorDataGridControl();
        var grid = new DataGrid();
        typeof(DecorDataGridControl).GetField("_innerGrid", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(control, grid);
        var name = new DataGridTextColumn { Header = "Nome", Binding = new Avalonia.Data.Binding(nameof(BrandDTO.BrandName)) };
        control.Columns.Add(name);
        typeof(DecorDataGridControl).GetMethod("ConfigureColumnPresentation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, null);
        Assert.IsType<DecorGridHeader>(name.HeaderTemplate);
        Assert.Equal(nameof(BrandDTO.BrandName), name.SortMemberPath);

        var code = new DataGridTextColumn { Header = "Código", Binding = new Avalonia.Data.Binding(nameof(BrandDTO.BrandID)) };
        control.AddColumn(code);
        typeof(DecorDataGridControl).GetMethod("ConfigureColumnPresentation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, null);
        Assert.IsType<DecorGridHeader>(code.HeaderTemplate);
        Assert.Equal(nameof(BrandDTO.BrandID), DecorGridSorting.GetDefaultSortMemberPath(grid));

        control.DefaultSortMemberPath = nameof(BrandDTO.BrandName);
        Assert.Equal(nameof(BrandDTO.BrandName), DecorGridSorting.GetDefaultSortMemberPath(grid));
    }

    private static void AssertSetter(XElement style, string property, string value) =>
        Assert.Contains(style.Elements(), setter => (string?)setter.Attribute("Property") == property
            && (string?)setter.Attribute("Value") == value);

    [Fact]
    public void Quote_embedded_buttons_stretch_inside_the_field_border()
    {
        var document = ReadXaml("Views", "QuotesView.axaml");
        var style = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "Button.field-action, Button.search-button, Button.clear-search-button");
        AssertSetter(style, "Height", "NaN");
        AssertSetter(style, "MinHeight", "0");
        AssertSetter(style, "MinWidth", "0");
        AssertSetter(style, "Margin", "0");
        AssertSetter(style, "Padding", "0");
        AssertSetter(style, "VerticalAlignment", "Stretch");
        var buttons = document.Descendants().Where(element => element.Name.LocalName == "Button"
            && element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "Border"
                && (string?)ancestor.Attribute("Height") == "36")).ToArray();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.Null(button.Attribute("Height")));

        var action = new Button { Width = 36, MinHeight = 0, MinWidth = 0, Padding = new Avalonia.Thickness(0) };
        var field = new Border { Height = 36, BorderThickness = new Avalonia.Thickness(1), Child = action };
        field.Measure(new Avalonia.Size(100, 36));
        field.Arrange(new Avalonia.Rect(0, 0, 100, 36));
        Assert.Equal(34, action.Bounds.Height);
        Assert.True(action.Bounds.Bottom <= field.Bounds.Height - field.BorderThickness.Bottom);
    }

    [Fact]
    public void State_icon_belongs_to_the_header()
    {
        Assert.Equal(DecorIconId.Common.Status, DecorGridHeader.GetIconId("Estado"));
    }

    [Fact]
    public void Shared_listing_state_pages_results_and_hides_status_while_editing()
    {
        var page = new System.Collections.ObjectModel.ObservableCollection<int>();
        var editing = false;
        var listing = new Decor.AvaloniaUI.ViewModels.GridListState<int>(page, () => !editing);

        Assert.True(listing.HasPagination);
        Assert.Equal("Registros encontrados: 0", listing.PaginationStatus);
        Assert.Equal("Página 0 de 0", listing.PaginationPageStatus);
        Assert.All(new[] { listing.FirstPageCommand, listing.PreviousPageCommand, listing.NextPageCommand, listing.LastPageCommand },
            command => Assert.False(command.CanExecute(null)));
        listing.Load(Enumerable.Range(1, 25));
        Assert.Equal(10, page.Count);
        Assert.Equal("Registros encontrados: 25", listing.PaginationStatus);
        Assert.Equal("Página 1 de 3", listing.PaginationPageStatus);
        listing.LastPageCommand.Execute(null);
        Assert.Equal([21, 22, 23, 24, 25], page);
        listing.SelectedPageSize = 25;
        Assert.Equal(25, page.Count);
        listing.SetValueMatch("Nome", 3);
        Assert.Equal("Nome: 3 correspondência(s)", listing.StatusSecondary);

        editing = true;
        Assert.False(listing.HasPagination);
        Assert.Null(listing.PaginationStatus);
        Assert.Null(listing.PaginationPageStatus);
        Assert.Null(listing.StatusSecondary);
        editing = false;
        listing.Clear();
        Assert.Empty(page);
        Assert.True(listing.HasPagination);
        Assert.Equal("Registros encontrados: 0", listing.PaginationStatus);
        Assert.Equal("Página 0 de 0", listing.PaginationPageStatus);
        Assert.Null(listing.StatusSecondary);
        Assert.Equal(25, listing.SelectedPageSize);
        Assert.All(new[] { listing.FirstPageCommand, listing.PreviousPageCommand, listing.NextPageCommand, listing.LastPageCommand },
            command => Assert.False(command.CanExecute(null)));
    }

    [Fact]
    public void Pagination_icons_have_readable_dimensions_and_contrast()
    {
        var buttons = ReadXaml("Controls", "DecorDataGridControl.axaml").Descendants().Where(element =>
            element.Name.LocalName == "Button" && (string?)element.Attribute("Classes") == "grid-page-button").ToArray();
        Assert.Equal(2, buttons.Length);
        Assert.All(buttons, button =>
        {
            var icon = Assert.Single(button.Elements());
            Assert.Equal("PathIcon", icon.Name.LocalName);
            Assert.Equal("16", (string?)icon.Attribute("Width"));
            Assert.Equal("16", (string?)icon.Attribute("Height"));
            Assert.Equal("{DynamicResource DecorTextBrush}", (string?)icon.Attribute("Foreground"));
        });
    }

    [Fact]
    public void Grid_footer_keeps_the_approved_paging_order_and_arrowless_scroll_track()
    {
        var document = ReadXaml("Controls", "DecorDataGridControl.axaml");
        var name = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        var layout = Assert.Single(document.Descendants(), element => (string?)element.Attribute(name) == "PaginationLayout");
        Assert.Equal("*,*", (string?)layout.Attribute("ColumnDefinitions"));
        var controls = Assert.Single(layout.Elements());
        Assert.Equal(["TextBlock", "ComboBox", "Button", "TextBlock", "Button"], controls.Elements().Select(element => element.Name.LocalName));
        var children = controls.Elements().ToArray();
        Assert.Equal("Registros por página:", (string?)children[0].Attribute("Text"));
        Assert.Contains("SelectedPageSize", (string?)children[1].Attribute("SelectedItem"));
        Assert.Contains("PreviousPageCommand", (string?)children[2].Attribute("Command"));
        Assert.Contains("PaginationPageStatus", (string?)children[3].Attribute("Text"));
        Assert.Contains("NextPageCommand", (string?)children[4].Attribute("Command"));
        var theme = Assert.Single(document.Descendants(), element => (string?)element.Attribute(XName.Get("Key", name.NamespaceName)) == "GridScrollBarTheme");
        Assert.DoesNotContain(theme.Descendants(), element => element.Name.LocalName is "Path" or "PathIcon");
        Assert.Contains(theme.Descendants(), element => (string?)element.Attribute("Name") == "PART_PageUpButton");
        Assert.Contains(theme.Descendants(), element => (string?)element.Attribute("Name") == "PART_PageDownButton");
        Assert.Contains(theme.Descendants(), element => element.Name.LocalName == "Border" && (string?)element.Attribute("CornerRadius") == "5");
    }

    [Fact]
    public void Status_bar_contains_counts_without_paging_controls()
    {
        var document = ReadXaml("", "MainWindow.axaml");
        var status = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Classes") == "status-bar");
        Assert.DoesNotContain(status.Descendants(), element => element.Name.LocalName is "Button" or "ComboBox" or "Popup");
        Assert.Contains(status.Descendants(), element => (string?)element.Attribute("Text") == "{Binding StatusSource.PaginationStatus}");
        Assert.Contains(status.Descendants(), element => (string?)element.Attribute("Text") == "{Binding StatusSource.StatusSecondary}");
        var message = Assert.Single(status.Descendants(), element => (string?)element.Attribute("Text") == "{Binding StatusSource.StatusMessage}");
        Assert.Equal("{Binding !IsPaginationVisible}", (string?)message.Attribute("IsVisible"));
    }

    [Theory]
    [InlineData("BrandsView.axaml", "BrandsGrid")]
    [InlineData("CustomersView.axaml", "CustomersGrid")]
    [InlineData("SuppliersView.axaml", "SuppliersGrid")]
    [InlineData("EmployeesView.axaml", "EmployeesGrid")]
    [InlineData("GroupsView.axaml", "RolesGrid")]
    [InlineData("ProductsView.axaml", "ProductsGrid")]
    [InlineData("QuotesView.axaml", "QuotesGrid")]
    [InlineData("SalesView.axaml", "SalesGrid")]
    [InlineData("UsersView.axaml", "UsersGrid")]
    public void Primary_listing_and_quote_catalogs_receive_separate_pagination_sources(string file, string gridName)
    {
        var grids = ReadXaml("Views", file).Descendants().Where(element => element.Name.LocalName == "DecorDataGridControl").ToArray();
        var paginated = Assert.Single(grids, element => (string?)element.Attribute("PaginationSource") == "{Binding}");
        Assert.Equal(gridName, (string?)paginated.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
        Assert.Equal("{Binding}", (string?)paginated.Attribute("PaginationSource"));
        foreach (var contextual in grids.Where(element => !ReferenceEquals(element, paginated)))
        {
            var source = (string?)contextual.Attribute("ItemsSource");
            Assert.Equal(source is "{Binding CatalogProducts}" or "{Binding CatalogServices}" ? "{Binding CatalogPagination}" : null,
                (string?)contextual.Attribute("PaginationSource"));
        }
    }

    private static XElement AssertSearchField(XDocument document, string text, string searchCommand, string clearCommand)
    {
        var field = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField"
            && (string?)element.Attribute("Text") == $"{{Binding {text}, Mode=TwoWay}}");
        Assert.Equal($"{{Binding {searchCommand}}}", (string?)field.Attribute("SearchCommand"));
        Assert.Equal($"{{Binding {clearCommand}}}", (string?)field.Attribute("ClearCommand"));
        Assert.False(string.IsNullOrWhiteSpace((string?)field.Attribute("PlaceholderText")));
        Assert.False(string.IsNullOrWhiteSpace((string?)field.Attribute("SearchHelp") ?? DecorSearchField.GenericSearchHelp));
        return field;
    }

    [Fact]
    public void Shared_search_keeps_named_input_help_and_unified_focus_border()
    {
        Assert.Equal(DecorSearchField.GenericSearchHelp, DecorSearchField.SearchHelpProperty.GetMetadata(typeof(DecorSearchField)).DefaultValue);
        var document = ReadXaml("Controls", "DecorSearchField.axaml");
        var name = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        var input = Assert.Single(document.Descendants(), element => (string?)element.Attribute(name) == "SearchInput");
        Assert.Equal("{Binding Text, ElementName=Root, Mode=TwoWay}", (string?)input.Attribute("Text"));
        Assert.Equal("0", (string?)input.Attribute("BorderThickness"));
        var border = input.Parent!.Parent!;
        Assert.Equal("FieldBorder", (string?)border.Attribute(name));
        Assert.Equal("1", (string?)border.Attribute("BorderThickness"));
        Assert.Equal("True", (string?)border.Attribute("ClipToBounds"));
        var focus = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Selector") == "Border#FieldBorder:focus-within");
        AssertSetter(focus, "BorderBrush", "{DynamicResource DecorFieldFocusBrush}");
        var info = Assert.Single(document.Descendants(), element => (string?)element.Attribute(name) == "InfoButton");
        Assert.Same(input.Parent, info.Parent);
        Assert.Equal("4", (string?)info.Attribute("Grid.Column"));
        Assert.Equal("{Binding HasSearchHelp, ElementName=Root}", (string?)info.Attribute("IsVisible"));
        Assert.Equal("{Binding SearchHelp, ElementName=Root}", (string?)info.Attribute("ToolTip.Tip"));
        Assert.Equal("Informações da pesquisa", (string?)info.Attribute("AutomationProperties.Name"));
        Assert.Null(info.Element("Button.Flyout"));
        var icon = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Selector") == "PathIcon.info-icon");
        Assert.Contains(icon.Descendants().Attributes(), attribute => attribute.Value.Contains("Application.Info", StringComparison.Ordinal));
    }

    private static XDocument ReadXaml(string folder, string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        return XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", folder, file));
    }
}