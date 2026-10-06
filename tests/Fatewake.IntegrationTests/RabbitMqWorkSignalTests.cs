using Fatewake.Infrastructure.Work;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies real RabbitMQ signals wake idle workers before their PostgreSQL polling deadline.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/RabbitMqWorkSignalTests.md">RabbitMqWorkSignalTests documentation</see>
public sealed class RabbitMqWorkSignalTests
{
    /// <summary>A published availability hint is consumed and wakes the matching queue.</summary>
    [Fact]
    public async Task Broker_signal_wakes_idle_worker()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_TEST_RABBITMQ");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Skip("FATEWAKE_TEST_RABBITMQ is required for the broker integration test.");
        var ct = TestContext.Current.CancellationToken;
        var queue = $"test.wakeup.{Guid.NewGuid():N}";
        var rabbit = Options.Create(new RabbitMqWorkOptions { ConnectionString = connection, Exchange = queue });
        var work = Options.Create(new WorkExecutionOptions
        {
            QueueConcurrency = new Dictionary<string, int> { [queue] = 1 },
            PollInterval = TimeSpan.FromMilliseconds(100)
        });
        using var wakeup = new WorkQueueWakeup();
        using var consumer = new RabbitMqWorkSignalConsumer(rabbit, work, wakeup, NullLogger<RabbitMqWorkSignalConsumer>.Instance);
        await using var publisher = new RabbitMqWorkSignalBus(rabbit);
        await consumer.StartAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var wait = wakeup.WaitAsync(queue, TimeSpan.FromMinutes(1), timeout.Token);
            while (!wait.IsCompleted)
            {
                await publisher.SignalAsync(queue, Guid.NewGuid(), timeout.Token);
                await Task.WhenAny(wait, Task.Delay(100, timeout.Token));
            }
            await wait;
        }
        finally
        {
            await consumer.StopAsync(ct);
            var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(connection) };
            await using var cleanupConnection = await factory.CreateConnectionAsync(ct);
            await using var channel = await cleanupConnection.CreateChannelAsync(cancellationToken: ct);
            await channel.QueueDeleteAsync(queue, false, false, cancellationToken: ct);
            await channel.ExchangeDeleteAsync(queue, cancellationToken: ct);
        }
    }
}
