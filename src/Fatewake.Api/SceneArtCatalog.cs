using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;

namespace Fatewake.Api;

/// <summary>Defines legitimate scene layers and composes SQL prompt revisions with enforced visual constraints.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtCatalog.md">SceneArtCatalog documentation</see>
public sealed class SceneArtCatalog
{
    private readonly IConfiguration configuration;
    private static readonly string[] Keys = [
        "bg-day1-bedroom-dark", "prop-phone-0617-noservice", "bg-day1-house-hallway", "light-day1-power-flicker",
        "bg-day1-neighborhood-player-threshold", "char-eli-day1-observing", "char-injured-stranger-day1-supported",
        "char-michelle-day1-supporting", "fg-player-threshold-shadow", "light-day1-early-morning",
        "bg-day1-neighborhood-after-choice", "bg-day1-radio-room", "prop-radio-day1", "effect-radio-static",
        "prop-radio-timestamp-tomorrow-0617", "bg-day1-blackout-0643", "effect-power-collapse",
        "bg-day1-neighborhood-silent", "light-day1-after-shutdown"
    ];

    private readonly string _root;
    private readonly string _styleBible;
    private readonly string _styleVersion;
    private readonly string _runtimeStyle;
    private readonly string _runtimeExclusions;

    /// <summary>Loads the style bible once; its version participates in every prompt identity.</summary>
    public SceneArtCatalog(IConfiguration configuration)
    {
        this.configuration = configuration;
        _root = configuration["ArtGeneration:CatalogRoot"] ?? Path.Combine(AppContext.BaseDirectory, "SceneArt");
        _styleBible = File.ReadAllText(Path.Combine(_root, "STYLE-BIBLE.md"));
        _styleVersion = ReadStyleVersion(_styleBible);
        _runtimeStyle = RequiredStyleSection(_styleBible, "Runtime generation style");
        _runtimeExclusions = RequiredStyleSection(_styleBible, "Runtime style exclusions");
    }

    /// <summary>Enforced positive style rules shared across every artwork role.</summary>
    internal string RuntimeStyle => _runtimeStyle;
    /// <summary>Enforced exclusions shared across every artwork role.</summary>
    internal string RuntimeExclusions => _runtimeExclusions;

    /// <summary>Checks the server-owned scene-layer allowlist.</summary>
    public bool Contains(string key) => Keys.Contains(key, StringComparer.Ordinal);

    /// <summary>Whether the selected image provider has its required configuration.</summary>
    public bool GenerationEnabled => (configuration["ArtGeneration:Provider"] ?? "None").ToLowerInvariant() switch
    {
        "openai" => configuration.GetValue<bool>("ArtGeneration:OpenAI:Enabled") && !string.IsNullOrWhiteSpace(configuration["ArtGeneration:OpenAI:ApiKey"]),
        "local" => configuration.GetValue<bool>("ArtGeneration:Local:Enabled"),
        "comfyui" => configuration.GetValue<bool>("ArtGeneration:ComfyUI:Enabled") &&
            File.Exists(configuration["ArtGeneration:ComfyUI:WorkflowPath"] ?? new ComfyUiArtGenerationOptions().WorkflowPath) &&
            (string.IsNullOrWhiteSpace(configuration["ArtGeneration:ComfyUI:OverlayWorkflowPath"]) ||
                File.Exists(configuration["ArtGeneration:ComfyUI:OverlayWorkflowPath"])),
        _ => false
    };

    /// <summary>Returns the stable, public identity used for SQL lookup and work coalescing.</summary>
    internal SceneArtPromptMetadata Describe(string key, string? reachedVisualContext = null)
    {
        if (!Contains(key))
            throw new ArgumentException("Unknown scene asset.", nameof(key));
        if (reachedVisualContext is not null &&
            (!SceneArtContextPolicy.RequiresReachedOutcome(key) ||
                !SceneArtContextPolicy.IsCanonicalVisualContext(reachedVisualContext)))
            throw new ArgumentException("Reached outcome context is not applicable to this shared scene layer.", nameof(reachedVisualContext));
        var type = key.Split('-')[0] switch
        {
            "bg" => "Background",
            "char" => "CharacterPose",
            "prop" => "Prop",
            "fg" => "Foreground",
            "light" => "Lighting",
            "effect" => "Effect",
            _ => throw new InvalidOperationException("Unknown asset role.")
        };
        var framing = type == "Background"
            ? "Opaque environment background, portrait 9:16 composition."
            : "Isolated reusable overlay on a transparent background; no full scene background.";
        var canonical = ReadCanonicalContext(key, type);
        if (reachedVisualContext is not null)
            canonical += $"\nReached visual outcome: {reachedVisualContext}";
        var contextFingerprint = Hash($"scene-context-v1\n{key}\n{type}\n{framing}\n{canonical}");
        var identity = $"{key}:shared:{_styleVersion}:{contextFingerprint}";
        return new(key, type, identity, contextFingerprint, _styleVersion, canonical, framing, reachedVisualContext);
    }

    /// <summary>Creates immutable authoring input from allowlisted canonical context and current style rules.</summary>
    internal ArtPromptAuthoringRequest CreateAuthoringRequest(SceneArtPromptMetadata metadata, Guid? accountId, Guid? survivorId) =>
        new(metadata.IdentityKey, metadata.AssetKey, metadata.AssetType, metadata.ContextFingerprint,
            metadata.StyleBibleVersion, metadata.CanonicalContext, metadata.Framing, _runtimeStyle,
            _runtimeExclusions, accountId, survivorId);

