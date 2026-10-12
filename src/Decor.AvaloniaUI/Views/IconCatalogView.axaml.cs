using Avalonia.Controls;
using Avalonia.Interactivity;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class IconCatalogView : UserControl
{
    private Button? _activeIconColorButton;

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
        {
            _activeIconColorButton = button;
            button.Flyout?.ShowAt(button);
        }
    }

    private async void IconColorChoice_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not IconCatalogItemViewModel item)
            return;

        button.IsEnabled = false;
        try
        {
            await item.SetColorAsync(button.Tag as string);
            if (DataContext is IconCatalogViewModel viewModel)
                viewModel.ClearIconColorStatus();
            CloseIconColorPalette();
        }
        catch
        {
            if (DataContext is IconCatalogViewModel viewModel)
                viewModel.ReportIconColorSaveFailure();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void IconColorReset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not IconCatalogItemViewModel item)
            return;

        button.IsEnabled = false;
        try
        {
            await item.SetColorAsync(null);
            if (DataContext is IconCatalogViewModel viewModel)
                viewModel.ClearIconColorStatus();
            CloseIconColorPalette();
        }
        catch
        {
            if (DataContext is IconCatalogViewModel viewModel)
                viewModel.ReportIconColorSaveFailure();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void CloseIconColorPalette()
    {
        _activeIconColorButton?.Flyout?.Hide();
        _activeIconColorButton = null;
    }
}
