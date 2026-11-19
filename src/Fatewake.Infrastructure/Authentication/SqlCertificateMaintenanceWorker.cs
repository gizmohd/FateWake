using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Maintains SQL wrapping certificates hourly without making a transient failure terminate the host.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/SqlCertificateMaintenanceWorker.md">Documentation</see>
public sealed class SqlCertificateMaintenanceWorker(SqlKeyRingCertificateStore certificates,
    ILogger<SqlCertificateMaintenanceWorker> logger) : BackgroundService
{
    /// <summary>Retries failed maintenance on the next hourly tick; key use still reports storage failures explicitly.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                certificates.Maintain(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError("Data Protection maintenance will retry on the next hourly tick after {ExceptionType}", ex.GetType().FullName);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
