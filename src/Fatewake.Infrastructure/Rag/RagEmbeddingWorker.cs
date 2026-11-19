using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Rag;

/// <summary>Continuously backfills vectors for imported and generated-story chunks; failures retry on the next poll.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/RagEmbeddingWorker.md">Documentation</see>.</remarks>
public sealed class RagEmbeddingWorker(IServiceScopeFactory scopes, EmbeddingOptions options,
    ILogger<RagEmbeddingWorker> log) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idle = TimeSpan.FromSeconds(Math.Max(1, options.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            var embedded = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                embedded = await scope.ServiceProvider.GetRequiredService<IRagEmbeddingService>()
                    .EmbedPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "RAG embedding backfill pass failed");
            }
            if (embedded == 0)
            {
                try
                {
                    await Task.Delay(idle, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
