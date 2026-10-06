# ArtValidateWorkStepHandler

## Purpose
Checks required shared-storage binaries and verifies derivative dimensions match the master before finalization.

## Safety and idempotency
The distributed pipeline uses durable PostgreSQL artifacts and shared binary storage. Final activation is conditional on the survivor still waiting for the same work job and visual fingerprint, preventing stale background generation from replacing a newer player choice.
