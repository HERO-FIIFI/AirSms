using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents.Common;

namespace AirSms.Application.Incidents.Queries.GetIncidentById;

public sealed class GetIncidentByIdQueryHandler(IAirSmsDbContext dbContext)
{
    public IncidentResponse? Handle(
        GetIncidentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var incident = dbContext.Incidents.SingleOrDefault(incident => incident.Id == query.IncidentId);
        return incident is null ? null : IncidentMapper.ToResponse(incident);
    }
}
