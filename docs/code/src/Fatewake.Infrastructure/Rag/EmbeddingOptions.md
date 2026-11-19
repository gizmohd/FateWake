# EmbeddingOptions

Settings for the `Embedding` configuration section (`src/Fatewake.Infrastructure/Rag/EmbeddingOptions.cs`).

- `Enabled` and `ApiKey` gate all embedding work; when either is missing the backfill is a no-op and RAG stays keyword-only.
- `Dimensions` must equal the `rag_chunk."Embedding" vector(1024)` column size.
- Set the key locally with `dotnet user-secrets set "Embedding:ApiKey" "<key>" --project src\Fatewake.AppHost`.
