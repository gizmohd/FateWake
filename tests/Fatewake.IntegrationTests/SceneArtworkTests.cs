using Fatewake.Api;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace Fatewake.IntegrationTests;

/// <summary>Checks scene-layer metadata and immutable SQL-revision request construction without a provider.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/SceneArtworkTests.md">Documentation</see>
public sealed class SceneArtworkTests
{
    private static readonly string[] SceneKeys =
    [
        "bg-day1-bedroom-dark", "prop-phone-0617-noservice", "bg-day1-house-hallway", "light-day1-power-flicker",
        "bg-day1-neighborhood-player-threshold", "char-eli-day1-observing", "char-injured-stranger-day1-supported",
        "char-michelle-day1-supporting", "fg-player-threshold-shadow", "light-day1-early-morning",
        "bg-day1-neighborhood-after-choice", "bg-day1-radio-room", "prop-radio-day1", "effect-radio-static",
        "prop-radio-timestamp-tomorrow-0617", "bg-day1-blackout-0643", "effect-power-collapse",
        "bg-day1-neighborhood-silent", "light-day1-after-shutdown"
    ];

    /// <summary>Only server-allowlisted scene layers may be described, regardless of supplied path-like keys.</summary>
    [Fact]
    public void Catalog_exposes_legitimate_scene_layers_without_a_prompt_file_fallback()
    {
        var catalog = CreateCatalog();
        Assert.Equal(19, SceneKeys.Count(catalog.Contains));
        Assert.All(SceneKeys, key => Assert.True(catalog.Contains(key)));
        Assert.False(catalog.Contains("../arbitrary"));
        Assert.False(catalog.Contains("not-a-scene"));
    }

    /// <summary>Outcome-dependent art accepts only reached, canonical outcomes and action identifiers.</summary>
    [Theory]
    [InlineData("injured_stranger_helped", "help_injured_stranger")]
    [InlineData("called_from_safety", "call_from_safety")]
    [InlineData("remained_inside", "stay_inside")]
    [InlineData("radio_investigated", "investigate_radio")]
    public void Reached_outcome_context_is_canonical_and_bound_to_its_action(string outcome, string action)
    {
        Assert.True(SceneArtContextPolicy.TryGetReachedVisualContext(outcome, out var context));
        Assert.True(SceneArtContextPolicy.IsCanonicalVisualContext(context));
        Assert.True(SceneArtContextPolicy.IsSelectedActionReached(action, "choice-wake", outcome));
        Assert.False(SceneArtContextPolicy.IsSelectedActionReached("freeform", "choice-wake", outcome));
    }

    /// <summary>Static layers remain shareable; missing or unsupported reached state cannot create outcome prompts.</summary>
    [Fact]
    public void Unsupported_or_unreached_visual_context_is_rejected_without_changing_shared_layers()
    {
        Assert.False(SceneArtContextPolicy.RequiresReachedOutcome("bg-day1-neighborhood-player-threshold"));
        Assert.True(SceneArtContextPolicy.RequiresReachedOutcome("bg-day1-neighborhood-after-choice"));
        Assert.False(SceneArtContextPolicy.TryGetReachedVisualContext(null, out _));
        Assert.False(SceneArtContextPolicy.TryGetReachedVisualContext("future_hidden_outcome", out _));
        Assert.False(SceneArtContextPolicy.IsSelectedActionReached("help_injured_stranger", "radio-fragment", "injured_stranger_helped"));
        Assert.False(SceneArtContextPolicy.IsCanonicalVisualContext("private profile information"));
    }

    /// <summary>A stored SQL revision resolves successfully when no authored prompt Markdown exists locally.</summary>
    [Fact]
    public void Sql_prompt_revision_resolves_without_local_prompt_markdown()
    {
        var root = FindRepositoryRoot();
        var artRoot = Path.Combine(root, "art");
        var promptRoot = Path.Combine(artRoot, "prompts");
        Assert.False(Directory.Exists(promptRoot) && Directory.EnumerateFiles(promptRoot, "*.md").Any());
        var catalog = new SceneArtCatalog(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ArtGeneration:CatalogRoot"] = artRoot }).Build());
        var revision = new ArtPromptRevisionRecord
        {
            Id = Guid.NewGuid(),
            RevisionNumber = 1,
            PositivePrompt = "An intact bedroom at dawn.",
            Source = "seed"
        };

        var request = SceneArtRevision.Create(catalog, "bg-day1-bedroom-dark", revision);

        Assert.StartsWith("Hand-pixelled high-color 32-bit-era console game illustration", request.ResolvedPrompt);
        Assert.Contains("Subject details: An intact bedroom at dawn.", request.ResolvedPrompt);
        Assert.Equal(revision.Id, request.PromptRevisionId);
        Assert.NotNull(request.LegacyVisualFingerprint);
    }

    /// <summary>Subject text is combined with server-enforced style, role, geometry and revision identity.</summary>
    [Fact]
    public void Prompt_revision_builds_constrained_deterministic_artwork_request()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = "None"
        }).Build();
        var context = new ArtPromptAuthoringRequest("bg:shared:3:hash", "bg-day1-bedroom-dark", "Background",
            "context-hash", "3", "Day 1 at dawn in a fictional intact suburban home.", "Opaque portrait background.",
            "Late-1990s high-color 2D console style.", "No text or watermark.");
        var firstRevision = new ArtPromptRevisionRecord
        {
            Id = Guid.NewGuid(),
            RevisionNumber = 1,
            PositivePrompt = "An empty bedroom at dawn.",
            Source = "seed"
        };
        var first = ArtPromptWorkRequestBuilder.Create(configuration, context, firstRevision);
        var secondRevision = new ArtPromptRevisionRecord
        {
            Id = Guid.NewGuid(),
            RevisionNumber = 2,
            PositivePrompt = "A quiet bedroom with an unmade bed.",
            Source = "admin"
        };
        var second = ArtPromptWorkRequestBuilder.Create(configuration, context, secondRevision);

        Assert.StartsWith("Late-1990s high-color 2D console style.", first.ResolvedPrompt);
        Assert.Contains("Subject details: An empty bedroom at dawn.", first.ResolvedPrompt);
        Assert.Contains("Enforced role: environment only", first.ResolvedPrompt);
        Assert.Contains("Late-1990s high-color 2D console style.", first.ResolvedPrompt);
        Assert.Contains("portrait background", first.ResolvedPrompt);
        Assert.Contains("people", first.NegativeInstructions!);
        Assert.NotEqual(first.VisualFingerprint, second.VisualFingerprint);
        Assert.Equal(firstRevision.Id, first.PromptRevisionId);
        Assert.Equal(1, first.PromptRevisionNumber);
    }

    private static SceneArtCatalog CreateCatalog() => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>()).Build());

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Fatewake.slnx")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
