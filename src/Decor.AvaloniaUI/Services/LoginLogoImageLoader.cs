using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Decor.AvaloniaUI.Services;

internal static class LoginLogoImageLoader
{
    public static Bitmap? Load(string path, bool invertColors)
    {
        try
        {
            using var source = path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                ? AssetLoader.Open(new Uri(path))
                : File.OpenRead(path);
            using var sourceBuffer = new MemoryStream();
            source.CopyTo(sourceBuffer);
            var sourceBytes = sourceBuffer.ToArray();
            if (!invertColors)
            {
                using var original = new MemoryStream(sourceBytes, writable: false);
                return new Bitmap(original);
            }

            using var decoded = SKBitmap.Decode(sourceBytes);
            if (decoded is null)
                return null;

            using var inverted = new SKBitmap(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            for (var y = 0; y < decoded.Height; y++)
            for (var x = 0; x < decoded.Width; x++)
            {
                var pixel = decoded.GetPixel(x, y);
                inverted.SetPixel(x, y, new SKColor(
                    (byte)(255 - pixel.Red),
                    (byte)(255 - pixel.Green),
                    (byte)(255 - pixel.Blue),
                    pixel.Alpha));
            }

            using var encoded = SKImage.FromBitmap(inverted).Encode(SKEncodedImageFormat.Png, 100);
            using var output = new MemoryStream(encoded.ToArray(), writable: false);
            return new Bitmap(output);
        }
        catch
        {
            return null;
        }
    }
}