using AirSms.Domain.Entities;

namespace AirSms.Application.Common.Interfaces;

public interface IAirSmsDbContext
{
    IQueryable<Incident> Incidents { get; }

    void AddIncident(Incident incident);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
