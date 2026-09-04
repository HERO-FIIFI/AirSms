using System.Text.Json;
using AirSms.Contracts.Events;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AirSms.Infrastructure.Persistence;

namespace AirSms.Infrastructure.Kafka;

public sealed class KafkaIntegrationEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<KafkaIntegrationEventConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = $"{options.Value.ClientId}-consumer",
            GroupId = options.Value.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(options.Value.IncidentEventsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var integrationEvent = await ProcessMessageAsync(result.Message.Value, stoppingToken);
                consumer.Commit(result);

                logger.LogInformation(
                    "Processed integration event {IntegrationEventId} type {IntegrationEventType} from partition {KafkaPartition} offset {KafkaOffset}; offset committed",
                    integrationEvent.EventId,
                    integrationEvent.EventType,
                    result.Partition.Value,
                    result.Offset.Value);
            }
            catch (ConsumeException exception)
            {
                logger.LogWarning(exception, "Kafka consume failed.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Kafka message processing failed; offset was not committed.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    public async Task<IntegrationEventEnvelope> ProcessMessageAsync(
        string messageValue,
        CancellationToken cancellationToken = default)
    {
        var integrationEvent = JsonSerializer.Deserialize<IntegrationEventEnvelope>(
            messageValue,
            JsonOptions);

        if (integrationEvent is null)
        {
            throw new InvalidOperationException("Kafka message could not be deserialized.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AirSmsDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IntegrationEventDispatcher>();

        logger.LogInformation(
            "Processing integration event {IntegrationEventId} type {IntegrationEventType} aggregate {AggregateId}",
            integrationEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.AggregateId);

        await dispatcher.DispatchAsync(integrationEvent, dbContext, cancellationToken);

        logger.LogInformation(
            "Processed integration event {IntegrationEventId} type {IntegrationEventType} aggregate {AggregateId}",
            integrationEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.AggregateId);

        return integrationEvent;
    }
}
