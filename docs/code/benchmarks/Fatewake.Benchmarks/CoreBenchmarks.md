# CoreBenchmarks

BenchmarkDotNet MemoryDiagnoser benchmarks for deterministic day-one resolution and appearance fingerprint normalization/serialization/hashing. Inputs are stable, allocated outside measured methods, and do not invoke logging or OTLP exporters. Run Release with --filter '*'; --job Dry checks harness execution only and is not a performance baseline.

`CheckpointFingerprint` measures canonical JSON hashing for a representative nine-panel chapter, including stable property ordering independent of PostgreSQL JSONB formatting. Its input is prepared outside the measured operation.
