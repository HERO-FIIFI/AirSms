using System.Text.Json;
using AirSms.Contracts.Events;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;
using AirSms.Domain.Events;
using AirSms.Infrastructure.Kafka;
using AirSms.Infrastructure.Outbox;
using AirSms.Infrastructure.Outbox.Handlers;
using AirSms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AirSms.Tests;

public class OutboxProcessorTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=airsms;Username=airsms;Password=airsms_dev_password";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MapsOutboxDomainEventToVersionedIntegrationEnvelope()
    {
        var incidentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var payload = $$"""
            {"IncidentId":"{{incidentId}}","ReportedByUserId":"{{reporterId}}","EventId":"{{eventId}}","OccurredAt":"2026-09-02T00:00:00Z"}
            """;
        var message = new OutboxMessage(
            eventId,
            typeof(IncidentCreatedDomainEvent).FullName!,
            payload,
            DateTime.UtcNow);

        var envelope = new IntegrationEventMapper().Map(message);

        Assert.Equal(eventId, envelope.EventId);
        Assert.Equal("incident.created", envelope.EventType);
        Assert.Equal(1, envelope.EventVersion);
        Assert.Equal(incidentId, envelope.AggregateId);
        Assert.Equal("Incident", envelope.AggregateType);
        Assert.Equal(reporterId, envelope.ActorUserId);
        Assert.Contains("IncidentId", envelope.Payload.GetRawText());
    }

    [Fact]
    public async Task PublisherSuccessMarksOutboxProcessed()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var incidentId = Guid.NewGuid();
        var message = CreateAssignedOutboxMessage(incidentId, Guid.NewGuid(), Guid.NewGuid());
        var publisher = new RecordingPublisher();

        await using (var context = CreateContext())
        {
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
        }

        try
        {
            await CreateProcessor(publisher).ProcessPendingAsync();

            await using var verify = CreateContext();
            var processed = await verify.OutboxMessages.SingleAsync(item => item.Id == message.Id);
            Assert.NotNull(processed.ProcessedAt);
            Assert.Null(processed.Error);
            var published = Assert.Single(publisher.Published);
            Assert.Equal("incident.assigned", published.EventType);
            Assert.Equal(incidentId, published.AggregateId);
        }
        finally
        {
            await DeleteIncidentRowsAsync(incidentId);
            await DeleteOutboxAsync(message.Id);
        }
    }

    [Fact]
    public async Task PublisherFailureDoesNotMarkOutboxProcessed()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var message = CreateAssignedOutboxMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using (var context = CreateContext())
        {
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
        }

        try
        {
            await CreateProcessor(new RecordingPublisher(shouldFail: true)).ProcessPendingAsync();

            await using var verify = CreateContext();
            var failed = await verify.OutboxMessages.SingleAsync(item => item.Id == message.Id);
            Assert.Null(failed.ProcessedAt);
            Assert.Equal(1, failed.RetryCount);
            Assert.NotNull(failed.Error);
            Assert.NotNull(failed.NextAttemptAt);
        }
        finally
        {
            await DeleteOutboxAsync(message.Id);
        }
    }

    [Fact]
    public async Task MaxRetryLeavesMessageUnprocessedWithoutNextAttempt()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var message = CreateAssignedOutboxMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using (var context = CreateContext())
        {
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
        }

        try
        {
            await CreateProcessor(new RecordingPublisher(shouldFail: true), maxRetryCount: 1)
                .ProcessPendingAsync();

            await using var verify = CreateContext();
            var failed = await verify.OutboxMessages.SingleAsync(item => item.Id == message.Id);
            Assert.Null(failed.ProcessedAt);
            Assert.Equal(1, failed.RetryCount);
            Assert.NotNull(failed.Error);
            Assert.Null(failed.NextAttemptAt);
        }
        finally
        {
            await DeleteOutboxAsync(message.Id);
        }
    }

    [Fact]
    public async Task ConsumerDispatchCreatesAuditRow()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var incidentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var envelope = CreateCreatedEnvelope(incidentId, eventId, Guid.NewGuid());

        try
        {
            await CreateConsumer().ProcessMessageAsync(JsonSerializer.Serialize(envelope, WebJson));

            await using var verify = CreateContext();
            var audit = await verify.AuditEvents.SingleAsync(audit => audit.EventId == eventId);
            Assert.Equal("incident.created", audit.EventType);
            Assert.Equal(incidentId, audit.AggregateId);
        }
        finally
        {
            await DeleteIncidentRowsAsync(incidentId);
            await DeleteAuditByEventIdAsync(eventId);
        }
    }

    [Fact]
    public async Task DuplicateKafkaDeliveryDoesNotDuplicateAuditRows()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var incidentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var message = JsonSerializer.Serialize(
            CreateCreatedEnvelope(incidentId, eventId, Guid.NewGuid()),
            WebJson);
        var consumer = CreateConsumer();

        try
        {
            await consumer.ProcessMessageAsync(message);
            await consumer.ProcessMessageAsync(message);

            await using var verify = CreateContext();
            Assert.Equal(1, await verify.AuditEvents.CountAsync(audit => audit.EventId == eventId));
        }
        finally
        {
            await DeleteIncidentRowsAsync(incidentId);
            await DeleteAuditByEventIdAsync(eventId);
        }
    }

    [Fact]
    public async Task UnknownIntegrationEventTypeFailsSafely()
    {
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            "incident.unknown",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Incident",
            null,
            null,
            JsonDocument.Parse("{}").RootElement.Clone());

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            CreateConsumer().ProcessMessageAsync(JsonSerializer.Serialize(envelope, WebJson)));
    }

    [Fact]
    public async Task UnknownIntegrationEventVersionFailsSafely()
    {
        var envelope = CreateCreatedEnvelope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) with
        {
            EventVersion = 99
        };

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            CreateConsumer().ProcessMessageAsync(JsonSerializer.Serialize(envelope, WebJson)));
    }

    private static Incident CreateIncident(Guid? reporterId = null)
    {
        return new Incident(
            "Outbox processor check",
            "Verifies outbox processing.",
            IncidentCategory.Technical,
            IncidentSeverity.High,
            reporterId ?? Guid.NewGuid());
    }

    private static OutboxMessage CreateAssignedOutboxMessage(
        Guid incidentId,
        Guid assigneeId,
        Guid eventId)
    {
        var payload = $$"""
            {"IncidentId":"{{incidentId}}","AssignedToUserId":"{{assigneeId}}","ActorUserId":"{{Guid.NewGuid()}}","EventId":"{{eventId}}","OccurredAt":"2026-09-02T00:00:00Z"}
            """;

        return new OutboxMessage(
            eventId,
            typeof(IncidentAssignedDomainEvent).FullName!,
            payload,
            DateTime.UtcNow);
    }

    private static IntegrationEventEnvelope CreateCreatedEnvelope(
        Guid incidentId,
        Guid eventId,
        Guid reporterId)
    {
        var payload = $$"""
            {"IncidentId":"{{incidentId}}","ReportedByUserId":"{{reporterId}}","EventId":"{{eventId}}","OccurredAt":"2026-09-02T00:00:00Z"}
            """;

        return new IntegrationEventEnvelope(
            eventId,
            "incident.created",
            1,
            DateTime.Parse("2026-09-02T00:00:00Z").ToUniversalTime(),
            incidentId,
            "Incident",
            reporterId,
            null,
            JsonDocument.Parse(payload).RootElement.Clone());
    }

    private static IntegrationEventEnvelope CreateAssignedEnvelope(
        Guid incidentId,
        Guid assigneeId,
        Guid eventId)
    {
        var actorUserId = Guid.NewGuid();
        var payload = $$"""
            {"IncidentId":"{{incidentId}}","AssignedToUserId":"{{assigneeId}}","ActorUserId":"{{actorUserId}}","EventId":"{{eventId}}","OccurredAt":"2026-09-02T00:00:00Z"}
            """;

        return new IntegrationEventEnvelope(
            eventId,
            "incident.assigned",
            1,
            DateTime.Parse("2026-09-02T00:00:00Z").ToUniversalTime(),
            incidentId,
            "Incident",
            actorUserId,
            null,
            JsonDocument.Parse(payload).RootElement.Clone());
    }

    private static async Task<OutboxMessage> GetOutboxMessageAsync<TEvent>(Guid incidentId)
    {
        await using var context = CreateContext();
        return await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.Type == typeof(TEvent).FullName &&
                message.Payload.Contains(incidentId.ToString()));
    }

    private static OutboxProcessor CreateProcessor(
        IIntegrationEventPublisher publisher,
        int maxRetryCount = 5)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AirSmsDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddSingleton(new IntegrationEventMapper());
        services.AddScoped<NotificationRecipientEnricher>();
        services.AddSingleton<IIntegrationEventPublisher>(publisher);

        var provider = services.BuildServiceProvider();
        return new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions
            {
                BatchSize = 20,
                MaxRetryCount = maxRetryCount,
                PollIntervalSeconds = 1
            }),
            NullLogger<OutboxProcessor>.Instance);
    }

    private static KafkaIntegrationEventConsumer CreateConsumer()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AirSmsDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddSingleton(new IntegrationEventMapper());
        services.AddScoped<IntegrationEventDispatcher>();
        services.AddScoped<AuditProjectionHandler>();

        var provider = services.BuildServiceProvider();
        return new KafkaIntegrationEventConsumer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new KafkaOptions
            {
                Enabled = false,
                BootstrapServers = "localhost:9092"
            }),
            NullLogger<KafkaIntegrationEventConsumer>.Instance);
    }

    private static AirSmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AirSmsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AirSmsDbContext(options);
    }

    private static async Task<bool> DatabaseAvailable()
    {
        try
        {
            await using var context = CreateContext();
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static async Task DeleteIncidentRowsAsync(Guid incidentId)
    {
        await using var context = CreateContext();
        await context.Notifications
            .Where(notification => notification.RelatedIncidentId == incidentId)
            .ExecuteDeleteAsync();
        await context.AuditEvents
            .Where(audit => audit.AggregateId == incidentId)
            .ExecuteDeleteAsync();
        await context.OutboxMessages
            .Where(message => message.Payload.Contains(incidentId.ToString()))
            .ExecuteDeleteAsync();
        await context.Incidents
            .Where(incident => incident.Id == incidentId)
            .ExecuteDeleteAsync();
    }

    private static async Task DeleteOutboxAsync(params Guid[] ids)
    {
        await using var context = CreateContext();
        await context.OutboxMessages
            .Where(message => ids.Contains(message.Id))
            .ExecuteDeleteAsync();
    }

    private static async Task DeleteAuditByEventIdAsync(Guid eventId)
    {
        await using var context = CreateContext();
        await context.AuditEvents
            .Where(audit => audit.EventId == eventId)
            .ExecuteDeleteAsync();
    }

    private sealed class RecordingPublisher(bool shouldFail = false) : IIntegrationEventPublisher
    {
        public List<IntegrationEventEnvelope> Published { get; } = [];

        public Task PublishAsync(
            IntegrationEventEnvelope integrationEvent,
            CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new InvalidOperationException("Kafka unavailable.");
            }

            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
