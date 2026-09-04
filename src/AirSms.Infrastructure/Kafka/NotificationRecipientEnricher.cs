using AirSms.Contracts.Events;
using AirSms.Domain.Events;
using AirSms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Infrastructure.Kafka;

public sealed class NotificationRecipientEnricher
{
    public async Task<IntegrationEventEnvelope> EnrichAsync(
        IntegrationEventEnvelope integrationEvent,
        AirSmsDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var userIds = await RecipientUserIdsAsync(integrationEvent, dbContext, cancellationToken);
        if (userIds.Count == 0)
        {
            return integrationEvent;
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new IntegrationEventRecipient(
                user.Id,
                user.Email,
                $"{user.FirstName} {user.LastName}".Trim()))
            .ToListAsync(cancellationToken);

        return integrationEvent with { Recipients = users };
    }

    private static async Task<IReadOnlySet<Guid>> RecipientUserIdsAsync(
        IntegrationEventEnvelope integrationEvent,
        AirSmsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.EventType == IncidentIntegrationEventTypes.Assigned &&
            integrationEvent.Payload.TryGetProperty(nameof(IncidentAssignedDomainEvent.AssignedToUserId), out var assignedToUserId) &&
            assignedToUserId.TryGetGuid(out var assigneeId))
        {
            return new HashSet<Guid> { assigneeId };
        }

        if (integrationEvent.EventType != IncidentIntegrationEventTypes.Resolved)
        {
            return new HashSet<Guid>();
        }

        var incident = await dbContext.Incidents.AsNoTracking().SingleOrDefaultAsync(
            incident => incident.Id == integrationEvent.AggregateId,
            cancellationToken);

        if (incident is null)
        {
            return new HashSet<Guid>();
        }

        var userIds = new HashSet<Guid> { incident.ReportedByUserId };
        if (incident.AssignedToUserId is not null)
        {
            userIds.Add(incident.AssignedToUserId.Value);
        }

        return userIds;
    }
}
