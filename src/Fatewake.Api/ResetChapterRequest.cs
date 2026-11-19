namespace Fatewake.Api;
/// <summary>Development-only request to rewind a survivor's active chapter to its first beat.</summary>
/// <remarks><see href="../../docs/code/src/Fatewake.Api/ResetChapterRequest.md">ResetChapterRequest documentation</see>.</remarks>
public sealed record ResetChapterRequest(Guid SurvivorId, Guid EventInstanceId);
