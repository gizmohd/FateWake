# OpenAiEmbeddingProvider

OpenAI `embeddings` implementation of `IEmbeddingProvider` (`src/Fatewake.Infrastructure/Rag/OpenAiEmbeddingProvider.cs`).

- Requests `dimensions` equal to the configured size and validates count, index, and length of every returned vector.
- Wrapped in `TrackedAiCall`, so usage is recorded even when parsing fails.
- Spans use `rag.embed`; input text and response bodies are never logged.
- HTTP 429 and 5xx surface as `HttpRequestException` (retryable); other failures are `InvalidOperationException`.
