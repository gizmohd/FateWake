namespace Fatewake.Story;
/// <summary>Audience-safe important story update with a durable acknowledgement revision.</summary>
public sealed record StoryNotice(Guid PanelId, int Revision, string Title, string Message);
