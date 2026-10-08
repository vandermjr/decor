using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Decor.AvaloniaUI.Controls;

/// <summary>
/// Mirrors the query ordering on the grid: every reload sorts by the target column (ascending)
/// and an empty result clears the sort, so the header indicator always matches the data.
/// </summary>
public sealed class DecorGridSorting
{
    private static readonly ConditionalWeakTable<DataGrid, State> States = new();

    public static readonly AttachedProperty<string?> DefaultSortMemberPathProperty =
        AvaloniaProperty.RegisterAttached<DecorGridSorting, DataGrid, string?>("DefaultSortMemberPath");

    static DecorGridSorting()
    {
        DefaultSortMemberPathProperty.Changed.AddClassHandler<DataGrid>((grid, _) => Attach(grid));
    }

    private DecorGridSorting()
    {
    }

    public static string? GetDefaultSortMemberPath(DataGrid grid) => grid.GetValue(DefaultSortMemberPathProperty);

    public static void SetDefaultSortMemberPath(DataGrid grid, string? value) => grid.SetValue(DefaultSortMemberPathProperty, value);

    public static void ApplyDefault(DataGrid grid)
    {
        if (grid.CollectionView?.SortDescriptions is not { } sorts)
            return;

        var path = GetDefaultSortMemberPath(grid);
        var hasItems = grid.ItemsSource is IEnumerable items && items.Cast<object>().Any();
        if (sorts.Count == 1 && hasItems && sorts[0].HasPropertyPath && sorts[0].PropertyPath == path
            && sorts[0].Direction == ListSortDirection.Ascending)
            return;

        sorts.Clear();
        if (hasItems && !string.IsNullOrEmpty(path))
            sorts.Add(DataGridSortDescription.FromPath(path, ListSortDirection.Ascending));
    }

    private static void Attach(DataGrid grid)
    {
        if (States.TryGetValue(grid, out var existing))
        {
            existing.Observe();
            return;
        }

        var state = new State(grid);
        States.Add(grid, state);
        grid.PropertyChanged += (_, args) =>
        {
            if (args.Property == ItemsControl.ItemsSourceProperty)
                state.Observe();
        };
        state.Observe();
    }

    private sealed class State(DataGrid grid)
    {
        private INotifyCollectionChanged? _observed;
        private bool _pending;
        private bool _reloaded = true;

        public void Observe()
        {
            if (!ReferenceEquals(_observed, grid.ItemsSource))
            {
                if (_observed is not null)
                    _observed.CollectionChanged -= OnCollectionChanged;
                _observed = grid.ItemsSource as INotifyCollectionChanged;
                if (_observed is not null)
                    _observed.CollectionChanged += OnCollectionChanged;
            }
            _reloaded = true;
            Schedule();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            // View models reload results with Clear() + Add(); the Reset marks a new query.
            if (args.Action == NotifyCollectionChangedAction.Reset)
                _reloaded = true;
            Schedule();
        }

        private void Schedule()
        {
            if (_pending)
                return;
            _pending = true;
            // Runs after the collection view has processed the whole batch of changes.
            Dispatcher.UIThread.Post(Apply);
        }

        private void Apply()
        {
            _pending = false;
            var hasItems = grid.ItemsSource is IEnumerable items && items.Cast<object>().Any();
            if (!_reloaded && hasItems)
                return;
            _reloaded = false;
            ApplyDefault(grid);
        }
    }
}
