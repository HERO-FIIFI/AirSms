using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;

namespace AirSms.Application.Incidents.Commands.CloseIncident;

public sealed class CloseIncidentCommandHandler(IAirSmsDbContext dbContext)
{
    public async Task<IncidentResponse?> Handle(
        CloseIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        return await IncidentWorkflow.UpdateAsync(
            dbContext,
            command.IncidentId,
            incident => incident.Close(command.ActorUserId),
            cancellationToken);
    }
}
