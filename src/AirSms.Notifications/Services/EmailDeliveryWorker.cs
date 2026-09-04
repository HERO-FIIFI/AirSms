using AirSms.Notifications.Email;
using AirSms.Notifications.Models;
using AirSms.Notifications.Options;
using AirSms.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AirSms.Notifications.Services;

public sealed class EmailDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingAsync(stoppingToken);
            await Task.Delay(pollInterval, stoppingToken);
        }
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var now = DateTime.UtcNow;

        var deliveryIds = await dbContext.NotificationDeliveries
            .Where(delivery => delivery.Channel == NotificationChannel.Email)
            .Where(delivery => delivery.Status == DeliveryStatus.Pending)
            .Where(delivery => delivery.NextAttemptAt == null || delivery.NextAttemptAt <= now)
            .OrderBy(delivery => delivery.CreatedAt)
            .Take(20)
            .Select(delivery => delivery.Id)
            .ToListAsync(cancellationToken);

        foreach (var deliveryId in deliveryIds)
        {
            try
            {
                await ProcessDeliveryAsync(deliveryId, sender, cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(
                    exception,
                    "Email delivery {NotificationDeliveryId} changed or was deleted while processing.",
                    deliveryId);
            }
        }
    }

    private async Task ProcessDeliveryAsync(
        Guid deliveryId,
        IEmailSender sender,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var delivery = await dbContext.NotificationDeliveries.SingleOrDefaultAsync(
            item => item.Id == deliveryId,
            cancellationToken);

        if (delivery is null || delivery.Status != DeliveryStatus.Pending)
        {
            return;
        }

        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            item => item.Id == delivery.NotificationId,
            cancellationToken);
        if (notification is null)
        {
            return;
        }

        try
        {
            delivery.BeginAttempt(DateTime.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            await sender.SendAsync(
                new EmailDeliveryRequest(
                    delivery.Destination,
                    Subject(notification.Type),
                    Body(notification)),
                cancellationToken);

            delivery.MarkDelivered(DateTime.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DateTime? nextAttemptAt = delivery.AttemptCount >= options.Value.MaxAttempts
                ? null
                : DateTime.UtcNow.Add(Backoff(delivery.AttemptCount));
            delivery.MarkFailed(exception.Message, nextAttemptAt);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                exception,
                "Email delivery {NotificationDeliveryId} failed on attempt {AttemptCount}",
                delivery.Id,
                delivery.AttemptCount);
        }
    }

    private static string Subject(string notificationType)
    {
        return notificationType == "IncidentResolved"
            ? "AirSms: Incident resolved"
            : "AirSms: Incident assigned";
    }

    private static string Body(Notification notification)
    {
        return $"""
            {notification.Title}

            {notification.Message}

            Incident: {notification.RelatedIncidentId}
            Created at: {notification.CreatedAt:O}
            """;
    }

    private static TimeSpan Backoff(int attemptCount)
    {
        return attemptCount switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(15)
        };
    }
}
