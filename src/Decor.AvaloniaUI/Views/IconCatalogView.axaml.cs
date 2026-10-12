using Avalonia.Controls;
using Avalonia.Interactivity;
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
        AttachedToVisualTree += async (_, _) => await viewModel.InitializeAsync();
    }

    private void SettingsTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not IconCatalogViewModel viewModel || sender is not TreeView treeView)
            return;

        if (treeView.SelectedItem is TreeViewItem { Tag: string section })
            viewModel.SelectedSettingsSection = section;
    }

    private void IconColorChoiceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
            button.Flyout?.ShowAt(button);
    }

    private void IconColorChoice_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is IconCatalogItemViewModel item)
        {
            item.SetColor(button.Tag as string);
            button.Flyout?.Hide();
        }
    }

    private void IconColorReset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is IconCatalogItemViewModel item)
        {
            item.SetColor(null);
            button.Flyout?.Hide();
        }
    }
}
