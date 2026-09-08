using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class TermDeliveryView : UserControl
{
    public TermDeliveryView()
    {
        InitializeComponent();
    }

    public TermDeliveryView(TermDeliveryViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }
}
