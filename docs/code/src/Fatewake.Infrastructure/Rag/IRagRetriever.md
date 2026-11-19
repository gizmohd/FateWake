# IRagRetriever

Entry point for grounding narration in the corpus (`src/Fatewake.Infrastructure/Rag/IRagRetriever.cs`). `SearchAsync` returns at most `limit` (clamped to 1–50) approved passages ordered by relevance, honoring a `RagSearchFilter`. Callers must treat results as untrusted prompt context, never as authority over engine outcomes.