    /// <summary>Builds a provider request from a specific SQL revision, never from mutable critique history.</summary>
    internal ArtWorkRequest CreateWorkRequest(SceneArtPromptMetadata metadata, ArtPromptRevisionRecord revision,
        Guid? accountId = null, Guid? survivorId = null, int priority = 0)
    {
        var context = CreateAuthoringRequest(metadata, accountId, survivorId) with
        {
            LegacyVisualFingerprint = revision.Source == "seed" ? ComputeLegacyFingerprint(metadata, revision) : null
        };
        return ArtPromptWorkRequestBuilder.Create(configuration, context, revision, accountId, survivorId, priority);
    }

    internal string ComputeLegacyFingerprint(SceneArtPromptMetadata metadata, ArtPromptRevisionRecord revision)
    {
        if (revision.Source != "seed")
            throw new InvalidOperationException("Only a seed revision can match legacy artwork.");
        var continuity = metadata.AssetType is "CharacterPose" or "Background"
            ? ReadFullContinuity(metadata.AssetKey, metadata.AssetType)
            : string.Empty;
        var resolved = $"{revision.PositivePrompt}\n{_runtimeStyle}\n{metadata.Framing}\n{continuity}";
        var negative = _runtimeExclusions;
        if (metadata.AssetType != "CharacterPose")
            negative += ", person, people, man, woman, child, human, face, body, hands, character portrait";
        if (metadata.AssetType == "Background")
            negative += ", smartphone, phone, text, ruined building, rubble, post-apocalyptic decay";
        if (metadata.AssetType == "Prop")
            negative += ", room, furniture, scenery, multiple objects, text, letters, numbers";
        return Hash($"scene-art-v4\n{metadata.StyleBibleVersion}\n{metadata.AssetKey}\n{resolved}\n{negative}");
    }

    private string ReadCanonicalContext(string key, string type)
    {
        if (type == "CharacterPose")
        {
            var file = key switch
            {
                "char-michelle-day1-supporting" => "MICHELLE-SUMMERS.md",
                "char-eli-day1-observing" => "ELI-SUMMERS.md",
                _ => "INJURED-STRANGER-DAY1.md"
            };
            return ExtractSection(File.ReadAllText(Path.Combine(_root, "characters", file)), "Continuity anchors");
        }
        if (type != "Background")
            return $"Day 1 visual layer: {Humanize(key)}. Time and weather follow reached scene state.";
        var fileName = key.Contains("neighborhood", StringComparison.Ordinal)
            ? "DAY1-NEIGHBORHOOD.md" : "DAY1-PLAYER-HOME.md";
        var continuity = File.ReadAllText(Path.Combine(_root, "locations", fileName));
        var firstParagraph = continuity.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(section => !section.StartsWith('#'))?.Trim() ?? string.Empty;
        var moment = key.Contains("0643", StringComparison.Ordinal) || key.Contains("after-shutdown", StringComparison.Ordinal)
            ? "Day 1 after the 6:43 AM power shutdown."
            : "Day 1 at dawn, before the 6:43 AM power shutdown.";
        return $"{moment} {firstParagraph}";
    }

    private string ReadFullContinuity(string key, string type)
    {
        if (type == "CharacterPose")
        {
            var file = key switch
            {
                "char-michelle-day1-supporting" => "MICHELLE-SUMMERS.md",
                "char-eli-day1-observing" => "ELI-SUMMERS.md",
                _ => "INJURED-STRANGER-DAY1.md"
            };
            return File.ReadAllText(Path.Combine(_root, "characters", file));
        }
        var fileName = key.Contains("neighborhood", StringComparison.Ordinal)
            ? "DAY1-NEIGHBORHOOD.md" : "DAY1-PLAYER-HOME.md";
        return File.ReadAllText(Path.Combine(_root, "locations", fileName));
    }

    private static string ExtractSection(string document, string heading)
    {
        var normalized = document.Replace("\r\n", "\n");
        var marker = $"## {heading}\n";
        var start = normalized.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;
        var body = normalized[(start + marker.Length)..];
        var end = body.IndexOf("\n## ", StringComparison.Ordinal);
        return (end >= 0 ? body[..end] : body).Trim();
    }

    private static string Humanize(string key) => string.Join(' ', key.Split('-').Skip(1));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ReadStyleVersion(string document)
    {
        var version = Regex.Match(document, @"\A# Fatewake Visual Style Bible (?:\u2014|-) v([1-9][0-9]*)\r?\n",
            RegexOptions.CultureInvariant);
        return version.Success ? version.Groups[1].Value
            : throw new InvalidDataException("The Style Bible requires a versioned title.");
    }

    private static string RequiredStyleSection(string document, string heading)
    {
        var normalized = document.Replace("\r\n", "\n");
        var marker = $"\n## {heading}\n";
        var start = normalized.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0 || normalized.IndexOf(marker, start + marker.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException($"The Style Bible requires exactly one '{heading}' section.");
        var body = normalized[(start + marker.Length)..];
        var end = body.IndexOf("\n## ", StringComparison.Ordinal);
        body = (end >= 0 ? body[..end] : body).Trim();
        return body.Length > 0 ? body
            : throw new InvalidDataException($"The Style Bible section '{heading}' must not be empty.");
    }

}
