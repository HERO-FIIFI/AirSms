using AirSms.Infrastructure.Persistence;
using AirSms.Infrastructure.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AirSms.Infrastructure.Outbox;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
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
        List<Guid> messageIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AirSmsDbContext>();
            var now = DateTime.UtcNow;
            var batchSize = Math.Clamp(options.Value.BatchSize, 1, 50);

            messageIds = await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message => message.ProcessedAt == null)
                .Where(message => message.RetryCount < options.Value.MaxRetryCount)
                .Where(message => message.NextAttemptAt == null || message.NextAttemptAt <= now)
                .OrderBy(message => message.OccurredAt)
                .Take(batchSize)
                .Select(message => message.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (var messageId in messageIds)
        {
            await ProcessMessageAsync(messageId, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AirSmsDbContext>();
            var mapper = scope.ServiceProvider.GetRequiredService<IntegrationEventMapper>();
            var enricher = scope.ServiceProvider.GetRequiredService<NotificationRecipientEnricher>();
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
                message => message.Id == messageId,
                cancellationToken);

            if (message is null ||
                message.ProcessedAt is not null ||
                message.RetryCount >= options.Value.MaxRetryCount ||
                message.NextAttemptAt > DateTime.UtcNow)
            {
                return;
            }

            logger.LogInformation(
                "Processing outbox message {OutboxMessageId} type {OutboxEventType} attempt {OutboxAttempt}",
                message.Id,
                message.Type,
                message.RetryCount + 1);

            var integrationEvent = await enricher.EnrichAsync(
                mapper.Map(message),
                dbContext,
                cancellationToken);
            await publisher.PublishAsync(integrationEvent, cancellationToken);
            message.MarkProcessed(DateTime.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Published outbox message {OutboxMessageId} type {OutboxEventType} as integration event {IntegrationEventType}",
                message.Id,
                message.Type,
                integrationEvent.EventType);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkFailedAsync(messageId, exception, cancellationToken);
        }
    }

    private async Task MarkFailedAsync(
        Guid messageId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AirSmsDbContext>();
        var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            message => message.Id == messageId,
            cancellationToken);

        if (message is null)
        {
            return;
        }

        var nextRetryCount = message.RetryCount + 1;
        DateTime? nextAttemptAt = nextRetryCount >= options.Value.MaxRetryCount
            ? null
            : DateTime.UtcNow.Add(Backoff(nextRetryCount));

        message.MarkFailed(exception.Message, nextAttemptAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            exception,
            "Failed outbox message {OutboxMessageId} type {OutboxEventType} attempt {OutboxAttempt}",
            message.Id,
            message.Type,
            message.RetryCount);
    }

    private static TimeSpan Backoff(int retryCount)
    {
        return retryCount switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(15)
        };
    }
}
