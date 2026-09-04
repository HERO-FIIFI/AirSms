using AirSms.Contracts.Events;
using AirSms.Notifications.Models;
using AirSms.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Notifications.Services;

public sealed class NotificationEventHandler(NotificationsDbContext dbContext)
{
    public async Task HandleAsync(
        IntegrationEventEnvelope integrationEvent,
        CancellationToken cancellationToken = default)
    {
        if (integrationEvent.EventVersion != 1)
        {
            throw new NotSupportedException(
                $"Unsupported integration event version '{integrationEvent.EventVersion}'.");
        }

        var notification = integrationEvent.EventType switch
        {
            IncidentIntegrationEventTypes.Assigned => CreateNotifications(
                integrationEvent,
                "IncidentAssigned",
                "Incident assigned",
                $"Incident {integrationEvent.AggregateId} has been assigned to you."),
            IncidentIntegrationEventTypes.Resolved => CreateNotifications(
                integrationEvent,
                "IncidentResolved",
                "Incident resolved",
                $"Incident {integrationEvent.AggregateId} has been resolved."),
            _ => []
        };

        if (notification.Count == 0)
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var item in notification)
        {
            var exists = await dbContext.Notifications.AnyAsync(existing =>
                existing.SourceEventId == item.SourceEventId &&
                existing.UserId == item.UserId &&
                existing.Type == item.Type,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            dbContext.Notifications.Add(item);
            dbContext.NotificationDeliveries.Add(new NotificationDelivery(
                item.Id,
                NotificationChannel.InApp,
                item.UserId.ToString(),
                DeliveryStatus.Delivered));

            var recipient = integrationEvent.Recipients?.SingleOrDefault(r => r.UserId == item.UserId);
            if (!string.IsNullOrWhiteSpace(recipient?.Email))
            {
                dbContext.NotificationDeliveries.Add(new NotificationDelivery(
                    item.Id,
                    NotificationChannel.Email,
                    recipient.Email));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static List<Notification> CreateNotifications(
        IntegrationEventEnvelope integrationEvent,
        string type,
        string title,
        string message)
    {
        return integrationEvent.Recipients?
            .GroupBy(recipient => recipient.UserId)
            .Select(group => group.First())
            .Select(recipient => new Notification(
                recipient.UserId,
                type,
                title,
                message,
                integrationEvent.AggregateId,
                integrationEvent.EventId))
            .ToList() ?? [];
    }
}
