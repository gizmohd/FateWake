# RagSearchFilter

Visibility limits for retrieval (`src/Fatewake.Infrastructure/Rag/RagSearchFilter.cs`).

- `SurvivorId` + `TimelineKey` together unlock a survivor's own private generated story; without both, private story is never returned.
- `SpoilerSeriesMax` and `SurvivorDayMax` hide chunks whose minimum series/day exceeds the reader's progress. Chunks without limits are always eligible.
- `SourceTypes` optionally restricts to types such as `WorldBible`, `StoryLine`, or `GeneratedStory`.
