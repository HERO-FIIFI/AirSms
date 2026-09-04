using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;

namespace AirSms.Application.Incidents.Commands.StartIncident;

public sealed class StartIncidentCommandHandler(IAirSmsDbContext dbContext)
{
    public async Task<IncidentResponse?> Handle(
        StartIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        return await IncidentWorkflow.UpdateAsync(
            dbContext,
            command.IncidentId,
            incident => incident.StartProgress(command.ActorUserId),
            cancellationToken);
    }
}
