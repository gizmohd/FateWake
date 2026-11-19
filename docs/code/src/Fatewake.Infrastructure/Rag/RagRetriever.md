# RagRetriever

pgvector-backed `IRagRetriever` (`src/Fatewake.Infrastructure/Rag/RagRetriever.cs`).

- **Ranking:** when embeddings are enabled the query is embedded and ordered by cosine distance. Zero vector hits (for example before backfill finishes) or disabled embeddings fall back to PostgreSQL full-text rank.
- **Authorization:** only chunks, documents and sources with status `Approved` are eligible. `SharedApprovedCanon` and `Internal` sources are visible to every survivor (server-side prompt context only). `Private` sources require `Metadata.survivorId` equal to the filter's survivor and a chunk `TimelineKey` equal to the filter's timeline.
- **Spoilers and progress:** `SpoilerSeriesMin` and `SurvivorDayMin` must not exceed the filter limits.
- **Observability:** span `rag.retrieve` and a passage-count log; query text is never logged or exported.
