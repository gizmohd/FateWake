namespace Fatewake.GameEngine;

/// <summary>
/// Deterministically validates a player's selection against the choices a persisted scene offered, so AI-authored
/// scenes can never accept an action they did not declare.
/// </summary>
/// <see href="../../docs/code/src/Fatewake.GameEngine/SceneChoiceResolver.md">SceneChoiceResolver documentation</see>
public static class SceneChoiceResolver
{
    /// <summary>Longest accepted free-text action.</summary>
    public const int MaxFreeformLength = 300;

    /// <summary>The reserved action type for free text.</summary>
    public const string Freeform = "freeform";

    /// <summary>Validates a selection.</summary>
    /// <param name="offered">Action types offered by the scene.</param>
    /// <param name="allowFreeform">Whether the scene allows free text.</param>
    /// <param name="actionType">Selected action type, or <see cref="Freeform"/>.</param>
    /// <param name="rawInput">Free text for freeform selections.</param>
    /// <returns>The accepted or rejected selection.</returns>
    public static SceneChoiceResult Resolve(IReadOnlyCollection<string> offered, bool allowFreeform, string actionType,
        string? rawInput)
    {
        if (actionType == Freeform)
        {
            var text = new string((rawInput ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
            return allowFreeform && text.Length is > 0 and <= MaxFreeformLength
                ? new SceneChoiceResult(true, Freeform, text)
                : new SceneChoiceResult(false, actionType, null);
        }
        return offered.Contains(actionType, StringComparer.Ordinal)
            ? new SceneChoiceResult(true, actionType, null)
            : new SceneChoiceResult(false, actionType, null);
    }
}
