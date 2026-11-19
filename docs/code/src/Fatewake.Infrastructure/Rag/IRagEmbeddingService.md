# IRagEmbeddingService

Embeds pending RAG chunks (`src/Fatewake.Infrastructure/Rag/IRagEmbeddingService.cs`).

`EmbedPendingAsync` processes one batch and returns the number stored, so callers can loop until it returns zero.
