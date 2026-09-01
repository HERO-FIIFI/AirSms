using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;

namespace AirSms.Application.Incidents;

public sealed class IncidentService(IAirSmsDbContext dbContext)
{
    public async Task<IncidentResponse> CreateAsync(
        CreateIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var incident = new Incident(
            request.Title,
            request.Description,
            request.Category,
            request.Severity,
            request.ReportedByUserId,
            request.FlightNumber,
            request.AircraftRegistration);

        dbContext.AddIncident(incident);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(incident);
    }

    public IncidentResponse? GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var incident = dbContext.Incidents.SingleOrDefault(incident => incident.Id == id);
        return incident is null ? null : ToResponse(incident);
    }

    public IReadOnlyList<IncidentResponse> List(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return dbContext.Incidents
            .OrderByDescending(incident => incident.ReportedAt)
            .AsEnumerable()
            .Select(ToResponse)
            .ToList();
    }

    private static IncidentResponse ToResponse(Incident incident)
    {
        return new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Category,
            incident.Severity,
            incident.Status,
            incident.FlightNumber,
            incident.AircraftRegistration,
            incident.ReportedByUserId,
            incident.AssignedToUserId,
            incident.ReportedAt,
            incident.ResolvedAt,
            incident.CreatedAt,
            incident.UpdatedAt);
    }
}
