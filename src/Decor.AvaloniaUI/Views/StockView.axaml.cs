using Avalonia.Controls;

namespace Decor.AvaloniaUI.Views;

public partial class StockView : UserControl
{
    public StockView()
    {
        InitializeComponent();
    }

    public StockView(ViewModels.StockViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
