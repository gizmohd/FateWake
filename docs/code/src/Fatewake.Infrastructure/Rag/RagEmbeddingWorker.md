# RagEmbeddingWorker

Hosted backfill loop (`src/Fatewake.Infrastructure/Rag/RagEmbeddingWorker.cs`), run by the Worker host.

Each pass resolves `IRagEmbeddingService` in a new scope. Productive passes repeat immediately; idle or failed passes wait `Embedding:PollIntervalSeconds`. Newly imported and generated-story chunks are embedded without a separate queue because selection is by missing vector.
