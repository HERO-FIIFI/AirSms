using AirSms.Domain.Entities;

namespace AirSms.Application.Common.Interfaces;

public interface IAirSmsDbContext
{
    IQueryable<Incident> Incidents { get; }

    Task<Incident?> FindIncidentForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddIncident(Incident incident);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
