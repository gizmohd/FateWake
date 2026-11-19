namespace Fatewake.Api;
/// <summary>Administrator feedback with an exact snapshot of the reviewed composition.</summary>
public sealed record ArtReviewRequest(string SceneKey, string? AssetKey, int? Version,
    Dictionary<string, int?>? Versions, string? Critique, string? Correction, bool Regenerate, bool UseForFutureArtwork = false);
