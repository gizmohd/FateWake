namespace Fatewake.Infrastructure.Rag;

/// <summary>Limits which approved passages a caller may retrieve.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/RagSearchFilter.md">Documentation</see>.</remarks>
/// <param name="SurvivorId">Survivor whose private generated story may be included; null excludes all private story.</param>
/// <param name="TimelineKey">Timeline that private generated story must belong to.</param>
/// <param name="SpoilerSeriesMax">Highest spoiler series the reader may see; null applies no limit.</param>
/// <param name="SurvivorDayMax">Latest survivor day the reader has reached; null applies no limit.</param>
/// <param name="SourceTypes">Optional source types such as WorldBible or StoryLine.</param>
public sealed record RagSearchFilter(Guid? SurvivorId = null, string? TimelineKey = null,
    int? SpoilerSeriesMax = null, int? SurvivorDayMax = null, IReadOnlyList<string>? SourceTypes = null);
