namespace Fatewake.Story;

/// <summary>Defines public identity independently of persisted identifiers, deployment names and authentication settings.</summary>
/// <see href="../../docs/code/src/Fatewake.Story/BrandIdentity.md">Documentation</see>
public static class BrandIdentity
{
    /// <summary>The game name shown to players; does not rename technical assemblies or existing saves.</summary>
    public const string GameName = "Pathweaver";

    /// <summary>The shared franchise credit, distinct from the opening chapter title.</summary>
    public const string Franchise = "A Wayfinder Universe Experience";

    /// <summary>The public promise of consequential player choices, not a rules implementation.</summary>
    public const string Tagline = "Every choice shapes reality.";

    /// <summary>The player-facing identity, independent of legacy survivor IDs.</summary>
    public const string PlayerName = "Pathweaver";

    /// <summary>The plural player identity used in public guidance.</summary>
    public const string Players = "Pathweavers";

    /// <summary>Marketing domain only; never substitutes for configured OAuth callbacks or email verification URLs.</summary>
    public const string Domain = "thepathweaver.com";

    /// <summary>The public landing-page canonical URL, not a service-discovery endpoint.</summary>
    public const string PublicUrl = "https://thepathweaver.com/";
}
