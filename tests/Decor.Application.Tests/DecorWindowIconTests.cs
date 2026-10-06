using System.IO;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Skia;
using Decor.AvaloniaUI.Icons;
using SkiaSharp;

namespace Decor.Application.Tests;

public class DecorWindowIconTests
{
    private static readonly Lazy<bool> RendererInitialized = new(() =>
    {
        SkiaPlatform.Initialize();
        return true;
    });

    [Fact]
    public void Unknown_id_is_rejected_before_creating_a_window_icon()
    {
        Assert.Throws<KeyNotFoundException>(() => DecorWindowIcon.Create(new DecorIconId("Common.Unknown")));
    }

    [Theory]
    [InlineData("Common.Barcode")]
    [InlineData("Common.Status")]
    [InlineData("Common.Unlocked")]
    [InlineData("Forms.Permissions")]
    [InlineData("Navigation.SortAscending")]
    [InlineData("User.SignIn")]
    public void Rasterization_produces_centered_transparent_png_without_windowing_platform(string id)
    {
        _ = RendererInitialized.Value;
        var render = typeof(DecorWindowIcon).GetMethod("RenderPng", BindingFlags.NonPublic | BindingFlags.Static)!;
        var color = Color.Parse("#2074B8");

        foreach (var weight in DecorIconCatalog.SupportedWeights)
        {
            var geometry = DecorIconCatalog.Get(new DecorIconId(id), weight);
            using var png = (MemoryStream)render.Invoke(null, [geometry, new SolidColorBrush(color)])!;
            Assert.Equal(0, png.Position);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.ToArray().Take(8));
            using var bitmap = SKBitmap.Decode(png);
            Assert.NotNull(bitmap);
            Assert.Equal(64, bitmap.Width);
            Assert.Equal(64, bitmap.Height);

            var opaquePixels = new List<(int Column, int Row)>();
            for (var row = 0; row < bitmap.Height; row++)
            {
                for (var column = 0; column < bitmap.Width; column++)
                {
                    var pixel = bitmap.GetPixel(column, row);
                    if (row < 3 || column < 3 || row >= 61 || column >= 61)
                        Assert.Equal(0, pixel.Alpha);
                    if (pixel.Alpha == 255)
                    {
                        Assert.Equal(color.R, pixel.Red);
                        Assert.Equal(color.G, pixel.Green);
                        Assert.Equal(color.B, pixel.Blue);
                        opaquePixels.Add((column, row));
                    }
                }
            }

            Assert.NotEmpty(opaquePixels);
            Assert.InRange((opaquePixels.Min(pixel => pixel.Column) + opaquePixels.Max(pixel => pixel.Column)) / 2d, 30, 33);
            Assert.InRange((opaquePixels.Min(pixel => pixel.Row) + opaquePixels.Max(pixel => pixel.Row)) / 2d, 30, 33);
        }
    }
}