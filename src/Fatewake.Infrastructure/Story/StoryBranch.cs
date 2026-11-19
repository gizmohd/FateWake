using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Fatewake.Infrastructure.Story;

/// <summary>Shared branch data consists only of bounded plot concepts, independent of player text or identity.</summary>
internal static partial class StoryBranch
{
    internal static readonly string[] Focuses =
        ["survey_surroundings", "check_on_neighbor", "listen_for_updates", "look_for_supplies", "find_shelter", "wait_and_observe"];

    internal static string? DayOneIntent(string? outcome) => outcome switch
    {
        "injured_stranger_helped" or "helped_maya" => "check_on_neighbor",
        "called_from_safety" => "request_assistance",
        "remained_inside" or "stayed_inside" => "find_shelter",
        "radio_investigated" or "investigated_radio" => "listen_for_updates",
        _ => null
    };

    internal static string? Intent(string? choice)
    {
        if (string.IsNullOrWhiteSpace(choice))
            return null;
        // Ambiguous, negated, or unfamiliar actions stay private to their own branch.
        if (Negation().IsMatch(choice))
            return null;
        var matches = IntentWords().Matches(choice).Select(match => match.Groups.Values.Skip(1)
            .Single(group => group.Success).Name).Distinct().Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    internal static string? Key(int day, int sequence, string? intent, string? previousBranch)
    {
        if (intent is null || (sequence > 1 && previousBranch is null))
            return null;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"story-branch-v1:{day}:{sequence}:{previousBranch}:{intent}")));
    }

    [GeneratedRegex(@"\b(?:not|never|without|avoid|don['’]t|won['’]t|can['’]t)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Negation();

    [GeneratedRegex(@"\b(?:(?<listen_for_updates>radio|broadcast|frequency)|(?<check_on_neighbor>help|assist|aid|neighbor)|(?<find_shelter>shelter|hide|indoors)|(?<look_for_supplies>supplies|water|food)|(?<wait_and_observe>wait|watch|observe)|(?<survey_surroundings>look around|look outside|scan the area)|(?<request_assistance>call|shout|yell))\b", RegexOptions.IgnoreCase)]
    private static partial Regex IntentWords();
}
