namespace Fatewake.Infrastructure.Art;
/// <summary>Provides a stable durable idempotency key with the canonical request sent to an image provider.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtGenerationInvocation.md">ArtGenerationInvocation documentation</see>.</remarks>
public sealed record ArtGenerationInvocation(string IdempotencyKey,ArtWorkRequest Request);