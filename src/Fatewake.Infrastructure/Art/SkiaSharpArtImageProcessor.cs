using SkiaSharp;

namespace Fatewake.Infrastructure.Art;

public sealed class SkiaSharpArtImageProcessor : IArtImageProcessor
{
    private const int WebPQuality=82;
    private const int PngQuality=100;

    public ArtImageInfo InspectPng(ReadOnlyMemory<byte> png)
    {
        using var data=SKData.CreateCopy(png.Span);
        using var codec=SKCodec.Create(data)??throw new InvalidDataException("Invalid PNG image.");
        var info=codec.Info;
        return new(info.Width,info.Height,info.AlphaType!=SKAlphaType.Opaque);
    }

    public Task<EncodedArtImage> CreateWebPAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        =>Task.FromResult(Encode(masterPng,SKEncodedImageFormat.Webp,WebPQuality,"image/webp",$"SkiaSharp;webp;quality={WebPQuality}"));

    public Task<EncodedArtImage> CreateOptimizedPngAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        =>Task.FromResult(Encode(masterPng,SKEncodedImageFormat.Png,PngQuality,"image/png","SkiaSharp;png"));

    private static EncodedArtImage Encode(ReadOnlyMemory<byte> source,SKEncodedImageFormat format,int quality,string contentType,string metadata)
    {
        using var data=SKData.CreateCopy(source.Span);
        using var image=SKImage.FromEncodedData(data)??throw new InvalidDataException("Invalid PNG image.");
        using var encoded=image.Encode(format,quality)??throw new InvalidOperationException($"Unable to encode {format}.");
        var info=image.Info;
        return new(encoded.ToArray(),contentType,metadata,new(info.Width,info.Height,info.AlphaType!=SKAlphaType.Opaque));
    }
}
