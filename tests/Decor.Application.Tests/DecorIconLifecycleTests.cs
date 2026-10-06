using System.Reflection;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Decor.AvaloniaUI.Icons;

namespace Decor.Application.Tests;

public sealed class DecorIconLifecycleTests
{
    [Fact]
    public void ClearingAttachedStyleValue_DoesNotResolveEmptyId()
    {
        var icon = new PathIcon();
        var style = icon.SetValue(DecorIcon.IdProperty, new DecorIconId(" "), BindingPriority.Style);
        icon.Data = new RectangleGeometry();

        style!.Dispose();

        Assert.Null(icon.Data);
    }

    [Fact]
    public void ResettingIconIdDuringStyleRemoval_ClearsGeometryWithoutAppearanceService()
    {
        var icon = new PathIcon { Data = new RectangleGeometry() };
        var register = typeof(DecorIcon).GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Static)!;

        register.Invoke(null, [icon, default(DecorIconId)]);

        Assert.Null(icon.Data);
    }

    [Fact]
    public void ResettingPathIdDuringStyleRemoval_ClearsGeometryWithoutAppearanceService()
    {
        var icon = new Avalonia.Controls.Shapes.Path { Data = new RectangleGeometry() };
        var register = typeof(DecorIcon).GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Static)!;

        register.Invoke(null, [icon, new DecorIconId(string.Empty)]);

        Assert.Null(icon.Data);
    }
}