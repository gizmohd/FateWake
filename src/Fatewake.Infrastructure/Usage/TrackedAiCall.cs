using Fatewake.Observability;
namespace Fatewake.Infrastructure.Usage;
/// <summary>Closes tracked provider attempts on success, failure, cancellation or timeout.</summary>
public static class TrackedAiCall
{
    /// <summary>Runs one provider operation, retaining reported consumption even if output processing fails.</summary>
    public static async Task<T> RunAsync<T>(IAiUsageRecorder? recorder, AiCallContext context, Func<Guid?, Task<T>> operation, CancellationToken ct)
    {
        var id = recorder is null ? (Guid?)null : await recorder.BeginAsync(context, ct);
        var outcome = "failed";
        try
        {
            var result = await operation(id);
            outcome = "succeeded";
            return result;
        }
        catch (OperationCanceledException) { outcome = ct.IsCancellationRequested ? "cancelled" : "timed_out"; throw; }
        finally
        {
            if (id is { } attempt)
            {
                using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await recorder!.FinishAsync(attempt, outcome, finish.Token);
            }
        }
    }
}
