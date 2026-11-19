# RagEmbeddingService

Writes vectors into `rag_chunk."Embedding"` (`src/Fatewake.Infrastructure/Rag/RagEmbeddingService.cs`).

- Selects chunks whose `EmbeddedAt` is null, oldest first, so the process is idempotent and resumable.
- Updates use raw SQL because the vector column is intentionally not mapped in EF.
- Records `EmbeddingModel`, `EmbeddingDimensions`, and `EmbeddedAt` with the vector.
- `FormatVector` produces the invariant-culture pgvector literal.
