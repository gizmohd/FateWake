using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace Fatewake.Infrastructure.Art;

/// <summary>Uses ComfyUI first and queues a low-cost OpenAI fallback for later ComfyUI replacement.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/FallbackComfyUiArtMasterGenerator.md">FallbackComfyUiArtMasterGenerator documentation</see>.</remarks>
public sealed class FallbackComfyUiArtMasterGenerator(
    ComfyUiArtMasterGenerator comfy,
    OpenAiArtMasterGenerator openAi,
    IOptions<OpenAiArtGenerationOptions> openAiOptions,
    IArtRegenerationStore regenerations) : IArtMasterGenerator
{
    public async Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation, CancellationToken ct = default)
    {
        try
        {
            return await comfy.GenerateAsync(invocation, ct);
        }
        catch (HttpRequestException ex)
        {
            return await GenerateFallbackAsync(invocation, ex.Message, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return await GenerateFallbackAsync(invocation, ex.Message, ct);
        }
    }

    private async Task<GeneratedArtMaster> GenerateFallbackAsync(ArtGenerationInvocation invocation, string reason, CancellationToken ct)
    {
        var options = openAiOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("ComfyUI is unavailable and OpenAI fallback is not configured.");

        var fallbackRequest = invocation.Request with
        {
            Provider = "OpenAI",
            Model = options.FallbackModel
        };
        var fallbackInvocation = invocation with { Request = fallbackRequest };
        var generated = await openAi.GenerateAsync(fallbackInvocation, ct);

        if (invocation.WorkJobId is { } workJobId)
        {
            await regenerations.QueueAsync(
                workJobId,
                invocation.Request.VisualFingerprint,
                JsonSerializer.Serialize(invocation.Request),
                "ComfyUI",
                invocation.Request.Model,
                $"ComfyUI unavailable; OpenAI fallback used: {reason}",
                ct);
        }
        return generated with { };
    }
}
