using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fatewake.Infrastructure.Authentication;

namespace Fatewake.Infrastructure.Story;

/// <summary>Story text checks shared by free-form input, generation and server-side presentation.</summary>
public static partial class StoryContentPolicy
{
    /// <summary>Safe player-facing feedback for rejected story content.</summary>
    public const string RetryAction = "Describe a different action that fits this scene.";

    /// <summary>Rejects disallowed language and topic terms before text is used or shown.</summary>
    public static bool Allows(ContentLevel level, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var normalized = string.Concat(text.Normalize(NormalizationForm.FormKD)
            .Where(c => char.GetUnicodeCategory(c) is not (UnicodeCategory.Format or UnicodeCategory.NonSpacingMark)));
        return !BannedTopics().IsMatch(normalized) &&
            (level == ContentLevel.AdultLanguage || !StrongLanguage().IsMatch(normalized)) &&
            (level >= ContentLevel.AdultClean || !AdultTopics().IsMatch(normalized)) &&
            (level > ContentLevel.Under14 || !OlderTeenTopics().IsMatch(normalized));
    }

    /// <summary>Checks every text field that could reach a player or the artwork pipeline.</summary>
    public static bool AllowsScene(ContentLevel level, SceneSpec spec) =>
        Allows(level, spec.Title) && Allows(level, spec.Summary) && Allows(level, spec.TimeLabel) &&
        spec.Beats is not null && spec.Choices is not null &&
        spec.Beats.All(beat => beat is not null && Allows(level, beat.Narration) && Allows(level, beat.Speaker) &&
            Allows(level, beat.Dialogue) && Allows(level, beat.ArtSubject)) &&
        spec.Choices.All(choice => choice is not null && Allows(level, choice.Label) && Allows(level, choice.ActionType));

    /// <summary>Audience and subject limits supplied to the narrator and the independent review pass.</summary>
    public static string Instructions(ContentLevel level)
    {
        const string always = "Exclude any mention, depiction, implication, or euphemism for suicide or self-harm. Exclude sexual content, graphic injury, and hate slurs. ";
        return always + (level switch
        {
            ContentLevel.AdultLanguage => "Adult themes are permitted. Strong language may be used sparingly when appropriate.",
            ContentLevel.AdultClean => "Adult themes are permitted. Use clean language without profanity, including disguised profanity.",
            ContentLevel.Teen => "Use clean language. Allow suspense and non-graphic peril suitable for ages 14–17. Exclude adult themes.",
            _ => "Use clean language. Keep conflict at mild suspense and brief, non-graphic peril suitable for children. Exclude mature themes and intense horror."
        });
    }

    [GeneratedRegex(@"s[\W_]*u[\W_]*[i1][\W_]*c[\W_]*[i1][\W_]*d|\b(?:self[\W_]*(?:harm|injur\w*)|(?:kill|hang|shoot|hurt|cut)(?:ing)?\s+(?:myself|yourself|himself|herself|themself|themselves)|(?:end|take)(?:ing)?\s+(?:my|your|his|her|their|(?:one['’]s))\s+(?:own\s+)?life|unaliv\w*\s+(?:myself|yourself|himself|herself|themself|themselves))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BannedTopics();

    [GeneratedRegex(@"\b(?:f[\W_]*[u*][\W_]*c?[\W_]*k\w*|sh[\W_]*[i*][\W_]*[t*]\w*|bullshit\w*|bitch\w*|hell|damn\w*|asshole\w*|bastard\w*|piss\w*|dickhead\w*|goddamn\w*|motherfuck\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StrongLanguage();

    [GeneratedRegex(@"\b(?:porn\w*|sexual\w*|sex|rape|raped|tortur\w*|dismember\w*|behead\w*|gore|overdose\w*|cocaine|heroin|methamphetamine)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdultTopics();

    [GeneratedRegex(@"\b(?:murder\w*|bloodbath\w*|gory|corpse\w*|drunk\w*|whisk[ey]+|vodka|cannabis)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OlderTeenTopics();
}
