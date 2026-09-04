# AirSms.Projections.Worker

This is the first small microservice-infrastructure seam in AirSms.

It is a separate deployable worker process that consumes Kafka integration
events from `airsms.incident-events.v1` and updates the audit/notification
projections.

It is not yet a fully extracted Notification Service:

- it shares the AirSms database for now;
- it reuses the existing projection handlers;
- it owns no separate domain model;
- it sends no email/SMS/push messages yet.

Current runtime split:

- `AirSms.Api`: HTTP API + transactional outbox publisher
- `AirSms.Projections.Worker`: Kafka consumer + projections
