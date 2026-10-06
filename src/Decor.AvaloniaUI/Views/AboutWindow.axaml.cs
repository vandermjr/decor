using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Decor.AvaloniaUI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs eventArgs) => Close();
}