using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.Views;

public partial class UserOptionsView : UserControl
{
    public UserOptionsView()
    {
        InitializeComponent();
    }

    public UserOptionsView(IThemeService themeService)
        : this()
    {
        DataContext = new UserOptionsViewModel(themeService);
    }
}