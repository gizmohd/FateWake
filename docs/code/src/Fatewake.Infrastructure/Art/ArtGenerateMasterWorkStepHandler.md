# ArtGenerateMasterWorkStepHandler

## Purpose
Generation boundary. It first reuses an existing art.master artifact, then honors art.resolve exact reuse, and invokes IArtMasterGenerator only when neither exists.

## Distributed execution
The handler may run on any Linux or Windows worker replica. Intermediate binary content lives in shared art storage; PostgreSQL job artifacts carry only durable keys and JSON metadata. Retried execution first checks its named artifact so completed work is reused.

## Idempotency and cost
Storage keys are deterministic per job. AI provider generation is allowed only after exact approved reuse and same-job master reuse have both missed. Encoding/inspection may safely retry without incurring provider-generation cost.
