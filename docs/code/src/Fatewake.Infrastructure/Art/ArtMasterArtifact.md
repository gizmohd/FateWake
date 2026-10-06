# ArtMasterArtifact

## Purpose
Durable pointer and provenance summary for the master PNG selected by the job. Reused masters point at an approved library asset; generated masters use deterministic job staging storage.

## Distributed execution
The handler may run on any Linux or Windows worker replica. Intermediate binary content lives in shared art storage; PostgreSQL job artifacts carry only durable keys and JSON metadata. Retried execution first checks its named artifact so completed work is reused.

## Idempotency and cost
Storage keys are deterministic per job. AI provider generation is allowed only after exact approved reuse and same-job master reuse have both missed. Encoding/inspection may safely retry without incurring provider-generation cost.
