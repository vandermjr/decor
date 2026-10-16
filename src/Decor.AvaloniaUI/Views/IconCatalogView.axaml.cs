using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class IconCatalogView : UserControl
{
    private Button? _activeIconColorButton;
    private TimeSpan? _backupNotificationLeadTimeBeforeSelection;

    public IconCatalogView()
    {
        InitializeComponent();
        GridAppearancePreview.InitializeColumns(typeof(GridAppearancePreviewRow));
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

    private async void ChooseLoginLogoButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IconCatalogViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar logo da empresa",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Imagens PNG ou JPEG") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }]
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            viewModel.SaveLoginLogo(path);
    }

    private void ChooseBackupNotificationLeadTimeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IconCatalogViewModel viewModel)
            _backupNotificationLeadTimeBeforeSelection = viewModel.BackupNotificationLeadTime;
    }

    private void BackupNotificationLeadTimePicker_Confirmed(object? sender, EventArgs e)
    {
        if (DataContext is IconCatalogViewModel viewModel)
            viewModel.SaveBackupNotificationSettings();
        ChooseBackupNotificationLeadTimeButton.Flyout?.Hide();
    }

    private void BackupNotificationLeadTimePicker_Dismissed(object? sender, EventArgs e)
    {
        if (DataContext is IconCatalogViewModel viewModel)
            viewModel.BackupNotificationLeadTime = _backupNotificationLeadTimeBeforeSelection;
        ChooseBackupNotificationLeadTimeButton.Flyout?.Hide();
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
