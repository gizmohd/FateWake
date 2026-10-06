namespace Fatewake.Infrastructure.Work;
/// <summary>Configures RabbitMQ work-availability signaling.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/RabbitMqWorkOptions.md">RabbitMqWorkOptions documentation</see>.</remarks>
public sealed class RabbitMqWorkOptions
{
    /// <summary>Gets or sets the AMQP connection string.</summary>
    public string ConnectionString{get;set;}="amqp://guest:guest@localhost:5672";
    /// <summary>Gets or sets the durable direct exchange used for work signals.</summary>
    public string Exchange{get;set;}="fatewake.work";
}