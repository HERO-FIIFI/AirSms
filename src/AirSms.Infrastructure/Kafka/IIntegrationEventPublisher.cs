using AirSms.Contracts.Events;

namespace AirSms.Infrastructure.Kafka;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        IntegrationEventEnvelope integrationEvent,
        CancellationToken cancellationToken = default);
}
