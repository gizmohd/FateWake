namespace Fatewake.Infrastructure.Persistence;
/// <summary>Persists a named JSON artifact shared by steps within one durable job.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkArtifactRecord.md">WorkArtifactRecord documentation</see>.</remarks>
public sealed class WorkArtifactRecord
{
    public Guid Id{get;set;}
    public Guid JobId{get;set;}
    public required string Key{get;set;}
    public required string Value{get;set;}
    public DateTimeOffset CreatedAt{get;set;}
    public DateTimeOffset UpdatedAt{get;set;}
}