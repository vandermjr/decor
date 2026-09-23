using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class RolesView : UserControl
{
    public RolesView()
    {
        InitializeComponent();
    }

    public RolesView(RolesViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }
}
