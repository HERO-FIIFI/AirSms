using System.Text.Json;
using System.Text.Json.Serialization;
using AirSms.Contracts.Events;
using AirSms.Domain.Common;
using AirSms.Domain.Events;
using AirSms.Infrastructure.Persistence;

namespace AirSms.Infrastructure.Kafka;

public sealed class IntegrationEventMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly IReadOnlyDictionary<string, EventMapping> Mappings =
        new Dictionary<string, EventMapping>
        {
            [typeof(IncidentCreatedDomainEvent).FullName!] = new(
                IncidentIntegrationEventTypes.Created,
                typeof(IncidentCreatedDomainEvent)),
            [typeof(IncidentAssignedDomainEvent).FullName!] = new(
                IncidentIntegrationEventTypes.Assigned,
                typeof(IncidentAssignedDomainEvent)),
            [typeof(IncidentStartedDomainEvent).FullName!] = new(
                IncidentIntegrationEventTypes.Started,
                typeof(IncidentStartedDomainEvent)),
            [typeof(IncidentResolvedDomainEvent).FullName!] = new(
                IncidentIntegrationEventTypes.Resolved,
                typeof(IncidentResolvedDomainEvent)),
            [typeof(IncidentClosedDomainEvent).FullName!] = new(
                IncidentIntegrationEventTypes.Closed,
                typeof(IncidentClosedDomainEvent))
        };

    private static readonly IReadOnlyDictionary<string, EventMapping> MappingsByIntegrationType =
        Mappings.Values.ToDictionary(mapping => mapping.IntegrationType);

    public IntegrationEventEnvelope Map(OutboxMessage message)
    {
        var domainEvent = DeserializeDomainEvent(message.Type, message.Payload);
        var (aggregateId, aggregateType, actorUserId) = Metadata(domainEvent);

        return new IntegrationEventEnvelope(
            message.Id,
            Mappings[message.Type].IntegrationType,
            1,
            domainEvent.OccurredAt,
            aggregateId,
            aggregateType,
            actorUserId,
            null,
            JsonDocument.Parse(message.Payload).RootElement.Clone());
    }

    public IDomainEvent ToDomainEvent(IntegrationEventEnvelope integrationEvent)
    {
        if (integrationEvent.EventVersion != 1)
        {
            throw new NotSupportedException(
                $"Unsupported integration event version '{integrationEvent.EventVersion}'.");
        }

        if (!MappingsByIntegrationType.TryGetValue(integrationEvent.EventType, out var mapping))
        {
            throw new NotSupportedException(
                $"Unknown integration event type '{integrationEvent.EventType}'.");
        }

        var domainEvent = (IDomainEvent?)integrationEvent.Payload.Deserialize(
            mapping.DomainType,
            JsonOptions);

        if (domainEvent is null)
        {
            throw new InvalidOperationException("Integration event payload could not be deserialized.");
        }

        if (domainEvent.EventId != integrationEvent.EventId)
        {
            throw new InvalidOperationException("Integration event envelope and payload event IDs do not match.");
        }

        return domainEvent;
    }

    public static (Guid AggregateId, string AggregateType, Guid? ActorUserId) Metadata(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            IncidentCreatedDomainEvent e => (e.IncidentId, "Incident", e.ReportedByUserId),
            IncidentAssignedDomainEvent e => (e.IncidentId, "Incident", e.ActorUserId),
            IncidentStartedDomainEvent e => (e.IncidentId, "Incident", e.ActorUserId),
            IncidentResolvedDomainEvent e => (e.IncidentId, "Incident", e.ActorUserId),
            IncidentClosedDomainEvent e => (e.IncidentId, "Incident", e.ActorUserId),
            _ => throw new NotSupportedException(
                $"Integration event mapping does not support event '{domainEvent.GetType().FullName}'.")
        };
    }

    private static IDomainEvent DeserializeDomainEvent(string type, string payload)
    {
        if (!Mappings.TryGetValue(type, out var mapping))
        {
            throw new NotSupportedException($"Unknown outbox event type '{type}'.");
        }

        var domainEvent = (IDomainEvent?)JsonSerializer.Deserialize(
            payload,
            mapping.DomainType,
            JsonOptions);

        if (domainEvent is null)
        {
            throw new InvalidOperationException("Outbox event payload could not be deserialized.");
        }

        return domainEvent;
    }

    private sealed record EventMapping(string IntegrationType, Type DomainType);
}
