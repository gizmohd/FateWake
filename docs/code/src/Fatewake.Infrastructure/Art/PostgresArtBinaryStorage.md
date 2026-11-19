# PostgresArtBinaryStorage

Implements `IArtBinaryStorage` using the `art_binary` PostgreSQL table. New writes and lazily imported legacy reads are persisted as `bytea` rows with a SHA-256 hash, content type, size, and timestamps. The existing stable storage keys remain unchanged, so the artwork job graph and derivative records keep their current contracts.

The optional `ArtStorage:RootPath` is a read-only migration source. When a requested key is absent from PostgreSQL, the store reads that legacy file, saves it to PostgreSQL, and returns the imported bytes. It never writes new binaries to the filesystem. `ArtBinaryBackfillService` proactively imports every binary referenced by registered assets before API readiness.

Reads materialize the binary to verify and stream it through the current storage abstraction. Keep generated image sizes bounded and monitor PostgreSQL storage and backup growth.
