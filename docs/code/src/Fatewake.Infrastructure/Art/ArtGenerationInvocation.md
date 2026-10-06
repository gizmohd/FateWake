# ArtGenerationInvocation

Carries the canonical art request plus a durable idempotency key. The key is derived from the durable work job identity so retries and lease recovery address the same provider operation rather than creating another billable generation.
