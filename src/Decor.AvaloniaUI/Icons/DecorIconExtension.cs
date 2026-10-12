using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI.Icons;

public sealed class DecorIconExtension : MarkupExtension
{
    public DecorIconId Id { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => DecorIcon.GetGeometry(Id);
}

public sealed class DecorIcon
{
    private static readonly List<WeakReference<AvaloniaObject>> RegisteredIcons = [];
    private static IIconAppearanceService? _appearanceService;

    public static readonly AttachedProperty<DecorIconId> IdProperty =
        AvaloniaProperty.RegisterAttached<DecorIcon, AvaloniaObject, DecorIconId>("Id");
    public static readonly AttachedProperty<int> WeightProperty =
        AvaloniaProperty.RegisterAttached<DecorIcon, AvaloniaObject, int>("Weight");
    private static readonly AttachedProperty<bool> IsLoadHandlerRegisteredProperty =
        AvaloniaProperty.RegisterAttached<DecorIcon, AvaloniaObject, bool>("IsLoadHandlerRegistered");

    public static DecorIconId GetId(AvaloniaObject element) => element.GetValue(IdProperty);

    public static void SetId(AvaloniaObject element, DecorIconId value) => element.SetValue(IdProperty, value);
    public static int GetWeight(AvaloniaObject element) => element.GetValue(WeightProperty);
    public static void SetWeight(AvaloniaObject element, int value) => element.SetValue(WeightProperty, value);

    private DecorIcon()
    {
    }

    static DecorIcon()
    {
        IdProperty.Changed.AddClassHandler<AvaloniaObject>((icon, args) => Register(icon, args.GetNewValue<DecorIconId>()));
        WeightProperty.Changed.AddClassHandler<AvaloniaObject>((icon, _) => Register(icon, icon.GetValue(IdProperty)));
        global::Avalonia.Application.RequestedThemeVariantProperty.Changed.AddClassHandler<global::Avalonia.Application>((_, args) =>
            UpdateRegisteredIcons(AppearanceService.CurrentAppearance, args.GetNewValue<ThemeVariant>()));
    }

    internal static Geometry GetGeometry(DecorIconId id) => DecorIconCatalog.Get(id, AppearanceService.CurrentAppearance.MaterialSymbolWeight);

    private static IIconAppearanceService AppearanceService
    {
        get
        {
            var currentService = ((App)Avalonia.Application.Current!).Services.GetRequiredService<IIconAppearanceService>();
            if (ReferenceEquals(_appearanceService, currentService))
                return currentService;

            if (_appearanceService is not null)
                _appearanceService.AppearanceChanged -= UpdateRegisteredIcons;

            _appearanceService = currentService;
            _appearanceService.AppearanceChanged += UpdateRegisteredIcons;
            return _appearanceService;
        }
    }

    private static void Register(AvaloniaObject icon, DecorIconId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            ClearGeometry(icon);
            return;
        }
        if (!RegisteredIcons.Any(reference => reference.TryGetTarget(out var registered) && ReferenceEquals(registered, icon)))
        {
            RegisteredIcons.Add(new WeakReference<AvaloniaObject>(icon));
        }

        if (icon is Control control && !icon.GetValue(IsLoadHandlerRegisteredProperty))
        {
            icon.SetValue(IsLoadHandlerRegisteredProperty, true);
            control.Loaded += (_, _) => Apply(icon, icon.GetValue(IdProperty), AppearanceService.CurrentAppearance);
        }

        Apply(icon, id, AppearanceService.CurrentAppearance);
    }

    private static void UpdateRegisteredIcons(Decor.Core.Configuration.IconAppearance appearance) => UpdateRegisteredIcons(appearance, null);

    private static void UpdateRegisteredIcons(Decor.Core.Configuration.IconAppearance appearance, ThemeVariant? themeVariant)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateRegisteredIcons(appearance, themeVariant));
            return;
        }

        for (var index = RegisteredIcons.Count - 1; index >= 0; index--)
        {
            if (!RegisteredIcons[index].TryGetTarget(out var icon))
            {
                RegisteredIcons.RemoveAt(index);
                continue;
            }

            Apply(icon, icon.GetValue(IdProperty), appearance, themeVariant);
        }
    }

    private static void Apply(AvaloniaObject icon, DecorIconId id, Decor.Core.Configuration.IconAppearance appearance, ThemeVariant? themeVariant = null)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            ClearGeometry(icon);
            return;
        }
        var weight = icon.GetValue(WeightProperty);
        var geometry = DecorIconCatalog.Get(id, weight == 0 ? appearance.MaterialSymbolWeight : weight);
        var effectiveThemeVariant = themeVariant ?? (icon is Control control ? control.ActualThemeVariant : null);
        var brush = DecorIconColors.GetBrush(id, appearance, effectiveThemeVariant);
        switch (icon)
        {
            case Avalonia.Controls.Shapes.Path path:
                path.Data = geometry;
                path.StrokeThickness = appearance.StrokeThickness;
                path.Fill = brush;
                break;
            case PathIcon pathIcon:
                pathIcon.Data = geometry;
                pathIcon.Foreground = brush;
                break;
        }
    }

    private static void ClearGeometry(AvaloniaObject icon)
    {
        switch (icon)
        {
            case Avalonia.Controls.Shapes.Path path:
                path.Data = null;
                break;
            case PathIcon pathIcon:
                pathIcon.Data = null;
                break;
        }
    }
}
