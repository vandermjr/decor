using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Decor.AvaloniaUI.Controls;

public sealed class DecorScrollViewerBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<bool> ResetToHomeWhenVisibleProperty =
        AvaloniaProperty.RegisterAttached<DecorScrollViewerBehavior, ScrollViewer, bool>("ResetToHomeWhenVisible");

    static DecorScrollViewerBehavior()
    {
        ResetToHomeWhenVisibleProperty.Changed.AddClassHandler<ScrollViewer>(OnResetBehaviorChanged);
    }

    public static bool GetResetToHomeWhenVisible(ScrollViewer scrollViewer) =>
        scrollViewer.GetValue(ResetToHomeWhenVisibleProperty);

    public static void SetResetToHomeWhenVisible(ScrollViewer scrollViewer, bool value) =>
        scrollViewer.SetValue(ResetToHomeWhenVisibleProperty, value);

    private static void OnResetBehaviorChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs args)
    {
        scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
        if (!GetResetToHomeWhenVisible(scrollViewer))
            return;

        scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        if (scrollViewer.IsVisible)
            QueueScrollToHome(scrollViewer);
    }

    private static void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is ScrollViewer scrollViewer && args.Property == Visual.IsVisibleProperty && scrollViewer.IsVisible)
            QueueScrollToHome(scrollViewer);
    }

    private static void QueueScrollToHome(ScrollViewer scrollViewer) =>
        Dispatcher.UIThread.Post(scrollViewer.ScrollToHome, DispatcherPriority.Loaded);
}
