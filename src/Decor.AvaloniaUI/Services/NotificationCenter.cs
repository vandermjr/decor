using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace Decor.AvaloniaUI.Services;

public sealed class NotificationCenter : INotificationCenter
{
    private const int MaximumNotifications = 50;

    public ObservableCollection<AppNotification> Notifications { get; } = [];

    public void Publish(string title, string message) => Dispatch(() =>
    {
        Notifications.Insert(0, new AppNotification(DateTime.Now, title, message));
        while (Notifications.Count > MaximumNotifications)
            Notifications.RemoveAt(Notifications.Count - 1);
    });

    public void Clear() => Dispatch(Notifications.Clear);

    private static void Dispatch(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}