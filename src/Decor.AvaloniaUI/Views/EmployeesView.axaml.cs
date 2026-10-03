using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();
        EmployeesGrid.InitializeColumns(
            dtoType: typeof(EmployeeDTO),
            getDisplayName: null,
            getColumnWidth: propertyName => propertyName == nameof(EmployeeDTO.EmployeeID)
                ? new DataGridLength(80)
                : new DataGridLength(1, DataGridLengthUnitType.Star));
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
    }

    public EmployeesView(EmployeesViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is EmployeesViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
            viewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }
}