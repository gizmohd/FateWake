namespace Fatewake.Api;

/// <summary>Constrains personalized artwork context to reached, server-owned Day 1 outcomes.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Api/SceneArtContextPolicy.md">Documentation</see>.</remarks>
public static class SceneArtContextPolicy
{
    private static readonly IReadOnlyDictionary<string, (string ActionType, string VisualContext)> Outcomes =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["injured_stranger_helped"] = ("help_injured_stranger", "Immediate assistance was given to the injured stranger."),
            ["helped_maya"] = ("help_injured_stranger", "Immediate assistance was given to the injured stranger."),
            ["called_from_safety"] = ("call_from_safety", "A call for help came from the safety of the doorway; no one reached the injured stranger."),
            ["remained_inside"] = ("stay_inside", "The resident remained indoors while Michelle supported the injured stranger."),
            ["stayed_inside"] = ("stay_inside", "The resident remained indoors while Michelle supported the injured stranger."),
            ["radio_investigated"] = ("investigate_radio", "The radio was investigated and its tomorrow timestamp was noticed."),
            ["investigated_radio"] = ("investigate_radio", "The radio was investigated and its tomorrow timestamp was noticed.")
        };

    private static readonly HashSet<string> ActionTypes = new(StringComparer.Ordinal)
    {
        "help_injured_stranger", "call_from_safety", "stay_inside", "investigate_radio", "freeform"
    };

    /// <summary>Indicates that a visual layer depends on the reached choice outcome rather than shared static canon.</summary>
    public static bool RequiresReachedOutcome(string assetKey) =>
        string.Equals(assetKey, "bg-day1-neighborhood-after-choice", StringComparison.Ordinal);

    /// <summary>Checks an action against the active interaction or its immediately reached consequence beat.</summary>
    public static bool IsSelectedActionReached(string? actionType, string? beatKey, string? lastOutcome)
    {
        if (actionType is null || !ActionTypes.Contains(actionType))
            return false;
        if (beatKey == "first-choice")
            return lastOutcome is null;
        if (beatKey != "choice-wake" || string.IsNullOrWhiteSpace(lastOutcome))
            return false;
        return Outcomes.TryGetValue(lastOutcome, out var outcome)
            ? string.Equals(outcome.ActionType, actionType, StringComparison.Ordinal)
            : actionType == "freeform";
    }

    /// <summary>Returns safe, bounded visual facts only for supported persisted outcome keys.</summary>
    public static bool TryGetReachedVisualContext(string? lastOutcome, out string context)
    {
        if (lastOutcome is not null && Outcomes.TryGetValue(lastOutcome, out var outcome))
        {
            context = outcome.VisualContext;
            return true;
        }
        context = string.Empty;
        return false;
    }

    /// <summary>Prevents arbitrary request text from entering the shared outcome-specific prompt identity.</summary>
    public static bool IsCanonicalVisualContext(string? context) =>
        context is not null && Outcomes.Values.Any(outcome =>
            string.Equals(outcome.VisualContext, context, StringComparison.Ordinal));

    /// <summary>Checks the bounded set of action identifiers accepted by the artwork priority hint.</summary>
    public static bool IsKnownAction(string? actionType) =>
        actionType is not null && ActionTypes.Contains(actionType);
}
