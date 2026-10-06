using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fatewake.IntegrationTests;

public sealed class ArtAssetIngestionServiceTests
{
    [Fact]
    public async Task Reusing_approved_fingerprint_does_not_process_or_store_again()
    {
        await using var db=CreateDb();
        var storage=new CountingStorage();var images=new CountingProcessor();
        var service=new ArtAssetIngestionService(db,storage,images);
        var request=Request("scene-a","fingerprint-a",approve:true);

        var first=await service.IngestAsync(request);
        var processCalls=images.Calls;var writes=storage.Writes;
        var second=await service.IngestAsync(request);

        Assert.Equal(first.AssetId,second.AssetId);
        Assert.Equal(processCalls,images.Calls);
        Assert.Equal(writes,storage.Writes);
        Assert.Equal(3,writes); // master + WebP + optimized PNG
        Assert.Single(db.ArtAssets);
        Assert.Single(db.ArtGenerations);
    }

    [Fact]
    public async Task Same_key_with_new_fingerprint_creates_next_immutable_version()
    {
        await using var db=CreateDb();var service=new ArtAssetIngestionService(db,new CountingStorage(),new CountingProcessor());
        var v1=await service.IngestAsync(Request("michelle-day1","fp-1",approve:true));
        var v2=await service.IngestAsync(Request("michelle-day1","fp-2",approve:true));

        Assert.Equal(1,v1.Version);Assert.Equal(2,v2.Version);
        Assert.NotEqual(v1.AssetId,v2.AssetId);
        Assert.Equal(2,await db.ArtAssets.CountAsync());
        Assert.Equal(2,await db.ArtGenerations.CountAsync());
    }

    [Fact]
    public async Task Generation_provenance_is_retained_exactly()
    {
        await using var db=CreateDb();var service=new ArtAssetIngestionService(db,new CountingStorage(),new CountingProcessor());
        const string prompt="EXACT resolved prompt: Michelle supports the injured stranger at 06:19.";
        await service.IngestAsync(Request("day1-0619","fp-prompt",approve:false,prompt:prompt));

        var generation=await db.ArtGenerations.SingleAsync();
        Assert.Equal(prompt,generation.ResolvedPrompt);
        Assert.Equal("template-v7",generation.PromptTemplateVersion);
        Assert.Equal("style-v1",generation.StyleBibleVersion);
        Assert.Equal("test-model",generation.Model);
        Assert.Contains("michelle-summers:v1",generation.ReferenceAssets);
    }

    [Fact]
    public async Task Derivative_asset_can_reference_immutable_parent()
    {
        await using var db=CreateDb();var service=new ArtAssetIngestionService(db,new CountingStorage(),new CountingProcessor());
        var parent=await service.IngestAsync(Request("michelle","fp-parent",approve:true));
        var child=await service.IngestAsync(Request("michelle","fp-child",approve:false,parent:parent.AssetId));

        var childRecord=await db.ArtAssets.SingleAsync(x=>x.Id==child.AssetId);
        Assert.Equal(parent.AssetId,childRecord.ParentAssetId);
        Assert.Equal(1,(await db.ArtAssets.SingleAsync(x=>x.Id==parent.AssetId)).Version);
        Assert.Equal(2,childRecord.Version);
    }

    private static FatewakeDbContext CreateDb()
    {
        var options=new DbContextOptionsBuilder<FatewakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new FatewakeDbContext(options);
    }

    private static ArtIngestRequest Request(string key,string fingerprint,bool approve,string? prompt=null,Guid? parent=null)
        =>new(key,"HeroIllustration",fingerprint,[1,2,3,4],
            new("generate","day1-test","template-v7",prompt??"resolved prompt","no text","test-provider","test-model","job-1","builder-v1","style-v1",
                ["michelle-summers:v1"],new Dictionary<string,object?>{{"quality",82}},new Dictionary<string,string>{{"michelle","v1"}},0.01m),
            CharacterKey:"michelle-summers",ParentAssetId:parent,Approve:approve);

    private sealed class CountingStorage:IArtBinaryStorage
    {
        public int Writes{get;private set;} private readonly Dictionary<string,byte[]> _files=new();
        public async Task PutAsync(string key,Stream content,string contentType,CancellationToken ct=default)
        { using var ms=new MemoryStream();await content.CopyToAsync(ms,ct);_files[key]=ms.ToArray();Writes++; }
        public Task<Stream> OpenReadAsync(string key,CancellationToken ct=default)=>Task.FromResult<Stream>(new MemoryStream(_files[key]));
        public Task<bool> ExistsAsync(string key,CancellationToken ct=default)=>Task.FromResult(_files.ContainsKey(key));
    }

    private sealed class CountingProcessor:IArtImageProcessor
    {
        public int Calls{get;private set;}
        public ArtImageInfo InspectPng(ReadOnlyMemory<byte> png){Calls++;return new(100,200,true);}
        public Task<EncodedArtImage> CreateWebPAsync(ReadOnlyMemory<byte> png,CancellationToken ct=default)
        {Calls++;return Task.FromResult(new EncodedArtImage([5,6],"image/webp","fake-webp",new(100,200,true)));}
        public Task<EncodedArtImage> CreateOptimizedPngAsync(ReadOnlyMemory<byte> png,CancellationToken ct=default)
        {Calls++;return Task.FromResult(new EncodedArtImage([7,8],"image/png","fake-png",new(100,200,true)));}
    }
}
