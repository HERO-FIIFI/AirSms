# AirSms.Notifications

AirSms uses a modular core with event-driven independently deployable services.
Notification delivery has been extracted as a Kafka-consuming service.

Responsibilities:

- consumes `airsms.incident-events.v1`
- consumer group: `airsms-notifications-v1`
- owns `airsms_notifications`
- stores in-app notifications
- stores delivery attempts in `notification_deliveries`
- sends local-development email through Mailpit SMTP
- validates AirSms JWTs issued by `AirSms.Api`

It does not own incident lifecycle rules, users, credentials, audit projections,
or outbox publishing.

Idempotency:

- notifications: unique `SourceEventId + UserId + Type`
- deliveries: unique `NotificationId + Channel`

Retries:

- email delivery is queued before SMTP send
- SMTP failure does not break Kafka processing
- retries use 1 minute, 5 minutes, then 15 minutes
- after the configured max attempts, delivery is marked `Failed`

Run locally:

```powershell
docker compose up -d postgres kafka mailpit
dotnet run --project src/AirSms.Notifications
```

Mailpit UI: http://localhost:8025
