using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Decor.AvaloniaUI.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.Application.Tests;

[Collection("Shared grid pagination")]
public sealed class SharedGridPaginationTests
{
    [Fact]
    public void Control_automatically_pages_the_native_view_without_replacing_the_source()
    {
        var items = new ObservableCollection<Row>(Enumerable.Range(1, 23).Select(value => new Row { Value = value }));
        var control = new DecorDataGridControl { ItemsSource = items };
        var grid = control.FindControl<DataGrid>("InnerDataGrid")!;
        var state = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        Assert.Null(control.PaginationSource);
        Assert.Same(items, control.ItemsSource);
        Assert.Same(items, grid.ItemsSource);
        Assert.Same(grid.CollectionView, state.View);
        Assert.Equal(10, state.View.Count);
        Assert.True(control.FindControl<Border>("PaginationFooter")!.IsVisible);
        grid.SelectedItem = items[3];
        Assert.Same(items[3], control.SelectedItem);
        control.SelectedItem = items[4];
        Assert.Same(items[4], grid.SelectedItem);
        items.Add(new Row { Value = 24 });
        Assert.Equal(24, state.TotalCount);
        state.NextPageCommand.Execute(null);
        Assert.Same(items[10], state.View.Cast<Row>().First());
    }

    [Fact]
    public void Selection_synchronization_preserves_the_external_two_way_binding()
    {
        var items = new ObservableCollection<Row>(Enumerable.Range(1, 5).Select(value => new Row { Value = value }));
        var source = new ContentControl { Content = items[0] };
        var control = new DecorDataGridControl { ItemsSource = items };
        var grid = control.FindControl<DataGrid>("InnerDataGrid")!;
        control.Bind(DecorDataGridControl.SelectedItemProperty,
            new Binding(nameof(ContentControl.Content)) { Source = source, Mode = BindingMode.TwoWay });
        var binding = BindingOperations.GetBindingExpressionBase(control, DecorDataGridControl.SelectedItemProperty)!;
        Assert.NotNull(binding);

        Assert.Same(items[0], grid.SelectedItem);
        grid.SelectedItem = items[1];
        Assert.Same(items[1], control.SelectedItem);
        Assert.Same(items[1], source.Content);
        Assert.Same(binding, BindingOperations.GetBindingExpressionBase(control, DecorDataGridControl.SelectedItemProperty));
        source.Content = items[2];
        binding.UpdateTarget();
        Assert.Same(items[2], control.SelectedItem);
        Assert.Same(items[2], grid.SelectedItem);
        grid.SelectedItem = null;
        Assert.Null(control.SelectedItem);
        Assert.Null(source.Content);
        Assert.Same(binding, BindingOperations.GetBindingExpressionBase(control, DecorDataGridControl.SelectedItemProperty));
        source.Content = items[3];
        binding.UpdateTarget();
        Assert.Same(items[3], grid.SelectedItem);
        source.Content = null;
        binding.UpdateTarget();
        Assert.Null(grid.SelectedItem);
        source.Content = items[4];
        binding.UpdateTarget();
        Assert.Same(items[4], grid.SelectedItem);
        items.Clear();
        Assert.Null(grid.SelectedItem);
        Assert.Null(control.SelectedItem);
        Assert.Null(source.Content);
    }

    [Fact]
    public void Explicit_pagination_is_never_paged_twice_and_can_be_removed_or_replaced()
    {
        var items = new ObservableCollection<int>(Enumerable.Range(1, 25));
        var listing = new GridListState<int>(items, () => true);
        var control = new DecorDataGridControl { PaginationSource = listing, ItemsSource = items };
        var grid = control.FindControl<DataGrid>("InnerDataGrid")!;
        Assert.Same(listing, control.EffectivePaginationSource);
        Assert.Equal(25, Assert.IsType<DataGridCollectionView>(grid.CollectionView).Count);
        control.PaginationSource = null;
        Assert.Equal(10, Assert.IsType<DataGridCollectionView>(grid.CollectionView).Count);
        Assert.Same(grid.CollectionView, Assert.IsType<GridPaginationState>(control.EffectivePaginationSource).View);
        control.PaginationSource = listing;
        Assert.Equal(25, Assert.IsType<DataGridCollectionView>(grid.CollectionView).Count);
        Assert.Same(listing, control.EffectivePaginationSource);
        control.PaginationSource = null;
        var previous = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        control.ItemsSource = new ObservableCollection<int>([99]);
        Assert.Equal(1, Assert.IsType<GridPaginationState>(control.EffectivePaginationSource).TotalCount);
        var notifications = 0;
        previous.PropertyChanged += (_, _) => notifications++;
        items.Add(26);
        Assert.Equal(0, notifications);
        Assert.Equal(1, Assert.IsType<GridPaginationState>(control.EffectivePaginationSource).TotalCount);
    }

    [Fact]
    public void Existing_paged_collection_view_is_reused_and_its_page_size_is_not_reset()
    {
        var view = new DataGridCollectionView(Enumerable.Range(1, 80)) { PageSize = 25 };
        view.MoveToPage(1);
        var control = new DecorDataGridControl { ItemsSource = view };
        var state = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        Assert.Same(view, state.View);
        Assert.Equal(25, state.SelectedPageSize);
        Assert.Equal(2, state.CurrentPage);
        control.PaginationSource = new GridListState<int>(new ObservableCollection<int>(), () => true);
        Assert.Equal(25, view.PageSize);
    }

