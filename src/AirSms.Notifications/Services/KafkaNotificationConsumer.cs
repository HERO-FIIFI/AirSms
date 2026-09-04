using System.Text.Json;
using AirSms.Contracts.Events;
using AirSms.Notifications.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace AirSms.Notifications.Services;

public sealed class KafkaNotificationConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<KafkaNotificationConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var value = options.Value;
        if (!value.Enabled || string.IsNullOrWhiteSpace(value.BootstrapServers))
        {
            logger.LogInformation("Kafka notification consumer disabled.");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = value.BootstrapServers,
            ClientId = value.ClientId,
            GroupId = value.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(value.IncidentEventsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var integrationEvent = JsonSerializer.Deserialize<IntegrationEventEnvelope>(
                    result.Message.Value,
                    JsonOptions);

                if (integrationEvent is null)
                {
                    throw new InvalidOperationException("Kafka message could not be deserialized.");
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<NotificationEventHandler>();
                await handler.HandleAsync(integrationEvent, stoppingToken);
                consumer.Commit(result);

                logger.LogInformation(
                    "Notification service processed {IntegrationEventId} type {IntegrationEventType} partition {KafkaPartition} offset {KafkaOffset}",
                    integrationEvent.EventId,
                    integrationEvent.EventType,
                    result.Partition.Value,
                    result.Offset.Value);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification Kafka processing failed; offset was not committed.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
