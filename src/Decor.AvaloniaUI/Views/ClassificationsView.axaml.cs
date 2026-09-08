using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class ClassificationsView : UserControl
{
    public ClassificationsView()
    {
        InitializeComponent();
    }

    public ClassificationsView(ClassificationsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }
}
