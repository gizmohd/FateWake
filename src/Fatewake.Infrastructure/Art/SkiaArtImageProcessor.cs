using SkiaSharp;
namespace Fatewake.Infrastructure.Art;
/// <summary>Validates master PNGs and creates cross-platform WebP and lossless PNG delivery derivatives with SkiaSharp.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/SkiaArtImageProcessor.md">SkiaArtImageProcessor documentation</see>.</remarks>
public sealed class SkiaArtImageProcessor:IArtImageProcessor
{
    private const int MaxDimension=16384;
    private const long MaxPixels=100_000_000;
    private const int WebPQuality=85;
    public ArtImageInfo Inspect(ReadOnlyMemory<byte> image)
    {
        using var data=SKData.CreateCopy(image.Span);
        using var codec=SKCodec.Create(data)??throw new InvalidDataException("Image data is not decodable.");
        ValidateSize(codec.Info.Width,codec.Info.Height);
        return new(codec.Info.Width,codec.Info.Height,codec.Info.AlphaType!=SKAlphaType.Opaque);
    }
    public ArtImageInfo InspectPng(ReadOnlyMemory<byte> png)
    {
        using var data=SKData.CreateCopy(png.Span);
        using var codec=SKCodec.Create(data)??throw new InvalidDataException("Image data is not decodable.");
        if(codec.EncodedFormat!=SKEncodedImageFormat.Png)throw new InvalidDataException("Master artwork must be PNG.");
        ValidateSize(codec.Info.Width,codec.Info.Height);
        return new(codec.Info.Width,codec.Info.Height,codec.Info.AlphaType!=SKAlphaType.Opaque);
    }
    public Task<EncodedArtImage> CreateWebPAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        => Task.FromResult(Encode(masterPng,SKEncodedImageFormat.Webp,WebPQuality,"skia:webp:q85",ct));
    public Task<EncodedArtImage> CreateOptimizedPngAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        => Task.FromResult(Encode(masterPng,SKEncodedImageFormat.Png,100,"skia:png:lossless",ct));
    private static EncodedArtImage Encode(ReadOnlyMemory<byte> source,SKEncodedImageFormat format,int quality,string metadata,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var data=SKData.CreateCopy(source.Span);
        using var codec=SKCodec.Create(data)??throw new InvalidDataException("Image data is not decodable.");
        if(codec.EncodedFormat!=SKEncodedImageFormat.Png)throw new InvalidDataException("Master artwork must be PNG.");
        ValidateSize(codec.Info.Width,codec.Info.Height);
        using var bitmap=SKBitmap.Decode(codec)??throw new InvalidDataException("Master PNG could not be decoded.");
        ct.ThrowIfCancellationRequested();
        using var image=SKImage.FromBitmap(bitmap);
        using var encoded=image.Encode(format,quality)??throw new InvalidOperationException($"SkiaSharp failed to encode {format}.");
        var bytes=encoded.ToArray();
        var info=new ArtImageInfo(bitmap.Width,bitmap.Height,bitmap.AlphaType!=SKAlphaType.Opaque);
        return new(bytes,format==SKEncodedImageFormat.Webp?"image/webp":"image/png",metadata,info);
    }
    private static void ValidateSize(int width,int height)
    {
        if(width<=0||height<=0||width>MaxDimension||height>MaxDimension||(long)width*height>MaxPixels)
            throw new InvalidDataException($"Image dimensions {width}x{height} exceed supported limits.");
    }
}