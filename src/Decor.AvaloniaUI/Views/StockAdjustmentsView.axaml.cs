using Avalonia.Controls;

namespace Decor.AvaloniaUI.Views;

public partial class StockAdjustmentsView : UserControl
{
    public StockAdjustmentsView()
    {
        InitializeComponent();
    }

    public StockAdjustmentsView(ViewModels.StockAdjustmentsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
