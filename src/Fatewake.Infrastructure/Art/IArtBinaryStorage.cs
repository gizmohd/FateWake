namespace Fatewake.Infrastructure.Art;
/// <summary>Provides durable binary storage for master artwork and delivery derivatives.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/IArtBinaryStorage.md">IArtBinaryStorage documentation</see>.</remarks>
public interface IArtBinaryStorage
{
    Task PutAsync(string storageKey,Stream content,string contentType,CancellationToken ct=default);
    Task<Stream> OpenReadAsync(string storageKey,CancellationToken ct=default);
    Task<bool> ExistsAsync(string storageKey,CancellationToken ct=default);
}