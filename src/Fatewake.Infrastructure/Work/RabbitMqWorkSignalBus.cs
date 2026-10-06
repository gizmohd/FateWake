using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Fatewake.Infrastructure.Work;

/// <summary>Publishes durable-work availability notifications to RabbitMQ.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/RabbitMqWorkSignalBus.md">RabbitMqWorkSignalBus documentation</see>. RabbitMQ accelerates dispatch but does not own durable work state.</remarks>
public sealed class RabbitMqWorkSignalBus:IWorkSignalBus,IAsyncDisposable
{
    private readonly RabbitMqWorkOptions _options; private readonly IConnection _connection; private readonly IChannel _channel;
    public RabbitMqWorkSignalBus(IOptions<RabbitMqWorkOptions> options)
    {
        _options=options.Value;var factory=new ConnectionFactory{Uri=new Uri(_options.ConnectionString)};
        _connection=factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel=_connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(_options.Exchange,ExchangeType.Direct,durable:true,autoDelete:false).GetAwaiter().GetResult();
    }
    public async Task SignalAsync(string queue,Guid stepId,CancellationToken ct=default)
    {
        await _channel.QueueDeclareAsync(queue,durable:true,exclusive:false,autoDelete:false,cancellationToken:ct);
        await _channel.QueueBindAsync(queue,_options.Exchange,queue,cancellationToken:ct);
        var body=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{stepId}));
        var props=new BasicProperties{Persistent=true,MessageId=stepId.ToString("N"),ContentType="application/json"};
        await _channel.BasicPublishAsync(_options.Exchange,queue,false,props,body,ct);
    }
    public async ValueTask DisposeAsync(){await _channel.DisposeAsync();await _connection.DisposeAsync();}
}
