using AirSms.Domain.Entities;

namespace AirSms.Application.Common.Interfaces;

public interface IAirSmsDbContext
{
    IQueryable<Incident> Incidents { get; }
    IQueryable<User> Users { get; }
    IQueryable<AuditEvent> AuditEvents { get; }
    IQueryable<Notification> Notifications { get; }

    Task<Incident?> FindIncidentForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddIncident(Incident incident);
    void AddAuditEvent(AuditEvent auditEvent);
    void AddNotification(Notification notification);

    Task<User?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<User?> FindUserForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddUser(User user);

    Task<Notification?> FindNotificationForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
