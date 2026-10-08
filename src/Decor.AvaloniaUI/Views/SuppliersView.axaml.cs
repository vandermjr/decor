using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class SuppliersView : UserControl
{
    public SuppliersView()
    {
        InitializeComponent();
        SuppliersGrid.InitializeColumns(
            dtoType: typeof(SupplierDTO),
            getDisplayName: null,
            getColumnWidth: propertyName => propertyName == nameof(SupplierDTO.SupplierID)
                ? new DataGridLength(80)
                : new DataGridLength(1, DataGridLengthUnitType.Star));
        SuppliersGrid.ValueMatchChanged += (_, eventArgs) =>
        {
            if (DataContext is SuppliersViewModel viewModel)
                viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
        };
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
    }

    public SuppliersView(SuppliersViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SuppliersViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
            viewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }
}