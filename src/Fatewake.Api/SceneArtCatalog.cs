using System.Security.Cryptography;
using System.Text;
using Fatewake.Infrastructure.Art;

namespace Fatewake.Api;

/// <summary>Builds generation requests only for authored scene layers shipped with the API.</summary>
public sealed class SceneArtCatalog(IConfiguration configuration)
{
    private static readonly string[] Keys = [
        "bg-day1-bedroom-dark", "prop-phone-0617-noservice", "bg-day1-house-hallway", "light-day1-power-flicker",
        "bg-day1-neighborhood-player-threshold", "char-eli-day1-observing", "char-injured-stranger-day1-supported",
        "char-michelle-day1-supporting", "fg-player-threshold-shadow", "light-day1-early-morning",
        "bg-day1-neighborhood-after-choice", "bg-day1-radio-room", "prop-radio-day1", "effect-radio-static",
        "prop-radio-timestamp-tomorrow-0617", "bg-day1-blackout-0643", "effect-power-collapse",
        "bg-day1-neighborhood-silent", "light-day1-after-shutdown"
    ];

    /// <summary>Checks the shipped scene-layer allowlist.</summary>
    public bool Contains(string key) => Keys.Contains(key, StringComparer.Ordinal);

    /// <summary>Combines authored prompts and continuity documents into a stable request.</summary>
    public ArtWorkRequest Create(string key)
    {
        if (!Contains(key)) throw new ArgumentException("Unknown scene asset.", nameof(key));
        var root = configuration["ArtGeneration:CatalogRoot"] ?? Path.Combine(AppContext.BaseDirectory, "SceneArt");
        var prompt = File.ReadAllText(Path.Combine(root, "prompts", key + ".md"));
        var type = key.Split('-')[0] switch
        {
            "bg" => "Background", "char" => "CharacterPose", "prop" => "Prop",
            "fg" => "Foreground", "light" => "Lighting", "effect" => "Effect",
            _ => throw new InvalidOperationException("Unknown asset role.")
        };
        var context = type == "CharacterPose"
            ? File.ReadAllText(Path.Combine(root, "characters", key switch
            {
                "char-michelle-day1-supporting" => "MICHELLE-SUMMERS.md",
                "char-eli-day1-observing" => "ELI-SUMMERS.md",
                _ => "INJURED-STRANGER-DAY1.md"
            }))
            : File.ReadAllText(Path.Combine(root, "locations", key.Contains("neighborhood", StringComparison.Ordinal)
                ? "DAY1-NEIGHBORHOOD.md" : "DAY1-PLAYER-HOME.md"));
        var framing = type == "Background" ? "Opaque environment background, portrait 9:16 composition."
            : "Isolated reusable overlay on a transparent background; no full scene background.";
        // Diffusion text encoders need the subject first, not Markdown policy documents.
        var subject = key switch
        {
            "bg-day1-bedroom-dark" => "Empty modest contemporary suburban bedroom at dawn, neatly rumpled bed, bedside table, neutral walls, weak cool morning light through curtains, ordinary intact furniture, wide interior establishing view.",
            "prop-phone-0617-noservice" => "A single contemporary black smartphone, upright front view, large dark glass screen with a faint cold blue glow, isolated product close-up, centered, entire phone visible, plain white backdrop for background removal.",
            _ => PromptBody(prompt)
        };
        var negative = "anime, manga, superhero, fantasy armor, zombies, gore, glossy 3D render, watermark, logo, captions, speech balloons";
        if (type != "CharacterPose") negative += ", person, people, man, woman, child, human, face, body, hands, character portrait";
        if (type == "Background") negative += ", smartphone, phone, text, ruined building, rubble, post-apocalyptic decay";
        if (type == "Prop") negative += ", room, furniture, scenery, multiple objects, text, letters, numbers";
        var continuity = type is "CharacterPose" or "Background" ? context : string.Empty;
        var resolved = $"{subject}\nGrounded digitally painted graphic-novel illustration, restrained ink texture, believable materials, subdued warm gray and charcoal palette, cinematic natural light.\n{framing}\n{continuity}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"scene-art-v2\n{key}\n{resolved}\n{negative}"))).ToLowerInvariant();
        var provider = configuration["ArtGeneration:Provider"] ?? "None";
        var model = provider.ToLowerInvariant() switch
        {
            "openai" => configuration["ArtGeneration:OpenAI:Model"] ?? new OpenAiArtGenerationOptions().Model,
            "local" => configuration["ArtGeneration:Local:Model"] ?? new LocalArtGenerationOptions().Model,
            "comfyui" => configuration["ArtGeneration:ComfyUI:Model"] ?? "workflow",
            _ => "None"
        };
        return new(key, type, fingerprint, resolved, provider, model,
            CharacterKey: type == "CharacterPose" ? key : null,
            LocationKey: type == "Background" ? key : null,
            NegativeInstructions: negative, PromptTemplateId: key, PromptTemplateVersion: "2", StyleBibleVersion: "1");
    }

    private static string PromptBody(string document)
    {
        var body = document.Replace("\r\n", "\n");
        var start = body.IndexOf("## Prompt\n", StringComparison.Ordinal);
        body = start >= 0 ? body[(start + "## Prompt\n".Length)..] : body[(body.IndexOf('\n') + 1)..];
        var end = body.IndexOf("\n## ", StringComparison.Ordinal);
        return (end >= 0 ? body[..end] : body).Trim();
    }

    /// <summary>Whether the selected provider has its required configuration.</summary>
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
}
