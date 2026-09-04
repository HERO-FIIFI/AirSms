using System.Text.Json;

namespace AirSms.Contracts.Events;

public sealed record IntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTime OccurredAt,
    Guid AggregateId,
    string AggregateType,
    Guid? ActorUserId,
    string? CorrelationId,
    JsonElement Payload,
    IReadOnlyList<IntegrationEventRecipient>? Recipients = null);
