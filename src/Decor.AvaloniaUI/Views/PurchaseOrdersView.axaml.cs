using Avalonia.Controls;

namespace Decor.AvaloniaUI.Views;

public partial class PurchaseOrdersView : UserControl
{
    public PurchaseOrdersView()
    {
        InitializeComponent();
    }

    public PurchaseOrdersView(ViewModels.PurchaseOrdersViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
