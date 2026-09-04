using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;
using AirSms.Domain.Entities;

namespace AirSms.Application.Incidents.Commands.CreateIncident;

public sealed class CreateIncidentCommandHandler(IAirSmsDbContext dbContext)
{
    public async Task<IncidentResponse> Handle(
        CreateIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        var incident = new Incident(
            command.Title,
            command.Description,
            command.Category,
            command.Severity,
            command.ReportedByUserId,
            command.FlightNumber,
            command.AircraftRegistration);

        dbContext.AddIncident(incident);
        await dbContext.SaveChangesAsync(cancellationToken);

        return IncidentMapper.ToResponse(incident);
    }
}
