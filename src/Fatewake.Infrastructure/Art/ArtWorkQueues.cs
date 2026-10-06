namespace Fatewake.Infrastructure.Art;
/// <summary>Defines stable logical queue names used by the distributed artwork pipeline.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtWorkQueues.md">ArtWorkQueues documentation</see>.</remarks>
public static class ArtWorkQueues
{
    public const string Generate="art.generate";
    public const string Encode="art.encode";
    public const string Validate="art.validate";
    public const string Finalize="art.finalize";
}