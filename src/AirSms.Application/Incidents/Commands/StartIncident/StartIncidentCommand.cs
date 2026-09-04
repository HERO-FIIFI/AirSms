namespace AirSms.Application.Incidents.Commands.StartIncident;

public sealed record StartIncidentCommand(Guid IncidentId, Guid ActorUserId);
