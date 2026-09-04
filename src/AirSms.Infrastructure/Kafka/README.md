# Kafka event boundary

Incident domain events are internal business facts raised by the domain model.
Kafka messages are integration events: stable, versioned envelopes intended for
external consumers.

Current flow:

1. `Incident` raises a domain event.
2. EF saves the domain event to `outbox_messages` in the same transaction.
3. `OutboxProcessor` maps the outbox row to an `IntegrationEventEnvelope`.
4. The envelope is published to Kafka topic `airsms.incident-events.v1` with
   `AggregateId`/IncidentId as the message key.
5. `KafkaIntegrationEventConsumer` processes the envelope, writes audit and
   notification projections, then commits the Kafka offset.

Semantics:

- At-least-once delivery.
- Outbox rows are marked processed only after Kafka acknowledges publication.
- Kafka offsets are committed only after projections persist successfully.
- Projection idempotency remains authoritative:
  - audit: unique `EventId`
  - notifications: unique `SourceEventId` + `UserId`
- Kafka ordering is per partition. Using IncidentId as the key preserves order
  for one incident within a partition; there is no global ordering claim.
