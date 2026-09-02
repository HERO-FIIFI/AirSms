using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;

namespace AirSms.Application.Incidents;

public sealed class IncidentService(IAirSmsDbContext dbContext)
{
    public async Task<IncidentResponse> CreateAsync(
        CreateIncidentRequest request,
        Guid reportedByUserId,
        CancellationToken cancellationToken = default)
    {
        var incident = new Incident(
            request.Title,
            request.Description,
            request.Category,
            request.Severity,
            reportedByUserId,
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
        ListIncidentsRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        request ??= new ListIncidentsRequest();
        if (request.Page < 1)
        {
            throw new ArgumentException("Page must be greater than zero.", nameof(request));
        }

        if (request.PageSize is < 1 or > 100)
        {
            throw new ArgumentException("PageSize must be between 1 and 100.", nameof(request));
        }

        var incidents = dbContext.Incidents;

        if (request.Status is not null)
        {
            incidents = incidents.Where(incident => incident.Status == request.Status);
        }

        if (request.Severity is not null)
        {
            incidents = incidents.Where(incident => incident.Severity == request.Severity);
        }

        if (request.Category is not null)
        {
            incidents = incidents.Where(incident => incident.Category == request.Category);
        }

        if (request.AssignedToUserId is not null)
        {
            incidents = incidents.Where(incident => incident.AssignedToUserId == request.AssignedToUserId);
        }

        return incidents
            .OrderByDescending(incident => incident.ReportedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsEnumerable()
            .Select(ToResponse)
            .ToList();
    }

    public Task<IncidentResponse?> AssignAsync(
        Guid id,
        Guid assignedToUserId,
        CancellationToken cancellationToken = default)
    {
        return UpdateAsync(
            id,
            incident => incident.AssignTo(assignedToUserId),
            cancellationToken);
    }

    public Task<IncidentResponse?> StartAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, incident => incident.StartProgress(), cancellationToken);
    }

    public Task<IncidentResponse?> ResolveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, incident => incident.Resolve(), cancellationToken);
    }

    public Task<IncidentResponse?> CloseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, incident => incident.Close(), cancellationToken);
    }

    private async Task<IncidentResponse?> UpdateAsync(
        Guid id,
        Action<Incident> update,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.FindIncidentForUpdateAsync(id, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        try
        {
            update(incident);
        }
        catch (InvalidOperationException exception)
        {
            throw new IncidentConflictException(exception.Message, exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(incident);
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
