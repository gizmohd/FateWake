namespace Fatewake.Infrastructure.Art;
/// <summary>Configures durable binary storage used by distributed artwork workers.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtStorageOptions.md">ArtStorageOptions documentation</see>.</remarks>
public sealed class ArtStorageOptions
{
    public const string SectionName="ArtStorage";
    public string Provider{get;set;}="FileSystem";
    public string? RootPath{get;set;}
}