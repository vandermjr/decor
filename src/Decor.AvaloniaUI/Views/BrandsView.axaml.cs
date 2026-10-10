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
        BrandsGrid.ValueMatchChanged += (_, eventArgs) =>
        {
            if (DataContext is BrandsViewModel viewModel)
                viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
        };
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
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

}
