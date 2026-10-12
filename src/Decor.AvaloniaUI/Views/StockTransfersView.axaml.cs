using Avalonia.Controls;

namespace Decor.AvaloniaUI.Views;

public partial class StockTransfersView : UserControl
{
    public StockTransfersView()
    {
        InitializeComponent();
    }

    public StockTransfersView(ViewModels.StockTransfersViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
