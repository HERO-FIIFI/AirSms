namespace AirSms.Domain.Entities;

public class AuditEvent
{
    private AuditEvent()
    {
        EventType = null!;
        AggregateType = null!;
        Payload = null!;
    }

    public AuditEvent(
        Guid eventId,
        string eventType,
        Guid aggregateId,
        string aggregateType,
        Guid? actorUserId,
        DateTime occurredAt,
        string payload,
        string? correlationId = null)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        EventType = eventType;
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
        Payload = payload;
        RecordedAt = DateTime.UtcNow;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; }
    public Guid AggregateId { get; private set; }
    public string AggregateType { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string Payload { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public string? CorrelationId { get; private set; }
}
