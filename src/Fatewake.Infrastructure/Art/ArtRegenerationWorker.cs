using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fatewake.Infrastructure.Art;

/// <summary>Polls pending artwork regeneration requests and queues ComfyUI replacement jobs after the provider responds.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtRegenerationWorker.md">ArtRegenerationWorker documentation</see>.</remarks>
public sealed class ArtRegenerationWorker(
    IServiceScopeFactory scopes,
    IHttpClientFactory httpClientFactory,
    IOptions<ComfyUiArtGenerationOptions> comfyOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await IsComfyAvailableAsync(stoppingToken))
                    await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Availability and queue failures are retried by the next bounded polling interval.
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task<bool> IsComfyAvailableAsync(CancellationToken ct)
    {
        var options = comfyOptions.Value;
        if (!options.Enabled)
            return false;
        using var client = httpClientFactory.CreateClient("art.comfyui.health");
        client.BaseAddress = new Uri(options.BaseUrl);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            client.DefaultRequestHeaders.TryAddWithoutValidation(options.ApiKeyHeader, options.ApiKey);
        using var response = await client.GetAsync("system_stats", ct);
        return response.IsSuccessStatusCode;
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        var jobs = scope.ServiceProvider.GetRequiredService<IArtWorkJobFactory>();
        var now = DateTimeOffset.UtcNow;
        var pending = await db.ArtRegenerationRequests.AsNoTracking()
            .Where(x => x.Status == "Pending" && (x.NextEligibleAt == null || x.NextEligibleAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        foreach (var item in pending)
        {
            var request = JsonSerializer.Deserialize<ArtWorkRequest>(item.RequestJson);
            if (request is null)
                continue;
            var regenerationRequest = request with
            {
                Provider = "ComfyUI",
                Model = item.TargetModel ?? request.Model,
                ForceProviderRegeneration = true
            };
            try
            {
                await jobs.CreateAsync(regenerationRequest, async (jobId, token) =>
                {
                    var updated = await db.ArtRegenerationRequests.Where(row => row.Id == item.Id && row.Status == "Pending")
                        .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "Processing")
                            .SetProperty(row => row.RegenerationWorkJobId, jobId)
                            .SetProperty(row => row.AttemptCount, row => row.AttemptCount + 1)
                            .SetProperty(row => row.UpdatedAt, now), token);
                    if (updated == 0)
                        throw new RegenerationAlreadyHandledException();
                }, ct);
            }
            catch (RegenerationAlreadyHandledException)
            {
                // Another worker or a debug reset changed this request before it could be queued.
            }
        }
    }

    private sealed class RegenerationAlreadyHandledException : Exception;
}
