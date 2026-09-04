namespace AirSms.Application.Incidents.Commands.ResolveIncident;

public sealed record ResolveIncidentCommand(Guid IncidentId, Guid ActorUserId);
