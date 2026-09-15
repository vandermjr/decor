using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Services;

namespace Decor.AvaloniaUI.Views;

public partial class LoginWindow : Window
{
    private readonly INavigationService? _navigationService;

    public LoginWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Dispatcher.UIThread.Post(FocusUsername, DispatcherPriority.Input);
    }

    private void FocusUsername()
    {
        if (!UsernameTextBox.IsEffectivelyVisible || !UsernameTextBox.IsEffectivelyEnabled)
            return;

        UsernameTextBox.Focus();
        UsernameTextBox.CaretIndex = UsernameTextBox.Text?.Length ?? 0;
    }

    public LoginWindow(LoginViewModel viewModel, INavigationService navigationService)
        : this()
    {
        DataContext = viewModel;
        _navigationService = navigationService;
        viewModel.LoginSucceeded += OnLoginSucceeded;
        viewModel.PasswordChangeRequired += OnPasswordChangeRequired;
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        var mainWindow = _navigationService!.Resolve<MainWindow>();
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = mainWindow;

        mainWindow.Show();
        Close();
    }

    private async void OnPasswordChangeRequired(object? sender, EventArgs e)
    {
        var changePasswordWindow = _navigationService!.Resolve<ChangePasswordWindow>();
        changePasswordWindow.Configure(required: true);
        changePasswordWindow.SetSuccessAction(() => OnLoginSucceeded(null, EventArgs.Empty));
        await _navigationService.ShowDialogAsync(this, changePasswordWindow);
    }

    private async void CopyErrorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel { ErrorMessage: { Length: > 0 } errorMessage })
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(errorMessage);
    }
}
