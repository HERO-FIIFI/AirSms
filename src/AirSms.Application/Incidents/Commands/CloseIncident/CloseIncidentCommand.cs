namespace AirSms.Application.Incidents.Commands.CloseIncident;

public sealed record CloseIncidentCommand(Guid IncidentId, Guid ActorUserId);
