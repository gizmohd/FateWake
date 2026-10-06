using Fatewake.Infrastructure.Persistence;

namespace Fatewake.Infrastructure.Art;

public sealed record ArtGenerationNeed(string Key,int Version,string VisualFingerprint);
public sealed record ArtResolution(bool RequiresGeneration,ArtAssetMatch? Asset,ArtGenerationNeed? Need);

public interface IArtResolutionService
{
    Task<ArtResolution> ResolveAsync(string key,int version,string visualFingerprint,CancellationToken ct=default);
}

public sealed class ArtResolutionService(IArtAssetStore assets):IArtResolutionService
{
    public async Task<ArtResolution> ResolveAsync(string key,int version,string visualFingerprint,CancellationToken ct=default)
    {
        var exact=await assets.FindApprovedByFingerprintAsync(visualFingerprint,ct);
        if(exact is not null)return new(false,exact,null);
        var keyed=await assets.FindApprovedAsync(key,version,ct);
        if(keyed is not null&&keyed.Key==key)return new(false,keyed,null);
        return new(true,null,new(key,version,visualFingerprint));
    }
}
