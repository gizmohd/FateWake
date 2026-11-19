using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fatewake.Infrastructure.Art;

/// <summary>Preserves the configured typed HTTP clients when selecting ComfyUI generation and its optional fallback.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ComfyUiGeneratorRegistration.md">Documentation</see>
public static class ComfyUiGeneratorRegistration
{
    /// <summary>Registers ComfyUI through its typed client, with OpenAI fallback only when explicitly configured.</summary>
    public static void AddComfyUiMasterGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        var comfy = configuration.GetSection(ComfyUiArtGenerationOptions.SectionName).Get<ComfyUiArtGenerationOptions>() ?? new();
        if (!comfy.Enabled)
            throw new InvalidOperationException("Enable ArtGeneration:ComfyUI when Provider is ComfyUI.");
        services.AddHttpClient<ComfyUiArtMasterGenerator>(client => client.BaseAddress = new Uri(comfy.BaseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        var fallback = configuration.GetSection(OpenAiArtGenerationOptions.SectionName).Get<OpenAiArtGenerationOptions>() ?? new();
        if (fallback.Enabled && !string.IsNullOrWhiteSpace(fallback.ApiKey))
        {
            services.AddHttpClient<OpenAiArtMasterGenerator>(client => client.BaseAddress = new Uri(fallback.BaseUrl));
            services.AddScoped<IArtMasterGenerator, FallbackComfyUiArtMasterGenerator>();
        }
        else
        {
            services.AddScoped<IArtMasterGenerator>(provider => provider.GetRequiredService<ComfyUiArtMasterGenerator>());
        }
    }
}
