namespace Fatewake.Api;
/// <summary>Identifies the scene and beat reached by a survivor presentation session.</summary>
/// <remarks><see href="../../docs/code/src/Fatewake.Api/PresentationProgressRequest.md">PresentationProgressRequest documentation</see>.</remarks>
public sealed record PresentationProgressRequest(Guid SurvivorId,Guid EventInstanceId,string SceneKey,string BeatKey);