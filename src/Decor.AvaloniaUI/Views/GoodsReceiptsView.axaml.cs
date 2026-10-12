using Avalonia.Controls;

namespace Decor.AvaloniaUI.Views;

public partial class GoodsReceiptsView : UserControl
{
    public GoodsReceiptsView()
    {
        InitializeComponent();
    }

    public GoodsReceiptsView(ViewModels.GoodsReceiptsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
