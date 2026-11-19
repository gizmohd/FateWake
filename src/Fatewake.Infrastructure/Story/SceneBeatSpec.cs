namespace Fatewake.Infrastructure.Story;

/// <summary>One presentation beat authored for a scene; text is untrusted until validated.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneBeatSpec.md">Documentation</see>.</remarks>
/// <param name="Kind">Beat kind name matching the story BeatKind enum.</param>
/// <param name="Narration">Narration text.</param>
/// <param name="Speaker">Dialogue speaker, when the beat has dialogue.</param>
/// <param name="Dialogue">Dialogue text.</param>
/// <param name="ArtRole">Layer role such as background, character or overlay.</param>
/// <param name="ArtSubject">Concise visual subject for artwork generation.</param>
public sealed record SceneBeatSpec(string Kind, string Narration, string? Speaker, string? Dialogue,
    string ArtRole, string ArtSubject);
