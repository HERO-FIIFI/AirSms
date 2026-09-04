using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Common;
using AirSms.Domain.Entities;
using AirSms.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirSms.Infrastructure.Persistence;

public class AirSmsDbContext(DbContextOptions<AirSmsDbContext> options)
    : DbContext(options), IAirSmsDbContext
{
    private static readonly JsonSerializerOptions OutboxJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    IQueryable<Incident> IAirSmsDbContext.Incidents => Incidents.AsNoTracking();
    IQueryable<User> IAirSmsDbContext.Users => Users.AsNoTracking();
    IQueryable<AuditEvent> IAirSmsDbContext.AuditEvents => AuditEvents.AsNoTracking();
    IQueryable<Notification> IAirSmsDbContext.Notifications => Notifications.AsNoTracking();

    public Task<Incident?> FindIncidentForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Incidents.SingleOrDefaultAsync(
            incident => incident.Id == id,
            cancellationToken);
    }

    public void AddIncident(Incident incident)
    {
        Incidents.Add(incident);
    }

    public void AddAuditEvent(AuditEvent auditEvent)
    {
        AuditEvents.Add(auditEvent);
    }

    public void AddNotification(Notification notification)
    {
        Notifications.Add(notification);
    }

    public Task<User?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return Users.AsNoTracking().SingleOrDefaultAsync(
            user => user.Email == normalizedEmail,
            cancellationToken);
    }

    public void AddUser(User user)
    {
        Users.Add(user);
    }

    public Task<Notification?> FindNotificationForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Notifications.SingleOrDefaultAsync(
            notification => notification.Id == id,
            cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = ChangeTracker
            .Entries<BaseEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(entity => entity.DomainEvents)
            .ToList();

        var outboxMessages = domainEvents
            .Select(domainEvent => new OutboxMessage(
                domainEvent.EventId,
                domainEvent.GetType().FullName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), OutboxJsonOptions),
                domainEvent.OccurredAt))
            .ToList();

        foreach (var outboxMessage in outboxMessages)
        {
            OutboxMessages.Add(outboxMessage);
        }

        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);

            foreach (var entity in entitiesWithEvents)
            {
                entity.ClearDomainEvents();
            }

            return result;
        }
        catch
        {
            foreach (var outboxMessage in outboxMessages)
            {
                Entry(outboxMessage).State = EntityState.Detached;
            }

            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
