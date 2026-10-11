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
        AttachedToVisualTree += async (_, _) => await viewModel.InitializeAsync();
    }

    private void SettingsTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not IconCatalogViewModel viewModel || sender is not TreeView treeView)
            return;

        if (treeView.SelectedItem is TreeViewItem { Tag: string section })
            viewModel.SelectedSettingsSection = section;
    }
}
