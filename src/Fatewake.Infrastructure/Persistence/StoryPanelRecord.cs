namespace Fatewake.Infrastructure.Persistence;
/// <summary>Immutable seen panel content and explicit disclosure-safe external influence narration.</summary>
public sealed class StoryPanelRecord
{
    public Guid Id { get; set; }
    public Guid SurvivorId { get; set; }
    public Guid TimelineId { get; set; }
    public Guid? EventInstanceId { get; set; }
    public Guid? SourceEventId { get; set; }
    public required string EntryKey { get; set; }
    public long Sequence { get; set; }
    public int SurvivorDay { get; set; }
    public required string Kind { get; set; }
    public required string Title { get; set; }
    public string? TimeLabel { get; set; }
    public required string Narration { get; set; }
    public string? DialogueSpeaker { get; set; }
    public string? DialogueText { get; set; }
    public required string Layers { get; set; }
    public string? HiddenCauseNarration { get; set; }
    public bool CauseRevealed { get; set; }
    public int VisibleFromDay { get; set; }
    public bool Reconstructed { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public int NotificationRevision { get; set; }
    public int SeenRevision { get; set; }
}
