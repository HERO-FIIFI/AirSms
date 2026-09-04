namespace AirSms.Infrastructure.Persistence;

// Durable staging for domain events; not the final human-readable audit log.
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
        Type = null!;
        Payload = null!;
    }

    public OutboxMessage(
        Guid id,
        string type,
        string payload,
        DateTime occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }

    public void MarkProcessed(DateTime processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
        NextAttemptAt = null;
    }

    public void MarkFailed(string error, DateTime? nextAttemptAt)
    {
        RetryCount++;
        Error = string.IsNullOrWhiteSpace(error)
            ? "Outbox dispatch failed."
            : error[..Math.Min(error.Length, 1000)];
        NextAttemptAt = nextAttemptAt;
    }
}
