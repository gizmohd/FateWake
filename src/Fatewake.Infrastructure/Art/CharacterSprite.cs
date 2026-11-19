using SkiaSharp;

namespace Fatewake.Infrastructure.Art;

/// <summary>Measures visible character silhouettes and produces tightly framed delivery PNGs.</summary>
public static class CharacterSprite
{
    private const byte VisibleAlpha = 32;
    private const long MaxPixels = 100_000_000;

    /// <summary>Checks for meaningful visible pixels and a transparent background before publication.</summary>
    public static bool HasUsableAlpha(ReadOnlyMemory<byte> png)
    {
        using var bitmap = Decode(png);
        return bitmap is not null && VisibleBounds(bitmap) is not null;
    }

    /// <summary>Trims empty margins while retaining a small border around visible character pixels.</summary>
    public static byte[]? CropPng(ReadOnlyMemory<byte> png)
    {
        using var bitmap = Decode(png);
        if (bitmap is null || VisibleBounds(bitmap) is not { } bounds)
            return null;

        var padding = Math.Max(2, (int)Math.Ceiling(Math.Max(bounds.Width, bounds.Height) * .02));
        var crop = new SKRectI(Math.Max(0, bounds.Left - padding), Math.Max(0, bounds.Top - padding),
            Math.Min(bitmap.Width, bounds.Right + padding), Math.Min(bitmap.Height, bounds.Bottom + padding));
        using var subset = new SKBitmap();
        if (!bitmap.ExtractSubset(subset, crop))
            return null;
        using var image = SKImage.FromBitmap(subset);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded?.ToArray();
    }

    private static SKBitmap? Decode(ReadOnlyMemory<byte> png)
    {
        using var data = SKData.CreateCopy(png.Span);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png ||
            (long)codec.Info.Width * codec.Info.Height > MaxPixels)
            return null;
        return SKBitmap.Decode(codec);
    }

    private static SKRectI? VisibleBounds(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque)
            return null;

        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = 0;
        var bottom = 0;
        long visible = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha < VisibleAlpha)
                    continue;
                visible++;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
        }

        var pixels = (long)bitmap.Width * bitmap.Height;
        var minimum = Math.Max(1, pixels / 200);
        return visible >= minimum && pixels - visible >= minimum
            ? new SKRectI(left, top, right, bottom)
            : null;
    }
}
