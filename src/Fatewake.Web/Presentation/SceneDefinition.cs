namespace Fatewake.Web.Presentation;

/// <summary>Defines an episode scene as ordered beats for a survivor's day.</summary>
/// <param name="Key">Stable scene identifier.</param>
/// <param name="EpisodeKey">Identifier of the containing episode.</param>
/// <param name="SurvivorDay">In-game day associated with the scene.</param>
/// <param name="Title">Player-facing scene title.</param>
/// <param name="Beats">Ordered scene beats.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SceneDefinition.md">SceneDefinition documentation</see>
public sealed record SceneDefinition(string Key, string EpisodeKey, int SurvivorDay, string Title, IReadOnlyList<BeatDefinition> Beats)
{
    /// <summary>Finds a beat by key, falling back to the first beat when no key matches.</summary>
    /// <param name="key">Beat identifier to find.</param>
    /// <returns>The matching beat, or the first beat when the key is unknown.</returns>
    public BeatDefinition Beat(string key) => Beats.FirstOrDefault(x => x.Key == key) ?? Beats[0];

    /// <summary>Gets the index of a beat by key, falling back to the first position when unknown.</summary>
    /// <param name="key">Beat identifier to locate.</param>
    /// <returns>The matching beat's zero-based index, or zero when the key is unknown.</returns>
    public int IndexOf(string key)
    {
        for (var i = 0; i < Beats.Count; i++)
            if (Beats[i].Key == key)
                return i;
        return 0;
    }
}
