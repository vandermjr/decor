using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Decor.AvaloniaUI.Controls;

namespace Decor.FieldPresentation.Tests;

public sealed class SharedGridSelectionTests
{
    [AvaloniaFact]
    public void Selection_synchronization_preserves_the_external_two_way_binding()
    {
        Assert.True(Dispatcher.UIThread.CheckAccess());
        var items = new ObservableCollection<Row>(Enumerable.Range(1, 5).Select(value => new Row { Value = value }));
        var source = new SelectionSource { Content = items[0] };
        var control = new DecorDataGridControl { ItemsSource = items };
        Assert.Same(Dispatcher.UIThread, control.Dispatcher);
        var grid = control.FindControl<DataGrid>("InnerDataGrid")!;
        using var subscription = control.Bind(DecorDataGridControl.SelectedItemProperty,
            new Binding(nameof(SelectionSource.Content)) { Source = source, Mode = BindingMode.TwoWay });
        var binding = BindingOperations.GetBindingExpressionBase(control, DecorDataGridControl.SelectedItemProperty)!;
        Assert.NotNull(binding);

        Assert.Same(items[0], grid.SelectedItem);
        grid.SelectedItem = items[1];
        Assert.Same(items[1], control.SelectedItem);
        Assert.Same(items[1], source.Content);
        Assert.Same(binding, BindingOperations.GetBindingExpressionBase(control, DecorDataGridControl.SelectedItemProperty));
        source.Content = items[2];
        Assert.Same(items[2], control.SelectedItem);
        Assert.Same(items[2], grid.SelectedItem);
        grid.SelectedItem = null;
        Assert.Null(control.SelectedItem);
        Assert.Null(source.Content);
        Assert.Same(binding, BindingOperations.GetBindingExpressionBase(control, DecorDataGridControl.SelectedItemProperty));
        source.Content = items[3];
        Assert.Same(items[3], grid.SelectedItem);
        source.Content = null;
        Assert.Null(grid.SelectedItem);
        source.Content = items[4];
        Assert.Same(items[4], grid.SelectedItem);
        items.Clear();
        Assert.Null(grid.SelectedItem);
        Assert.Null(control.SelectedItem);
        Assert.Null(source.Content);
    }

    private sealed class SelectionSource : INotifyPropertyChanged
    {
        private object? _content;

        public object? Content
        {
            get => _content;
            set
            {
                if (ReferenceEquals(_content, value))
                    return;
                _content = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class Row
    {
        public int Value { get; set; }
    }
}