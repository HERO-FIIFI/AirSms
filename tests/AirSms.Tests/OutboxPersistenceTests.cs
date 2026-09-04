using AirSms.Domain.Entities;
using AirSms.Domain.Enums;
using AirSms.Domain.Events;
using AirSms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Tests;

public class OutboxPersistenceTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=airsms;Username=airsms;Password=airsms_dev_password";

    [Fact]
    public async Task SavingIncidentCreationCreatesOutboxRow()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var incident = CreateIncident();

        await using (var context = CreateContext())
        {
            context.Incidents.Add(incident);
            await context.SaveChangesAsync();
        }

        try
        {
            await using var context = CreateContext();
            var message = await context.OutboxMessages.SingleAsync(
                message => message.Payload.Contains(incident.Id.ToString()));

            Assert.Contains(nameof(IncidentCreatedDomainEvent), message.Type);
            Assert.Contains(incident.ReportedByUserId.ToString(), message.Payload);
        }
        finally
        {
            await DeleteIncidentAndOutboxAsync(incident.Id);
        }
    }

    [Fact]
    public async Task AssignmentStateAndOutboxEventPersistTogether()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var incident = CreateIncident();
        var assigneeId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using (var context = CreateContext())
        {
            context.Incidents.Add(incident);
            await context.SaveChangesAsync();
        }

        try
        {
            await using (var context = CreateContext())
            {
                var stored = await context.Incidents.SingleAsync(item => item.Id == incident.Id);
                stored.AssignTo(assigneeId, actorId);
                await context.SaveChangesAsync();
            }

            await using var verify = CreateContext();
            var updated = await verify.Incidents.AsNoTracking().SingleAsync(item => item.Id == incident.Id);
            var message = await verify.OutboxMessages.SingleAsync(
                message => message.Type.Contains(nameof(IncidentAssignedDomainEvent)) &&
                    message.Payload.Contains(incident.Id.ToString()));

            Assert.Equal(IncidentStatus.Assigned, updated.Status);
            Assert.Equal(assigneeId, updated.AssignedToUserId);
            Assert.Contains(assigneeId.ToString(), message.Payload);
            Assert.Contains(actorId.ToString(), message.Payload);
        }
        finally
        {
            await DeleteIncidentAndOutboxAsync(incident.Id);
        }
    }

    [Fact]
    public async Task FailedTransactionDoesNotPersistStandaloneOutboxEvent()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var incident = CreateIncident();

        await using (var context = CreateContext())
        {
            context.Incidents.Add(incident);
            context.OutboxMessages.Add(new OutboxMessage(
                Guid.NewGuid(),
                new string('x', 501),
                $"{{\"incidentId\":\"{incident.Id}\"}}",
                DateTime.UtcNow));

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using var verify = CreateContext();
        Assert.False(await verify.Incidents.AnyAsync(item => item.Id == incident.Id));
        Assert.False(await verify.OutboxMessages.AnyAsync(
            message => message.Payload.Contains(incident.Id.ToString())));
    }

    private static Incident CreateIncident()
    {
        return new Incident(
            "Outbox persistence check",
            "Verifies domain events are stored atomically.",
            IncidentCategory.Technical,
            IncidentSeverity.High,
            Guid.NewGuid());
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

    private static async Task DeleteIncidentAndOutboxAsync(Guid incidentId)
    {
        await using var context = CreateContext();
        await context.OutboxMessages
            .Where(message => message.Payload.Contains(incidentId.ToString()))
            .ExecuteDeleteAsync();
        await context.Incidents
            .Where(incident => incident.Id == incidentId)
            .ExecuteDeleteAsync();
    }
}
