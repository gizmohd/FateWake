namespace Fatewake.Infrastructure.Art;
/// <summary>Generates a master PNG for an artwork request when exact approved reuse is unavailable.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/IArtMasterGenerator.md">IArtMasterGenerator documentation</see>. Implementations must preserve provider provenance and support idempotent request semantics.</remarks>
public interface IArtMasterGenerator
{
    Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation,CancellationToken ct=default);
}