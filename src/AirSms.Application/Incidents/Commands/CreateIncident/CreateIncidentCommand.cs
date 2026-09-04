using AirSms.Domain.Enums;

namespace AirSms.Application.Incidents.Commands.CreateIncident;

public sealed record CreateIncidentCommand(
    string Title,
    string Description,
    IncidentCategory Category,
    IncidentSeverity Severity,
    Guid ReportedByUserId,
    string? FlightNumber,
    string? AircraftRegistration);
