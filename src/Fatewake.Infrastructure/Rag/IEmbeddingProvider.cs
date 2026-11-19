namespace Fatewake.Infrastructure.Rag;

/// <summary>Converts text into fixed-size semantic vectors.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/IEmbeddingProvider.md">Documentation</see>.</remarks>
public interface IEmbeddingProvider
{
    /// <summary>Gets the model identifier recorded beside every stored vector.</summary>
    string Model
    {
        get;
    }

    /// <summary>Gets the vector length produced for each input.</summary>
    int Dimensions
    {
        get;
    }

    /// <summary>Embeds inputs, returning one vector per input in the same order.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, Guid? accountId, Guid? survivorId, CancellationToken ct);
}
