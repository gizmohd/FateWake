namespace Fatewake.Infrastructure.Art;
/// <summary>Configures durable binary storage used by distributed artwork workers.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtStorageOptions.md">ArtStorageOptions documentation</see>.</remarks>
public sealed class ArtStorageOptions
{
    /// <summary>The configuration section used by API and Worker artwork storage.</summary>
    public const string SectionName="ArtStorage";

    /// <summary>Selects PostgreSQL storage or an explicitly configured filesystem.</summary>
    public string Provider{get;set;}="Postgres";

    /// <summary>Optional legacy filesystem root used only to read and backfill existing binaries.</summary>
    public string? RootPath{get;set;}
}