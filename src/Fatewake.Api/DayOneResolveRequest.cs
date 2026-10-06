using Fatewake.GameEngine;
namespace Fatewake.Api;
/// <summary>Supplies the authoritative identifiers, idempotency key, and candidate action required to resolve a Day One action.</summary>
/// <remarks><see href="../../docs/code/src/Fatewake.Api/DayOneResolveRequest.md">DayOneResolveRequest documentation</see>.</remarks>
public sealed record DayOneResolveRequest(Guid EventInstanceId,Guid SurvivorId,Guid TimelineId,Guid IdempotencyKey,CandidateAction Action);