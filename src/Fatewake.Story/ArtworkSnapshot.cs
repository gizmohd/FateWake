namespace Fatewake.Story;
/// <summary>Approved versions displayed for one authored composition.</summary>
public sealed record ArtworkSnapshot(string ArtworkKey, Dictionary<string, int?> Versions);
