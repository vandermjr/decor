using Avalonia;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
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
        InitializeDocumentTabScrollBehavior();
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

        InitializeDocumentTabScrollBehavior(mainViewModel);
    }

    private void InitializeDocumentTabScrollBehavior(MainViewModel? viewModel = null)
    {
        if (viewModel is null)
        {
            this.AttachedToVisualTree += (_, _) => UpdateDocumentTabsButtons();
            this.LayoutUpdated += (_, _) => UpdateDocumentTabsButtons();
            return;
        }

        viewModel.OpenDocuments.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ScrollTabsToEnd();
                    UpdateDocumentTabsButtons();
                });
                return;
            }

            global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateDocumentTabsButtons);
        };

        this.AttachedToVisualTree += (_, _) =>
        {
            UpdateDocumentTabsButtons();
            if (viewModel.OpenDocuments.Count > 0)
                ScrollTabsToEnd();
        };
    }

    private void DocumentTabsScroller_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateDocumentTabsButtons();
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

    private void DocumentTabsScrollLeft_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        ScrollDocumentTabs(-220d);
    }

    private void DocumentTabsScrollRight_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        ScrollDocumentTabs(220d);
    }

    private void ScrollDocumentTabs(double delta)
    {
        if (this.FindControl<ScrollViewer>("DocumentTabsScroller") is not { } scroller)
            return;

        var nextOffset = Math.Clamp(scroller.Offset.X + delta, 0d, Math.Max(0d, scroller.Extent.Width - scroller.Viewport.Width));
        scroller.Offset = new Vector(nextOffset, scroller.Offset.Y);
        UpdateDocumentTabsButtons();
    }

    private void ScrollTabsToEnd()
    {
        if (this.FindControl<ScrollViewer>("DocumentTabsScroller") is not { } scroller)
            return;

        var maxOffset = Math.Max(0d, scroller.Extent.Width - scroller.Viewport.Width);
        scroller.Offset = new Vector(maxOffset, scroller.Offset.Y);
        UpdateDocumentTabsButtons();
    }

    private void UpdateDocumentTabsButtons()
    {
        if (this.FindControl<ScrollViewer>("DocumentTabsScroller") is not { } scroller)
            return;

        if (this.FindControl<Button>("DocumentTabsScrollLeft") is not { } leftButton ||
            this.FindControl<Button>("DocumentTabsScrollRight") is not { } rightButton)
            return;

        var maxOffset = Math.Max(0d, scroller.Extent.Width - scroller.Viewport.Width);
        var hasOverflow = maxOffset > 0.01d;

        leftButton.IsEnabled = hasOverflow && scroller.Offset.X > 0.01d;
        rightButton.IsEnabled = hasOverflow && scroller.Offset.X < maxOffset - 0.01d;
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
