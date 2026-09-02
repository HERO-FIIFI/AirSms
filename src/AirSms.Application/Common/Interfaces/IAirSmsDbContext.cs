using AirSms.Domain.Entities;

namespace AirSms.Application.Common.Interfaces;

public interface IAirSmsDbContext
{
    IQueryable<Incident> Incidents { get; }
    IQueryable<User> Users { get; }

    Task<Incident?> FindIncidentForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddIncident(Incident incident);

    Task<User?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    void AddUser(User user);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
