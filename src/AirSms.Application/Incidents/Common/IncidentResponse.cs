using AirSms.Domain.Enums;

namespace AirSms.Application.Incidents.Common;

public sealed record IncidentResponse(
    Guid Id,
    string Title,
    string Description,
    IncidentCategory Category,
    IncidentSeverity Severity,
    IncidentStatus Status,
    string? FlightNumber,
    string? AircraftRegistration,
    Guid ReportedByUserId,
    Guid? AssignedToUserId,
    DateTime ReportedAt,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
