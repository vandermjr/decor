using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();
        CustomersGrid.InitializeColumns(
            dtoType: typeof(CustomerDTO),
            getDisplayName: null,
            getColumnWidth: propertyName => propertyName == nameof(CustomerDTO.CustomerID)
                ? new DataGridLength(80)
                : new DataGridLength(1, DataGridLengthUnitType.Star));
        CustomersGrid.ValueMatchChanged += (_, eventArgs) =>
        {
            if (DataContext is CustomersViewModel viewModel)
                viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
        };
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
    }

    public CustomersView(CustomersViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is CustomersViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
            viewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }
}