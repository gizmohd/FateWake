using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Fatewake.Infrastructure.Art;

public sealed class ImageSharpArtImageProcessor : IArtImageProcessor
{
    private const int WebPQuality=82;

    public ArtImageInfo InspectPng(ReadOnlyMemory<byte> png)
    {
        var info=Image.Identify(png.Span)??throw new InvalidDataException("Invalid PNG image.");
        using var image=Image.Load<Rgba32>(png.Span);
        return new(info.Width,info.Height,HasAlpha(image));
    }

    public Task<EncodedArtImage> CreateWebPAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        =>EncodeAsync(masterPng,new WebpEncoder{Quality=WebPQuality},"image/webp",$"ImageSharp;webp;quality={WebPQuality}",ct);

    public Task<EncodedArtImage> CreateOptimizedPngAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default)
        =>EncodeAsync(masterPng,new PngEncoder{CompressionLevel=PngCompressionLevel.BestCompression},"image/png","ImageSharp;png;compression=best",ct);

    private static async Task<EncodedArtImage> EncodeAsync(ReadOnlyMemory<byte> source,SixLabors.ImageSharp.Formats.IImageEncoder encoder,string contentType,string metadata,CancellationToken ct)
    {
        using var image=Image.Load<Rgba32>(source.Span);
        await using var output=new MemoryStream();
        await image.SaveAsync(output,encoder,ct);
        return new(output.ToArray(),contentType,metadata,new(image.Width,image.Height,HasAlpha(image)));
    }

    private static bool HasAlpha(Image<Rgba32> image)
    {
        for(var y=0;y<image.Height;y++)
        {
            var row=image.DangerousGetPixelRowMemory(y).Span;
            for(var x=0;x<row.Length;x++)if(row[x].A<255)return true;
        }
        return false;
    }
}
