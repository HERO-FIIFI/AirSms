using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;
using AirSms.Domain.Entities;

namespace AirSms.Application.Incidents.Commands;

internal static class IncidentWorkflow
{
    public static async Task<IncidentResponse?> UpdateAsync(
        IAirSmsDbContext dbContext,
        Guid incidentId,
        Action<Incident> update,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.FindIncidentForUpdateAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        try
        {
            update(incident);
        }
        catch (InvalidOperationException exception)
        {
            throw new IncidentConflictException(exception.Message, exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return IncidentMapper.ToResponse(incident);
    }
}
