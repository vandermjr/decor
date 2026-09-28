using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class IconCatalogView : UserControl
{
    public IconCatalogView()
    {
        InitializeComponent();
    }

    public IconCatalogView(IconCatalogViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}
