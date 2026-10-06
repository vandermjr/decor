using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI.Icons;

public static class DecorWindowIcon
{
    public static WindowIcon Create(DecorIconId id)
    {
        var application = Avalonia.Application.Current;
        var weight = (application as App)?.Services.GetService<IIconAppearanceService>()
            ?.CurrentAppearance.MaterialSymbolWeight ?? 400;
        var geometry = DecorIconCatalog.Get(id, weight);
        var brush = application is not null &&
            application.TryGetResource("DecorAccentBrush", application.ActualThemeVariant, out var resource) &&
            resource is IBrush accent
                ? accent
                : new SolidColorBrush(Color.Parse("#2074B8"));

        using var png = RenderPng(geometry, brush);
        return new WindowIcon(png);
    }

    private static MemoryStream RenderPng(Geometry geometry, IBrush brush)
    {
        const int pixelSize = 64;
        const double padding = 4;
        var bounds = geometry.Bounds;
        var scale = (pixelSize - 2 * padding) / Math.Max(bounds.Width, bounds.Height);
        var transform = new Matrix(scale, 0, 0, scale,
            pixelSize / 2d - bounds.Center.X * scale,
            pixelSize / 2d - bounds.Center.Y * scale);

        using var bitmap = new RenderTargetBitmap(new PixelSize(pixelSize, pixelSize));
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(transform))
            context.DrawGeometry(brush, null, geometry);

        var png = new MemoryStream();
        try
        {
            bitmap.Save(png);
            png.Position = 0;
            return png;
        }
        catch
        {
            png.Dispose();
            throw;
        }
    }
}