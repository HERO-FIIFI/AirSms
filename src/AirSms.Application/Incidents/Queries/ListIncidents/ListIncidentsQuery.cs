using AirSms.Domain.Enums;

namespace AirSms.Application.Incidents.Queries.ListIncidents;

public sealed class ListIncidentsQuery
{
    public IncidentStatus? Status { get; init; }
    public IncidentSeverity? Severity { get; init; }
    public IncidentCategory? Category { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
