using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;

namespace AirSms.Application.Incidents.Queries.ListIncidents;

public sealed class ListIncidentsQueryHandler(IAirSmsDbContext dbContext)
{
    public IReadOnlyList<IncidentResponse> Handle(
        ListIncidentsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        query ??= new ListIncidentsQuery();
        if (query.Page < 1)
        {
            throw new ArgumentException("Page must be greater than zero.", nameof(query));
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentException("PageSize must be between 1 and 100.", nameof(query));
        }

        var incidents = dbContext.Incidents;

        if (query.Status is not null)
        {
            incidents = incidents.Where(incident => incident.Status == query.Status);
        }

        if (query.Severity is not null)
        {
            incidents = incidents.Where(incident => incident.Severity == query.Severity);
        }

        if (query.Category is not null)
        {
            incidents = incidents.Where(incident => incident.Category == query.Category);
        }

        if (query.AssignedToUserId is not null)
        {
            incidents = incidents.Where(incident => incident.AssignedToUserId == query.AssignedToUserId);
        }

        return incidents
            .OrderByDescending(incident => incident.ReportedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsEnumerable()
            .Select(IncidentMapper.ToResponse)
            .ToList();
    }
}
