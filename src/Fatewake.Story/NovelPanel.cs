namespace Fatewake.Story;
/// <summary>A reader-safe panel projection containing only disclosed narration and pinned artwork.</summary>
public sealed record NovelPanel(Guid Id, long Sequence, int SurvivorDay, string Kind, string Title,
    string? TimeLabel, string Narration, string? DialogueSpeaker, string? DialogueText, ArtLayer[] Layers, bool Reconstructed);
