# TimelineSnapshotCodec

Pure integrity and compatibility validation. SHA-256 covers recursively property-ordered JSON, remaining stable across PostgreSQL JSONB roundtrips. Unsupported schema/rules/content, malformed facts, invalid opening/pre-decision episode state and incompatible personal history are explicit conflicts before restoration. The caller also checks episode identity against the checkpoint's recorded lineage. No persistence, model calls or telemetry occur in these pure operations.
