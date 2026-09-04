using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;

namespace AirSms.Application.Incidents.Commands.AssignIncident;

public sealed class AssignIncidentCommandHandler(IAirSmsDbContext dbContext)
{
    public async Task<IncidentResponse?> Handle(
        AssignIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        return await IncidentWorkflow.UpdateAsync(
            dbContext,
            command.IncidentId,
            incident => incident.AssignTo(command.AssignedToUserId, command.ActorUserId),
            cancellationToken);
    }
}
