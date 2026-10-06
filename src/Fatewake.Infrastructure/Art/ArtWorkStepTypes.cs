namespace Fatewake.Infrastructure.Art;
/// <summary>Defines stable handler identifiers for steps in the distributed artwork workflow.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtWorkStepTypes.md">ArtWorkStepTypes documentation</see>.</remarks>
public static class ArtWorkStepTypes
{
    public const string Resolve="art.resolve";
    public const string GenerateMaster="art.generate-master";
    public const string EncodeWebP="art.encode-webp";
    public const string OptimizePng="art.optimize-png";
    public const string InspectMetadata="art.inspect-metadata";
    public const string Validate="art.validate";
    public const string Finalize="art.finalize";
}