using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Fatewake.Infrastructure.Persistence;

namespace Fatewake.Infrastructure.Art;

/// <summary>Creates deterministic image work requests from immutable prompt revisions and trusted server constraints.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtPromptWorkRequestBuilder.md">Documentation</see>.</remarks>
public static class ArtPromptWorkRequestBuilder
{
    /// <summary>Places the shared style, role and framing before the subject and continuity details.</summary>
    public static ArtWorkRequest Create(IConfiguration configuration, ArtPromptAuthoringRequest context,
        ArtPromptRevisionRecord revision, Guid? accountId = null, Guid? survivorId = null, int priority = 0)
    {
        var roleConstraints = context.AssetType switch
        {
            "CharacterPose" => "single character in one complete pose, centered and filling the frame with a small margin; preserve canonical age, readable facial features, body proportions and clothing",
            "Background" => "environment only; no people, characters, faces or bodies",
            "Prop" => "one isolated reusable object only; no scenery or extra objects",
            "Foreground" => "transparent reusable foreground only; no full-scene background",
            "Lighting" => "transparent lighting effect only; no people or solid background",
            _ => "transparent isolated visual effect only; no people or solid background"
        };
        var subject = revision.PositivePrompt.Trim();
        var resolved = $"{context.RuntimeStyle}\nEnforced role: {roleConstraints}\n{context.Framing}\nSubject details: {subject}\n{context.CanonicalContext}";
        var negative = string.Join(", ", new[] { context.RuntimeExclusions, revision.NegativePrompt, RoleExclusions(context.AssetType) }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var fingerprint = Hash($"scene-art-v5\n{context.StyleBibleVersion}\n{context.AssetKey}\n" +
            $"{context.ContextFingerprint}\n{revision.Id:N}\n{revision.RevisionNumber}\n{resolved}\n{negative}");
        var provider = configuration["ArtGeneration:Provider"] ?? "None";
        var model = provider.ToLowerInvariant() switch
        {
            "openai" => configuration["ArtGeneration:OpenAI:Model"] ?? new OpenAiArtGenerationOptions().Model,
            "local" => configuration["ArtGeneration:Local:Model"] ?? new LocalArtGenerationOptions().Model,
            "comfyui" => configuration["ArtGeneration:ComfyUI:Model"] ?? "workflow",
            _ => "None"
        };
        return new(context.AssetKey, context.AssetType, fingerprint, resolved, provider, model,
            CharacterKey: context.AssetType == "CharacterPose" ? context.AssetKey : null,
            LocationKey: context.AssetType == "Background" ? context.AssetKey : null,
            NegativeInstructions: negative, PromptTemplateId: context.AssetKey,
            PromptTemplateVersion: revision.RevisionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StyleBibleVersion: context.StyleBibleVersion, AccountId: accountId, SurvivorId: survivorId,
            PromptRevisionId: revision.Id, PromptRevisionNumber: revision.RevisionNumber,
            LegacyVisualFingerprint: context.LegacyVisualFingerprint);
    }

    private static string RoleExclusions(string role) => role switch
    {
        "CharacterPose" => "sprite sheets, multiple panels, inset portraits, alternate poses, extra people, duplicated limbs, incorrect age, changed identity",
        "Background" => "person, people, man, woman, child, human, face, body, hands, character portrait",
        "Prop" => "room, furniture, scenery, multiple objects, text, letters, numbers",
        _ => "full scene background, people, text, letters, numbers"
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
