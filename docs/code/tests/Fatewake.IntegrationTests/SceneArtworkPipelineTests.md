# SceneArtworkPipelineTests

Uses a disposable PostgreSQL database, an allowlisted SQL prompt revision, and a deterministic fake image provider to verify concurrent request coalescing, durable job-specific execution through every pipeline handler, and PNG/WebP delivery. A unique immutable test revision prevents prior test assets/jobs from changing the request identity; the original prompt binding is restored after the test. Temporary delivery files are created under the repository's `art` directory and removed on completion.
