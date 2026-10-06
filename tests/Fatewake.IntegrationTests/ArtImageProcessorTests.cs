using Fatewake.Infrastructure.Art;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace Fatewake.IntegrationTests;

public sealed class ArtImageProcessorTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Processors()
    {
        yield return [new SkiaSharpArtImageProcessor()];
        yield return [new ImageSharpArtImageProcessor()];
    }

    [Theory]
    [MemberData(nameof(Processors))]
    public async Task Processor_preserves_dimensions_and_alpha(IArtImageProcessor processor)
    {
        var master=CreateFixture(hasAlpha:true);
        var inspected=processor.InspectPng(master);
        Assert.Equal(32,inspected.Width);Assert.Equal(24,inspected.Height);Assert.True(inspected.HasAlpha);

        var webp=await processor.CreateWebPAsync(master);
        var png=await processor.CreateOptimizedPngAsync(master);

        Assert.Equal("image/webp",webp.ContentType);Assert.Equal("image/png",png.ContentType);
        Assert.Equal(inspected,webp.Info);Assert.Equal(inspected,png.Info);
        Assert.NotEmpty(webp.Bytes);Assert.NotEmpty(png.Bytes);
        AssertDecodes(webp.Bytes,32,24);AssertDecodes(png.Bytes,32,24);
        output.WriteLine($"{processor.GetType().Name}: master={master.Length:N0} webp={webp.Bytes.Length:N0} png={png.Bytes.Length:N0}");
        output.WriteLine($"  {webp.EncoderMetadata}; {png.EncoderMetadata}");
    }

    [Theory]
    [MemberData(nameof(Processors))]
    public void Processor_detects_opaque_png(IArtImageProcessor processor)
    {
        var info=processor.InspectPng(CreateFixture(hasAlpha:false));
        Assert.False(info.HasAlpha);
    }

    [Fact]
    public async Task Both_processors_produce_comparable_delivery_assets()
    {
        var master=CreateFixture(hasAlpha:true);
        IArtImageProcessor[] processors=[new SkiaSharpArtImageProcessor(),new ImageSharpArtImageProcessor()];
        foreach(var processor in processors)
        {
            var webp=await processor.CreateWebPAsync(master);
            var png=await processor.CreateOptimizedPngAsync(master);
            output.WriteLine($"{processor.GetType().Name,-32} WebP {webp.Bytes.Length,8:N0} bytes | PNG {png.Bytes.Length,8:N0} bytes");
            AssertDecodes(webp.Bytes,32,24);AssertDecodes(png.Bytes,32,24);
        }
    }

    private static byte[] CreateFixture(bool hasAlpha)
    {
        using var image=new Image<Rgba32>(32,24);
        for(var y=0;y<image.Height;y++)
        for(var x=0;x<image.Width;x++)
            image[x,y]=new Rgba32((byte)(x*7),(byte)(y*10),(byte)((x+y)*4),hasAlpha&&x<8?(byte)96:(byte)255);
        using var stream=new MemoryStream();
        image.Save(stream,new PngEncoder{CompressionLevel=PngCompressionLevel.BestCompression});
        return stream.ToArray();
    }

    private static void AssertDecodes(byte[] bytes,int width,int height)
    {
        using var image=Image.Load(bytes);
        Assert.Equal(width,image.Width);Assert.Equal(height,image.Height);
    }
}
