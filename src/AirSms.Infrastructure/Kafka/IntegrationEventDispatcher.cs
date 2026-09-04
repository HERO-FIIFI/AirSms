using AirSms.Contracts.Events;
using AirSms.Infrastructure.Outbox.Handlers;
using AirSms.Infrastructure.Persistence;

namespace AirSms.Infrastructure.Kafka;

public sealed class IntegrationEventDispatcher(
    IntegrationEventMapper mapper,
    AuditProjectionHandler auditProjection)
{
    public async Task DispatchAsync(
        IntegrationEventEnvelope integrationEvent,
        AirSmsDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var domainEvent = mapper.ToDomainEvent(integrationEvent);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await auditProjection.HandleAsync(integrationEvent, domainEvent, dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
