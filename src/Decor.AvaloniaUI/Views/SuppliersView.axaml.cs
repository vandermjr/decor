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
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
    }

    public SuppliersView(SuppliersViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

}