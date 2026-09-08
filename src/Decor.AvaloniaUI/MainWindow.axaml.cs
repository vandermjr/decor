using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Views;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InitializeStatusBarDiagnosticInstrumentation();
    }

    public MainWindow(MainViewModel mainViewModel, IAuthenticatedUserContext authenticatedUserContext, IServiceProvider services)
        : this()
    {
        if (!authenticatedUserContext.IsAuthenticated || authenticatedUserContext.User!.MustChangePassword)
            throw new InvalidOperationException("A troca de senha obrigatória deve ser concluída antes de acessar a aplicação.");

        DataContext = mainViewModel;
        authenticatedUserContext.SignedOut += (_, _) =>
        {
            var loginWindow = services.GetRequiredService<LoginWindow>();
            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        };
        mainViewModel.PasswordChangeRequested += async (_, _) =>
        {
            var changePasswordWindow = services.GetRequiredService<ChangePasswordWindow>();
            changePasswordWindow.Configure(required: false);
            await changePasswordWindow.ShowDialog(this);
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

    private void Notifications_PointerEntered(object? sender, PointerEventArgs eventArgs)
    {
        if (sender is Border notificationBorder)
            SetNotificationHover(notificationBorder);
    }

    private void Notifications_PointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (sender is Border notificationBorder)
            SetNotificationHover(notificationBorder);
    }

    private void Notifications_PointerExited(object? sender, PointerEventArgs eventArgs)
    {
        if (sender is Border notificationBorder && !notificationBorder.Bounds.Contains(eventArgs.GetPosition(notificationBorder)))
        {
            notificationBorder.Classes.Remove("is-hovered");
            notificationBorder.ClearValue(Border.BackgroundProperty);
        }
    }

    private static void SetNotificationHover(Border notificationBorder)
    {
        if (!notificationBorder.Classes.Contains("is-hovered"))
            notificationBorder.Classes.Add("is-hovered");

        notificationBorder.Background = new SolidColorBrush(Color.Parse("#39424D"));
    }

    private void Notifications_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is Control notificationControl && Resources["NotificationsFlyout"] is Flyout notificationsFlyout)
            notificationsFlyout.ShowAt(notificationControl);
    }

    private void InitializeStatusBarDiagnosticInstrumentation()
    {
        MonitorControl(StatusBarFirstPageButton, "PaginationFirst");
        MonitorControl(StatusBarNotificationsBorder, "Notifications");
        MonitorControl(MainMenuRegistrationsButton, "MainMenuRegistrations");
    }

    private static void MonitorControl(Control control, string controlName)
    {
        control.PointerEntered += (_, eventArgs) => LogPointerEvent(controlName, control, "PointerEntered", eventArgs);
        control.PointerExited += (_, eventArgs) => LogPointerEvent(controlName, control, "PointerExited", eventArgs);
        control.PointerMoved += (_, eventArgs) => LogPointerEvent(controlName, control, "PointerMoved", eventArgs);
        control.PointerPressed += (_, eventArgs) => LogPointerEvent(controlName, control, "PointerPressed", eventArgs);
        control.PointerReleased += (_, eventArgs) => LogPointerEvent(controlName, control, "PointerReleased", eventArgs);
        ToolTip.AddToolTipOpeningHandler(control, (_, _) => LogToolTipEvent(controlName, control, "ToolTipOpening"));
        ToolTip.AddToolTipClosingHandler(control, (_, _) => LogToolTipEvent(controlName, control, "ToolTipClosing"));
    }

    private static void LogPointerEvent(string controlName, Control control, string eventName, PointerEventArgs eventArgs)
    {
        var position = eventArgs.GetPosition(control);
        WriteDiagnostic(controlName, eventName, control, eventArgs.Source, position.X, position.Y, eventArgs.Pointer.Type, eventArgs.Pointer.Id);
        Dispatcher.UIThread.Post(() =>
            WriteDiagnostic(controlName, $"{eventName}:AfterEvent", control, eventArgs.Source, position.X, position.Y, eventArgs.Pointer.Type, eventArgs.Pointer.Id));
    }

    private static void LogToolTipEvent(string controlName, Control control, string eventName)
    {
        WriteDiagnostic(controlName, eventName, control, source: null, positionX: null, positionY: null, pointerType: null, pointerId: null);
        Dispatcher.UIThread.Post(() =>
            WriteDiagnostic(controlName, $"{eventName}:AfterEvent", control, source: null, positionX: null, positionY: null, pointerType: null, pointerId: null));
    }

    private static void WriteDiagnostic(
        string controlName,
        string eventName,
        Control control,
        object? source,
        double? positionX,
        double? positionY,
        PointerType? pointerType,
        int? pointerId)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] STATUSBAR_DIAGNOSTIC CONTROL={controlName} EVENT={eventName} " +
            $"SOURCE={source?.GetType().Name ?? "None"} ORIGINAL_SOURCE=Unavailable " +
            $"IS_POINTER_OVER={control.IsPointerOver} TOOLTIP_IS_OPEN={ToolTip.GetIsOpen(control)} " +
            $"POSITION={(positionX.HasValue ? $"{positionX:F1},{positionY:F1}" : "N/A")} " +
            $"POINTER_TYPE={pointerType?.ToString() ?? "N/A"} POINTER_ID={pointerId?.ToString() ?? "N/A"}");
    }
}
