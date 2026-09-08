using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class BrandsView : UserControl
{
    public BrandsView()
    {
        InitializeComponent();
        ConfigureGridColumns();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
    }

    public BrandsView(BrandsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void ConfigureGridColumns()
    {
        BrandsGrid.InitializeColumns(
            dtoType: typeof(BrandDTO),
            getDisplayName: null,
            getColumnWidth: propertyName => propertyName == nameof(BrandDTO.BrandID)
                ? new DataGridLength(90)
                : new DataGridLength(1, DataGridLengthUnitType.Star));
    }

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (DataContext is BrandsViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
            viewModel.SearchCommand.Execute(null);

        e.Handled = true;
    }
}
