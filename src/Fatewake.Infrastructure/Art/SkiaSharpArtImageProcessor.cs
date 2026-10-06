using SkiaSharp;

namespace Fatewake.Infrastructure.Art;

public sealed class SkiaSharpArtImageProcessor : IArtImageProcessor
{
    private const int WebPQuality=82;
    private const int PngQuality=100;

    public ArtImageInfo Inspect(ReadOnlyMemory<byte> image)
    {
        using var data=SKData.CreateCopy(image.Span);
        using var codec=SKCodec.Create(data)??throw new InvalidDataException("Invalid image.");
        using var bitmap=SKBitmap.Decode(codec)??throw new InvalidDataException("Image could not be decoded.");
        return new(bitmap.Width,bitmap.Height,HasAlpha(bitmap));
    }

    public ArtImageInfo InspectPng(ReadOnlyMemory<byte> png)
    {
        using var data=SKData.CreateCopy(png.Span);
        using var codec=SKCodec.Create(data)??throw new InvalidDataException("Invalid PNG image.");
        var info=codec.Info;
        using var bitmap=SKBitmap.Decode(codec)??throw new InvalidDataException("PNG image could not be decoded.");
        return new(info.Width,info.Height,HasAlpha(bitmap));
    }

    public Task<EncodedArtImage> CreateWebPAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        =>Task.FromResult(Encode(masterPng,SKEncodedImageFormat.Webp,WebPQuality,"image/webp",$"SkiaSharp;webp;quality={WebPQuality}"));

    public Task<EncodedArtImage> CreateOptimizedPngAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        =>Task.FromResult(Encode(masterPng,SKEncodedImageFormat.Png,PngQuality,"image/png","SkiaSharp;png"));

    private static EncodedArtImage Encode(ReadOnlyMemory<byte> source,SKEncodedImageFormat format,int quality,string contentType,string metadata)
    {
        using var data=SKData.CreateCopy(source.Span);
        using var codec=SKCodec.Create(data)??throw new InvalidDataException("Invalid image.");
        using var bitmap=SKBitmap.Decode(codec)??throw new InvalidDataException("Image could not be decoded.");
        using var image=SKImage.FromBitmap(bitmap);
        using var encoded=image.Encode(format,quality)??throw new InvalidOperationException($"Unable to encode {format}.");
        var info=bitmap.Info;
        return new(encoded.ToArray(),contentType,metadata,new(info.Width,info.Height,HasAlpha(bitmap)));
    }

    private static bool HasAlpha(SKBitmap image)
    {
        for(var y=0;y<image.Height;y++)
            for(var x=0;x<image.Width;x++)
                if(image.GetPixel(x,y).Alpha<255)return true;
        return false;
    }
}
