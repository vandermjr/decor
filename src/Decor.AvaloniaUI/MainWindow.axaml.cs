using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Views;
using Decor.AvaloniaUI.Services;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI;

public partial class MainWindow : Window
{
    private bool _isAboutOpen;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel mainViewModel, IAuthenticatedUserContext authenticatedUserContext, INavigationService navigationService)
        : this()
    {
        if (!authenticatedUserContext.IsAuthenticated || authenticatedUserContext.User!.MustChangePassword)
            throw new InvalidOperationException("A troca de senha obrigatória deve ser concluída antes de acessar a aplicação.");

        DataContext = mainViewModel;
        authenticatedUserContext.SignedOut += (_, _) =>
        {
            var loginWindow = navigationService.Resolve<LoginWindow>();
            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        };
        mainViewModel.PasswordChangeRequested += async (_, _) =>
        {
            var changePasswordWindow = navigationService.Resolve<ChangePasswordWindow>();
            changePasswordWindow.Configure(required: false);
            await navigationService.ShowDialogAsync(this, changePasswordWindow);
        };
        mainViewModel.AboutRequested += async (_, _) =>
        {
            if (_isAboutOpen) return;
            _isAboutOpen = true;
            try
            {
                await navigationService.ShowDialogAsync(this, new AboutWindow());
            }
            finally
            {
                _isAboutOpen = false;
            }
        };
    }

    private static void DocumentTab_PointerEntered(object? sender, PointerEventArgs eventArgs)
    {
        if (sender is Control { DataContext: WorkspaceDocumentViewModel document })
            document.IsPointerOver = true;
    }

    private static void DocumentTab_PointerExited(object? sender, PointerEventArgs eventArgs)
    {
        if (sender is Control { DataContext: WorkspaceDocumentViewModel document })
            document.IsPointerOver = false;
    }

    private static void DocumentTab_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: WorkspaceDocumentViewModel document })
            document.ActivateCommand.Execute(null);
    }

    private void NotificationsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (sender is Button notificationsButton && Resources["NotificationsFlyout"] is Flyout notificationsFlyout)
            notificationsFlyout.ShowAt(notificationsButton);
    }

    private void UserButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (sender is Button userButton && Resources["UserFlyout"] is Flyout userFlyout)
            userFlyout.ShowAt(userButton);
    }

}
