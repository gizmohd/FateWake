using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Rag;

public sealed class RagClaimExtractionWorker(IServiceScopeFactory scopes, ClaimExtractionOptions options,
    ILogger<RagClaimExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            logger.LogWarning("RAG claim extraction is enabled but ClaimExtraction:ApiKey is missing");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<RagClaimService>();
                var extractor = scope.ServiceProvider.GetRequiredService<IClaimExtractor>();
                if (await service.ExtractNextAsync(extractor, stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RAG claim extraction pass failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
