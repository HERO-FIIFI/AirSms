using System.Text.Json;
using AirSms.Contracts.Events;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AirSms.Infrastructure.Kafka;

public sealed class KafkaIntegrationEventPublisher : IIntegrationEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> producer;
    private readonly KafkaOptions options;
    private readonly ILogger<KafkaIntegrationEventPublisher> logger;

    public KafkaIntegrationEventPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaIntegrationEventPublisher> logger)
    {
        this.options = options.Value;
        this.logger = logger;

        producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = this.options.BootstrapServers,
            ClientId = this.options.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public async Task PublishAsync(
        IntegrationEventEnvelope integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var result = await producer.ProduceAsync(
            options.IncidentEventsTopic,
            new Message<string, string>
            {
                Key = integrationEvent.AggregateId.ToString(),
                Value = JsonSerializer.Serialize(integrationEvent, JsonOptions)
            },
            cancellationToken);

        logger.LogInformation(
            "Published integration event {IntegrationEventId} type {IntegrationEventType} aggregate {AggregateId} to {KafkaTopic} at {KafkaPartitionOffset}",
            integrationEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.AggregateId,
            options.IncidentEventsTopic,
            result.TopicPartitionOffset);
    }

    public void Dispose()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
    }
}