    [Fact]
    public void Page_size_survives_source_replacement_and_empty_results_update_the_footer()
    {
        var control = new DecorDataGridControl { ItemsSource = new ObservableCollection<int>(Enumerable.Range(1, 60)) };
        var previous = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        previous.SelectedPageSize = 25;
        var items = new ObservableCollection<int>();
        control.ItemsSource = items;
        var state = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        Assert.Equal(25, state.SelectedPageSize);
        Assert.False(control.FindControl<Border>("PaginationFooter")!.IsVisible);
        items.Add(42);
        Assert.True(control.FindControl<Border>("PaginationFooter")!.IsVisible);
        items.Clear();
        Assert.False(control.FindControl<Border>("PaginationFooter")!.IsVisible);
        control.ItemsSource = null;
        Assert.Null(control.EffectivePaginationSource);
    }

    [Fact]
    public void Native_source_replace_move_and_reset_are_observed_without_mutating_the_source()
    {
        var items = new ObservableCollection<int>(Enumerable.Range(1, 12));
        var control = new DecorDataGridControl { ItemsSource = items };
        var state = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        items[0] = 99;
        Assert.Equal(99, state.View.Cast<int>().First());
        items.Move(0, 11);
        Assert.Equal(2, state.View.Cast<int>().First());
        state.LastPageCommand.Execute(null);
        Assert.Equal(new[] { 12, 99 }, state.View.Cast<int>());
        Assert.Equal(12, items.Count);
        items.Clear();
        Assert.Equal(0, state.TotalCount);
        Assert.Empty(state.View.Cast<int>());
        items.Add(7);
        Assert.Equal(7, Assert.Single(state.View.Cast<int>()));
    }

    [Fact]
    public void Paging_does_not_reset_the_default_sort_observers_source()
    {
        var items = new ObservableCollection<Row>(Enumerable.Range(1, 30).Select(value => new Row { Value = value }));
        var control = new DecorDataGridControl { ItemsSource = items };
        control.InitializeColumns(typeof(Row));
        var grid = control.FindControl<DataGrid>("InnerDataGrid")!;
        var state = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        state.View.SortDescriptions.Clear();
        state.View.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Row.Value), ListSortDirection.Descending));
        var sourceResets = 0;
        items.CollectionChanged += (_, args) => sourceResets += args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ? 1 : 0;
        state.NextPageCommand.Execute(null);
        Assert.Equal(0, sourceResets);
        Assert.Same(items, grid.ItemsSource);
        Assert.Equal(Enumerable.Range(11, 10).Reverse(), state.View.Cast<Row>().Select(item => item.Value));
        Assert.Equal(ListSortDirection.Descending, Assert.Single(state.View.SortDescriptions).Direction);
    }

    [Fact]
    public void Editing_uses_the_same_native_view_and_defers_pagination_until_commit()
    {
        var items = new ObservableCollection<Row>(Enumerable.Range(1, 20).Select(value => new Row { Value = value }));
        var control = new DecorDataGridControl { ItemsSource = items, IsReadOnly = false };
        var state = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        var grid = control.FindControl<DataGrid>("InnerDataGrid")!;
        Assert.False(grid.IsReadOnly);
        state.View.EditItem(items[0]);
        items[0].Value = 99;
        Assert.False(state.NextPageCommand.CanExecute(null));
        state.SelectedPageSize = 25;
        Assert.Equal(10, state.SelectedPageSize);
        state.View.CommitEdit();
        Assert.Equal(99, items[0].Value);
        Assert.True(state.NextPageCommand.CanExecute(null));
        state.SelectedPageSize = 25;
        Assert.Equal(20, state.View.Count);
    }

    [Fact]
    public void Native_paging_sorts_the_whole_source_and_preserves_editable_item_identity()
    {
        var items = new ObservableCollection<Row>(Enumerable.Range(1, 23).Reverse().Select(value => new Row { Value = value }));
        var view = new DataGridCollectionView(items);
        using var state = new GridPaginationState(view);
        view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Row.Value), ListSortDirection.Ascending));

        Assert.Equal(Enumerable.Range(1, 10), view.Cast<Row>().Select(item => item.Value));
        state.NextPageCommand.Execute(null);
        Assert.Equal(Enumerable.Range(11, 10), view.Cast<Row>().Select(item => item.Value));
        var row = view.Cast<Row>().First();
        Assert.Same(items.Single(item => item.Value == 11), row);
        row.Value = 111;
        Assert.Equal(111, items.Single(item => ReferenceEquals(item, row)).Value);
        Assert.Equal(23, state.TotalCount);
        Assert.Equal(3, state.TotalPages);
    }

    [Fact]
    public void Native_paging_tracks_mutations_and_clamps_after_last_page_is_removed()
    {
        var items = new ObservableCollection<int>(Enumerable.Range(1, 21));
        using var state = new GridPaginationState(new DataGridCollectionView(items));
        var notifications = 0;
        state.PropertyChanged += (_, _) => notifications++;
        state.LastPageCommand.Execute(null);
        items.Remove(21);
        Assert.Equal(20, state.TotalCount);
        Assert.Equal(2, state.CurrentPage);
        Assert.Equal(Enumerable.Range(11, 10), state.View.Cast<int>());
        items.Add(21);
        Assert.True(state.HasNextPage);
        Assert.True(notifications > 0);
        state.SelectedPageSize = 25;
        Assert.Equal(1, state.CurrentPage);
        Assert.Equal(21, state.View.Count);
        items.Clear();
        Assert.False(state.HasPagination);
        Assert.False(state.NextPageCommand.CanExecute(null));
        items.Add(42);
        Assert.Equal(42, Assert.Single(state.View.Cast<int>()));
    }

    private sealed class Row
    {
        public int Value { get; set; }
    }
}

[CollectionDefinition("Shared grid pagination", DisableParallelization = true)]
public sealed class SharedGridPaginationCollection;