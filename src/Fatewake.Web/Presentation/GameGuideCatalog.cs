using Fatewake.Story;

namespace Fatewake.Web.Presentation;

/// <summary>Provides authored landing-page guidance derived from currently playable story content.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/GameGuideCatalog.md">GameGuideCatalog documentation</see>
public static class GameGuideCatalog
{
    /// <summary>Introduces the current premise without changing gameplay canon or state.</summary>
    public const string Introduction =
        "A near-future suburban morning breaks into silence. Follow a survivor through the first choices of The Silence.";

    /// <summary>The title of the currently authored opening scene.</summary>
    public static string CurrentStoryTitle => DayOneScenes.Opening.Title;

    /// <summary>The survivor day represented by the currently authored opening scene.</summary>
    public static int CurrentStoryDay => DayOneScenes.Opening.SurvivorDay;

    /// <summary>The number of authored beats currently available in the opening scene.</summary>
    public static int CurrentStoryBeatCount => DayOneScenes.Opening.Beats.Count;

    /// <summary>Explains the current survivor-appearance experience without promising unavailable controls.</summary>
    public const string CharacterAppearanceStatus =
        "You begin with a survivor and can shape what happens through play. Character appearance customization and player-directed character images are not available yet; authored scene artwork can be prepared in the background.";
}
