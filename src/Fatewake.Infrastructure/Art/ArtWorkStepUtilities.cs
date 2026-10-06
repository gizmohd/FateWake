using System.Security.Cryptography;
namespace Fatewake.Infrastructure.Art;
/// <summary>Provides deterministic binary-loading and hashing helpers shared by artwork work-step handlers.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtWorkStepUtilities.md">ArtWorkStepUtilities documentation</see>.</remarks>
internal static class ArtWorkStepUtilities
{
    public static async Task<byte[]> ReadAllAsync(IArtBinaryStorage storage,string key,CancellationToken ct){await using var stream=await storage.OpenReadAsync(key,ct);using var ms=new MemoryStream();await stream.CopyToAsync(ms,ct);return ms.ToArray();}
    public static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}