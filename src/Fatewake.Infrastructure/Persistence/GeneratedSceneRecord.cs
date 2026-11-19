namespace Fatewake.Infrastructure.Persistence;

/// <summary>Append-only AI-authored scene for one survivor timeline, validated before it becomes Ready.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/GeneratedSceneRecord.md">Documentation</see>
public sealed class GeneratedSceneRecord
{
    public Guid Id
    {
        get; set;
    }
    public Guid SurvivorId
    {
        get; set;
    }
    public Guid TimelineId
    {
        get; set;
    }
    public int Sequence
    {
        get; set;
    }
    public required string Status
    {
        get; set;
    }
    public int SurvivorDay
    {
        get; set;
    }
    public string? Title
    {
        get; set;
    }
    public string? Summary
    {
        get; set;
    }
    public string? Spec
    {
        get; set;
    }
    public bool HasDecision
    {
        get; set;
    }
    public int DecisionFreeStreak
    {
        get; set;
    }
    public string? Error
    {
        get; set;
    }
    public Guid? RagSourceId
    {
        get; set;
    }
    public DateTimeOffset? CompletedAt
    {
        get; set;
    }
    public string? ChosenActionType
    {
        get; set;
    }
    public string? ChosenText
    {
        get; set;
    }
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
    public DateTimeOffset UpdatedAt
    {
        get; set;
    }
}
