namespace Fatewake.Api;
/// <summary>Supplies optional survivor identity and privacy-safe broad region when starting or resuming a session.</summary>
/// <remarks><see href="../../docs/code/src/Fatewake.Api/StartSessionRequest.md">StartSessionRequest documentation</see>.</remarks>
public sealed record StartSessionRequest(Guid? SurvivorId,string? BroadRegion);