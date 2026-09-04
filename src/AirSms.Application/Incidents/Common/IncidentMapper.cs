using AirSms.Domain.Entities;

namespace AirSms.Application.Incidents.Common;

public static class IncidentMapper
{
    public static IncidentResponse ToResponse(Incident incident)
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
