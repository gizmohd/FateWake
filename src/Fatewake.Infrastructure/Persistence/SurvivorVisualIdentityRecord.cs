namespace Fatewake.Infrastructure.Persistence;
/// <summary>Tracks the active immutable identity asset and any background appearance request for a survivor.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/SurvivorVisualIdentityRecord.md">SurvivorVisualIdentityRecord documentation</see>.</remarks>
public sealed class SurvivorVisualIdentityRecord
{
    public Guid SurvivorId{get;set;}
    public Guid ActiveArtAssetId{get;set;}
    public Guid? RequestedWorkJobId{get;set;}
    public string? RequestedVisualFingerprint{get;set;}
    public SurvivorVisualIdentityStatus Status{get;set;}
    public Guid? SourceSurvivorId{get;set;}
    public DateTimeOffset CreatedAt{get;set;}
    public DateTimeOffset UpdatedAt{get;set;}
    public DateTimeOffset? ActivatedAt{get;set;}
}