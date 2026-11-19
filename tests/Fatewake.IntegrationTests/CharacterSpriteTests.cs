using Fatewake.Infrastructure.Art;
using SkiaSharp;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies the alpha checks and crop used to display scene characters.</summary>
public sealed class CharacterSpriteTests
{
    /// <summary>A padded sprite crops to its character while keeping a transparent border.</summary>
    [Fact]
    public void Trim_preserves_the_silhouette_and_transparency()
    {
        var png = Image(SKColors.Transparent, drawSubject: true);
        Assert.True(CharacterSprite.HasUsableAlpha(png));
        var cropped = CharacterSprite.CropPng(png);
        Assert.NotNull(cropped);

        using var result = SKBitmap.Decode(cropped);
        Assert.Equal(28, result.Width);
        Assert.Equal(64, result.Height);
        Assert.Equal(0, result.GetPixel(0, 0).Alpha);
        Assert.Equal(SKColors.Red, result.GetPixel(4, 4));
    }

    /// <summary>Empty images and opaque backdrops fail character-layer validation.</summary>
    [Fact]
    public void Rejects_blank_and_opaque_overlays()
    {
        Assert.False(CharacterSprite.HasUsableAlpha(Image(SKColors.Transparent, drawSubject: false)));
        Assert.False(CharacterSprite.HasUsableAlpha(Image(SKColors.Black, drawSubject: true)));
    }

    private static byte[] Image(SKColor background, bool drawSubject)
    {
        using var bitmap = new SKBitmap(160, 90, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);
        if (drawSubject)
        {
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(90, 18, 24, 60, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
