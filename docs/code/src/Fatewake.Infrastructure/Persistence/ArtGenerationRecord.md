# ArtGenerationRecord

Durable audit record created before a provider call. It is uniquely associated with the distributed work job and idempotency key, records the exact resolved prompt, references and generation parameters, and is completed with provider job ID/cost after generation. ArtAssetId is nullable until finalization.
