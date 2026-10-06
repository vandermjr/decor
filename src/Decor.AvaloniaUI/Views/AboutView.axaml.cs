using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = new AboutViewModel();
    }
}