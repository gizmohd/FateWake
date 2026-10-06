namespace Fatewake.Infrastructure.Art;

/// <summary>Rejects new artwork generation when no provider is configured, while allowing approved-asset reuse.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/DisabledArtMasterGenerator.md">DisabledArtMasterGenerator documentation</see>
public sealed class DisabledArtMasterGenerator : IArtMasterGenerator
{
    /// <inheritdoc />
    public Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation, CancellationToken ct = default)
    {
        throw new InvalidOperationException("New artwork generation requires ArtGeneration:Provider and an enabled provider configuration.");
    }
}
