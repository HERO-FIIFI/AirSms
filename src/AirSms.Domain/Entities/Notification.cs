namespace AirSms.Domain.Entities;

public class Notification
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
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Type = Required(type, nameof(type));
        Title = Required(title, nameof(title));
        Message = Required(message, nameof(message));
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
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public Guid SourceEventId { get; private set; }

    public void MarkRead()
    {
        ReadAt ??= DateTime.UtcNow;
    }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}
