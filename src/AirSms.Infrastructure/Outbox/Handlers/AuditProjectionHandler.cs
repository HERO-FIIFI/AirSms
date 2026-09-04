using AirSms.Domain.Common;
using AirSms.Domain.Entities;
using AirSms.Contracts.Events;
using AirSms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Infrastructure.Outbox.Handlers;

public sealed class AuditProjectionHandler
{
    public async Task HandleAsync(
        IntegrationEventEnvelope integrationEvent,
        IDomainEvent domainEvent,
        AirSmsDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.AuditEvents.AnyAsync(
            auditEvent => auditEvent.EventId == domainEvent.EventId,
            cancellationToken))
        {
            return;
        }

        dbContext.AuditEvents.Add(new AuditEvent(
            domainEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.AggregateId,
            integrationEvent.AggregateType,
            integrationEvent.ActorUserId,
            domainEvent.OccurredAt,
            integrationEvent.Payload.GetRawText(),
            integrationEvent.CorrelationId));
    }
}
