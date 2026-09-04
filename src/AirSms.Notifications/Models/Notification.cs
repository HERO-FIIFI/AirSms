namespace AirSms.Notifications.Models;

public sealed class Notification
{
    private Notification()
    {
        Type = null!;
        Title = null!;
        Message = null!;
    }

    public Notification(
        Guid userId,
        string type,
        string title,
        string message,
        Guid? relatedIncidentId,
        Guid sourceEventId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Type is required.", nameof(type));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required.", nameof(message));

        Id = Guid.NewGuid();
        UserId = userId;
        Type = type.Trim();
        Title = title.Trim();
        Message = message.Trim();
        RelatedIncidentId = relatedIncidentId;
        SourceEventId = sourceEventId;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }
    public Guid? RelatedIncidentId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public List<NotificationDelivery> Deliveries { get; private set; } = [];

    public void MarkRead()
    {
        ReadAt ??= DateTime.UtcNow;
    }
}
