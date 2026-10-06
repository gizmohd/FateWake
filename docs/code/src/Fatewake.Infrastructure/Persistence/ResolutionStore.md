# ResolutionStore

Persists accepted action consequences transactionally in PostgreSQL. Uses game.persist_resolution operation tracing/latency and a host-supplied typed Microsoft logger. No raw player input, arguments, narrative text, or canonical IDs are used as metric labels. GameEngine itself remains free of telemetry dependencies.
