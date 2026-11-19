# ResolutionStore

Persists accepted action consequences transactionally in PostgreSQL. Uses game.persist_resolution operation tracing/latency and a host-supplied typed Microsoft logger. No raw player input, arguments, narrative text, or canonical IDs are used as metric labels. GameEngine itself remains free of telemetry dependencies.

Survivor/timeline locking serializes resolution with Thread creation and selection. Reached pre-decision state, branch facts, action/effects/Wakes and seen consequence history commit together. Matching retries return the recorded full resolution without reapplying effects; changed payloads and an already-resolved decision conflict.

Tracked episode state is refreshed under the lock before validating the decision. Business conflicts produce bounded warnings; unexpected failures/cancellation mark the operation without exporting exception messages or action content.
