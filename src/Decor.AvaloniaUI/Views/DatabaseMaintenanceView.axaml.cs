using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    }

    private async void ChooseSpecificFolder_Click(object? sender, RoutedEventArgs eventArgs) =>
        await ChooseFolderAsync("Selecionar pasta para backup", path => ViewModel.SpecificFolderPath = path);

    private async void ChooseExternalDrive_Click(object? sender, RoutedEventArgs eventArgs) =>
        await ChooseFolderAsync("Selecionar unidade externa para backup", path => ViewModel.ExternalDrivePath = path);

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
