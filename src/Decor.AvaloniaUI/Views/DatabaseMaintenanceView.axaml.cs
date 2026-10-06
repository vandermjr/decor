using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Layout;
using Avalonia.Media;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class DatabaseMaintenanceView : UserControl
{
    private DateTimeOffset? _backupDateBeforeSelection;
    private TimeSpan? _backupTimeBeforeSelection;

    public DatabaseMaintenanceView()
    {
        InitializeComponent();
    }

    public DatabaseMaintenanceView(DatabaseMaintenanceViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(DatabaseMaintenanceViewModel.IsRestoring)
                && TopLevel.GetTopLevel(this) is Window owner)
                owner.IsEnabled = !viewModel.IsRestoring;
        };
        viewModel.RestoreFinishedRequested += async () =>
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                viewModel.FinishRestoreCommand.Execute(null);
                return;
            }
            var dialog = new Window
            {
                Title = "Decor - Restauração do banco",
                Width = 560,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var signOutIcon = new PathIcon { Width = 16, Height = 16 };
            DecorIcon.SetId(signOutIcon, DecorIconId.User.SignOut);
            var closeButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { signOutIcon, new TextBlock { Text = "Encerrar sessão" } }
                }
            };
            closeButton.Click += (_, _) => dialog.Close();
            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = viewModel.RestoreStatus, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = "Cópia de segurança: " + viewModel.SafetyBackupFile, TextWrapping = TextWrapping.Wrap },
                    closeButton
                }
            };
            try { await dialog.ShowDialog(owner); }
            finally { viewModel.FinishRestoreCommand.Execute(null); }
        };
    }

    private async void ChooseSpecificFolder_Click(object? sender, RoutedEventArgs eventArgs) =>
        await ChooseFolderAsync("Selecionar pasta para backup", path => ViewModel.SpecificFolderPath = path);

    private async void ChooseExternalDrive_Click(object? sender, RoutedEventArgs eventArgs) =>
        await ChooseFolderAsync("Selecionar unidade externa para backup", path => ViewModel.ExternalDrivePath = path);

    private async void ChooseRestoreFile_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (!ViewModel.CanRestore || TopLevel.GetTopLevel(this) is not { } topLevel) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar backup Decor para restauração",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Backup Decor") { Patterns = ["*.zip"] }]
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) ViewModel.RestoreFile = path;
    }

    private void SchedulesList_PointerReleased(object? sender, PointerReleasedEventArgs eventArgs) =>
        ViewModel.LoadSelectedScheduleForEditing();

    private void ChooseBackupDateButton_Click(object? sender, RoutedEventArgs eventArgs) =>
        _backupDateBeforeSelection = ViewModel.BackupDate;

    private void ChooseBackupTimeButton_Click(object? sender, RoutedEventArgs eventArgs) =>
        _backupTimeBeforeSelection = ViewModel.BackupTime;

    private void BackupDateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is not Calendar { SelectedDate: { } selectedDate })
            return;

        ViewModel.BackupDate = new DateTimeOffset(selectedDate.Date);
        ChooseBackupDateButton.Flyout?.Hide();
    }

    private void BackupTimePicker_Confirmed(object? sender, EventArgs eventArgs) =>
        ChooseBackupTimeButton.Flyout?.Hide();

    private void BackupTimePicker_Dismissed(object? sender, EventArgs eventArgs)
    {
        ViewModel.BackupTime = _backupTimeBeforeSelection;
        ChooseBackupTimeButton.Flyout?.Hide();
    }

    private DatabaseMaintenanceViewModel ViewModel => (DatabaseMaintenanceViewModel)DataContext!;

    private async Task ChooseFolderAsync(string title, Action<string> setPath)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        var selectedFolder = folders.FirstOrDefault();
        if (selectedFolder?.Path is null)
            return;

        setPath(selectedFolder.Path.IsFile ? selectedFolder.Path.LocalPath : selectedFolder.Path.ToString());
    }
}
