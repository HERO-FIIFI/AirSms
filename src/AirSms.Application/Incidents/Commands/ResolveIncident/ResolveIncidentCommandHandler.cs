using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;

namespace AirSms.Application.Incidents.Commands.ResolveIncident;

public sealed class ResolveIncidentCommandHandler(IAirSmsDbContext dbContext)
{
    public async Task<IncidentResponse?> Handle(
        ResolveIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        return await IncidentWorkflow.UpdateAsync(
            dbContext,
            command.IncidentId,
            incident => incident.Resolve(command.ActorUserId),
            cancellationToken);
    }
}
