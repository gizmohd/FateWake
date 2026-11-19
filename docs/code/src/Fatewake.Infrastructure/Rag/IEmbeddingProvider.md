# IEmbeddingProvider

Abstraction over a text-embedding service (`src/Fatewake.Infrastructure/Rag/IEmbeddingProvider.cs`).

Returns one vector per input, in input order, each of length `Dimensions`. Implementations must signal transient failures with `HttpRequestException` and must not log input text.
