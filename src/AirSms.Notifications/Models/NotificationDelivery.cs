namespace AirSms.Notifications.Models;

public sealed class NotificationDelivery
{
    private NotificationDelivery()
    {
        Destination = null!;
    }

    public NotificationDelivery(
        Guid notificationId,
        NotificationChannel channel,
        string destination,
        DeliveryStatus status = DeliveryStatus.Pending)
    {
        if (notificationId == Guid.Empty) throw new ArgumentException("Notification ID cannot be empty.", nameof(notificationId));
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Destination is required.", nameof(destination));

        Id = Guid.NewGuid();
        NotificationId = notificationId;
        Channel = channel;
        Destination = destination.Trim();
        Status = status;
        CreatedAt = DateTime.UtcNow;
        if (status == DeliveryStatus.Delivered)
        {
            DeliveredAt = CreatedAt;
        }
    }

    public Guid Id { get; private set; }
    public Guid NotificationId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public string Destination { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void BeginAttempt(DateTime now)
    {
        Status = DeliveryStatus.Processing;
        AttemptCount++;
        LastAttemptAt = now;
        LastError = null;
    }

    public void MarkDelivered(DateTime now)
    {
        Status = DeliveryStatus.Delivered;
        DeliveredAt = now;
        NextAttemptAt = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTime? nextAttemptAt)
    {
        Status = nextAttemptAt is null ? DeliveryStatus.Failed : DeliveryStatus.Pending;
        NextAttemptAt = nextAttemptAt;
        LastError = string.IsNullOrWhiteSpace(error)
            ? "Email delivery failed."
            : error[..Math.Min(error.Length, 1000)];
    }
}
