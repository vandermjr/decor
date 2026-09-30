using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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

    public static DecorIconId GetId(AvaloniaObject element) => element.GetValue(IdProperty);

    public static void SetId(AvaloniaObject element, DecorIconId value) => element.SetValue(IdProperty, value);

    private DecorIcon()
    {
    }

    static DecorIcon()
    {
        IdProperty.Changed.AddClassHandler<AvaloniaObject>((icon, args) => Register(icon, args.GetNewValue<DecorIconId>()));
    }

    internal static Geometry GetGeometry(DecorIconId id) => DecorIconCatalog.Get(id, AppearanceService.CurrentAppearance.MaterialSymbolWeight);

    private static IIconAppearanceService AppearanceService
    {
        get
        {
            if (_appearanceService is not null)
                return _appearanceService;

            _appearanceService = ((App)Avalonia.Application.Current!).Services.GetRequiredService<IIconAppearanceService>();
            _appearanceService.AppearanceChanged += UpdateRegisteredIcons;
            return _appearanceService;
        }
    }

    private static void Register(AvaloniaObject icon, DecorIconId id)
    {
        RegisteredIcons.Add(new WeakReference<AvaloniaObject>(icon));
        Apply(icon, id, AppearanceService.CurrentAppearance);
    }

    private static void UpdateRegisteredIcons(Decor.Core.Configuration.IconAppearance appearance)
    {
        for (var index = RegisteredIcons.Count - 1; index >= 0; index--)
        {
            if (!RegisteredIcons[index].TryGetTarget(out var icon))
            {
                RegisteredIcons.RemoveAt(index);
                continue;
            }

            Apply(icon, icon.GetValue(IdProperty), appearance);
        }
    }

    private static void Apply(AvaloniaObject icon, DecorIconId id, Decor.Core.Configuration.IconAppearance appearance)
    {
        var geometry = DecorIconCatalog.Get(id, appearance.MaterialSymbolWeight);
        switch (icon)
        {
            case Avalonia.Controls.Shapes.Path path:
                path.Data = geometry;
                path.StrokeThickness = appearance.StrokeThickness;
                break;
            case PathIcon pathIcon:
                pathIcon.Data = geometry;
                break;
        }
    }
}
