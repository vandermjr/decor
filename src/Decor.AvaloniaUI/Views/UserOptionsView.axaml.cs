using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class UserOptionsView : UserControl
{
    public UserOptionsView()
    {
        InitializeComponent();
    }

    public UserOptionsView(UserOptionsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}