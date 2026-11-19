namespace Fatewake.Story;
/// <summary>Shows concrete results for the engine's canonical Day 1 outcome keys.</summary>
public static class DayOneOutcomes
{
    /// <summary>Formats a saved outcome, retaining compatibility with older stored keys.</summary>
    public static string Text(string key, IReadOnlyDictionary<string, string>? facts = null) => key switch
    {
        "injured_stranger_helped" or "helped_maya" => "You step outside and help support the injured stranger. He receives immediate assistance. Michelle sees that you came to help.",
        "called_from_safety" => "You call from the safety of your doorway, trying to help without stepping into the street. Michelle hears you; the injured man still needs someone beside him.",
        "remained_inside" or "stayed_inside" => "You remain inside. Michelle sees that no help is coming from your home and continues supporting the injured man herself.",
        "radio_investigated" or "investigated_radio" => "You turn your attention to the radio. The broadcast carries a timestamp for tomorrow at 6:17 AM. You notice it before the display changes.",
        "improvised_response" => "Michelle adjusts her grip and asks where she can set the injured man down. Whatever you try, he still needs somewhere safe to rest.",
        _ => facts is { Count: > 0 } ? string.Join(" ", facts.Values) : "Your action was recorded, but its outcome could not be displayed. Reload to recover your saved result."
    };
}
