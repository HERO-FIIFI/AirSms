using AirSms.Domain.Common;
using System.Text.Json.Serialization;

namespace AirSms.Domain.Events;

public sealed record IncidentCreatedDomainEvent(
    Guid IncidentId,
    Guid ReportedByUserId) : IDomainEvent
{
    [JsonInclude]
    public Guid EventId { get; private set; } = Guid.NewGuid();

    [JsonInclude]
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
}
