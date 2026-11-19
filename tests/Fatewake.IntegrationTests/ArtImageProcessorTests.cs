using Fatewake.Infrastructure.Art;
using SkiaSharp;
using Xunit;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies image inspection and cross-platform delivery encoding for supported processors.</summary>
/// <param name="output">Test output writer used to report encoder measurements.</param>
/// <see href="../../../docs/code/tests/Fatewake.IntegrationTests/ArtImageProcessorTests.md">ArtImageProcessorTests documentation</see>
public sealed class ArtImageProcessorTests(ITestOutputHelper output)
{
    /// <summary>The production processor preserves dimensions and alpha across delivery encodings.</summary>
    [Fact]
    public async Task Processor_preserves_dimensions_and_alpha()
    {
        var processor = new SkiaSharpArtImageProcessor();
        var ct = TestContext.Current.CancellationToken;
        var master = CreateFixture(hasAlpha: true);
        var inspected = processor.InspectPng(master);
        Assert.Equal(32, inspected.Width); Assert.Equal(24, inspected.Height); Assert.True(inspected.HasAlpha);

        var webp = await processor.CreateWebPAsync(master, ct);
        var png = await processor.CreateOptimizedPngAsync(master, ct);

        Assert.Equal("image/webp", webp.ContentType); Assert.Equal("image/png", png.ContentType);
        Assert.Equal(inspected, webp.Info); Assert.Equal(inspected, png.Info);
        Assert.NotEmpty(webp.Bytes); Assert.NotEmpty(png.Bytes);
        AssertDecodes(webp.Bytes, 32, 24); AssertDecodes(png.Bytes, 32, 24);
        output.WriteLine($"{processor.GetType().Name}: master={master.Length:N0} webp={webp.Bytes.Length:N0} png={png.Bytes.Length:N0}");
        output.WriteLine($"  {webp.EncoderMetadata}; {png.EncoderMetadata}");
    }

    /// <summary>The production processor detects an opaque PNG without reporting alpha.</summary>
    [Fact]
    public void Processor_detects_opaque_png()
    {
        var processor = new SkiaSharpArtImageProcessor();
        var info = processor.InspectPng(CreateFixture(hasAlpha: false));
        Assert.False(info.HasAlpha);
    }

    private static byte[] CreateFixture(bool hasAlpha)
    {
        using var image = new SKBitmap(32, 24);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                image.SetPixel(x, y, new SKColor((byte)(x * 7), (byte)(y * 10), (byte)((x + y) * 4),
                    hasAlpha && x < 8 ? (byte)96 : (byte)255));
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static void AssertDecodes(byte[] bytes, int width, int height)
    {
        using var image = SKBitmap.Decode(bytes);
        Assert.NotNull(image);
        Assert.Equal(width, image.Width); Assert.Equal(height, image.Height);
    }
}
