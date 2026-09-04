namespace AirSms.Application.Incidents.Commands.AssignIncident;

public sealed record AssignIncidentCommand(
    Guid IncidentId,
    Guid AssignedToUserId,
    Guid ActorUserId);
