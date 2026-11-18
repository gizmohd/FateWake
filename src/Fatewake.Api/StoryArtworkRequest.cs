namespace Fatewake.Api;
/// <summary>Exact displayed versions for a reached story beat; authored placement and text remain server-owned.</summary>
public sealed record StoryArtworkRequest(Guid EventInstanceId, string BeatKey, Dictionary<string,int?> Versions);
