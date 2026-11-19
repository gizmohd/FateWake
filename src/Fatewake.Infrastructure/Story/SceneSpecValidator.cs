using System.Text.RegularExpressions;
using Fatewake.GameEngine;
using Fatewake.Story;

namespace Fatewake.Infrastructure.Story;

/// <summary>Server-side validation of untrusted narrator output, including the decision pacing rule.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneSpecValidator.md">Documentation</see>.</remarks>
public static partial class SceneSpecValidator
{
    /// <summary>Throws <see cref="InvalidDataException"/> when the scene is unsafe, malformed, or violates pacing.</summary>
    /// <param name="spec">Candidate scene.</param>
    /// <param name="previousStreak">Decision-free streak before this scene.</param>
    /// <returns>The decision-free streak after this scene.</returns>
    public static int Validate(SceneSpec spec, int previousStreak)
    {
        Text(spec.Title, 200, "title");
        Text(spec.Summary, 1000, "summary");
        Text(spec.TimeLabel, 60, "time label");
        if (spec.Beats.Count is < 1 or > 8)
            throw new InvalidDataException("A scene needs one to eight beats.");
        foreach (var beat in spec.Beats)
        {
            if (!Enum.TryParse<BeatKind>(beat.Kind, true, out _))
                throw new InvalidDataException("A beat has an unknown kind.");
            Text(beat.Narration, 1200, "narration");
            Text(beat.ArtRole, 40, "art role");
            Text(beat.ArtSubject, 400, "art subject");
            if (!string.IsNullOrWhiteSpace(beat.Dialogue))
            {
                Text(beat.Dialogue, 500, "dialogue");
                Text(beat.Speaker, 60, "speaker");
            }
        }
        if (spec.Choices.Count is 1 or > 4)
            throw new InvalidDataException("A decision needs two to four choices.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in spec.Choices)
        {
            Text(choice.Label, 80, "choice label");
            if (!ActionPattern().IsMatch(choice.ActionType) || !seen.Add(choice.ActionType))
                throw new InvalidDataException("A choice has an invalid or duplicate action type.");
        }
        if (!SceneDecisionPolicy.TryAccept(previousStreak, spec.HasDecision, out var streak))
            throw new InvalidDataException("The scene must offer a decision after four decision-free scenes.");
        return streak;
    }

    private static void Text(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > max ||
            text.Any(c => char.IsControl(c) && c != '\n') ||
            text.Contains("```", StringComparison.Ordinal) ||
            text.Contains("ignore previous instructions", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("tool_call", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The scene {field} is missing, too long, or unsafe.");
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{2,39}$")]
    private static partial Regex ActionPattern();
}
