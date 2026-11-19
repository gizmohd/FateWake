namespace Fatewake.Infrastructure.Art;

/// <summary>Authors a bounded subject prompt without deciding game outcomes or selecting assets.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/IArtPromptAuthor.md">Documentation</see>.</remarks>
public interface IArtPromptAuthor
{
    /// <summary>Requests a strictly structured positive and negative presentation prompt.</summary>
    Task<ArtPromptAuthoringResult> AuthorAsync(ArtPromptAuthoringRequest request, Guid workJobId, int attempt,
        CancellationToken ct);
}
