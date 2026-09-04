using AirSms.Domain.Enums;

namespace AirSms.Application.Incidents.Commands.CreateIncident;

public sealed record CreateIncidentRequest(
    string Title,
    string Description,
    IncidentCategory Category,
    IncidentSeverity Severity,
    string? FlightNumber,
    string? AircraftRegistration);
