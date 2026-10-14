using System.Collections.ObjectModel;

namespace Decor.AvaloniaUI.Services;

public interface INotificationCenter
{
    ObservableCollection<AppNotification> Notifications { get; }
    void Publish(string title, string message);
    void Clear();
}