using AirSms.Domain.Common;
using AirSms.Domain.Enums;

namespace AirSms.Domain.Entities;

public class Incident : BaseEntity
{
    public Incident(
        string title,
        string description,
        IncidentCategory category,
        IncidentSeverity severity,
        Guid reportedByUserId,
        string? flightNumber = null,
        string? aircraftRegistration = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty or whitespace.", nameof(title));
        }

        title = title.Trim();
        if (title.Length > 200)
        {
            throw new ArgumentException("Title cannot exceed 200 characters.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty or whitespace.", nameof(description));
        }

        if (reportedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Reporter ID cannot be empty.", nameof(reportedByUserId));
        }

        Title = title;
        Description = description.Trim();
        Category = category;
        Severity = severity;
        Status = IncidentStatus.Open;
        FlightNumber = flightNumber?.Trim();
        AircraftRegistration = aircraftRegistration?.Trim();
        ReportedByUserId = reportedByUserId;
        ReportedAt = CreatedAt;
    }

    public string Title { get; private set; }
    public string Description { get; private set; }
    public IncidentCategory Category { get; private set; }
    public IncidentSeverity Severity { get; private set; }
    public IncidentStatus Status { get; private set; }
    public string? FlightNumber { get; private set; }
    public string? AircraftRegistration { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public DateTime ReportedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public void AssignTo(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Assignee ID cannot be empty.", nameof(userId));
        }

        AssignedToUserId = userId;
        Status = IncidentStatus.Assigned;
        MarkUpdated();
    }

    public void StartProgress()
    {
        if (Status != IncidentStatus.Assigned)
        {
            throw new InvalidOperationException("Only an assigned incident can be started.");
        }

        Status = IncidentStatus.InProgress;
        MarkUpdated();
    }

    public void Resolve()
    {
        if (Status != IncidentStatus.InProgress)
        {
            throw new InvalidOperationException("Only an incident in progress can be resolved.");
        }

        Status = IncidentStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Close()
    {
        if (Status != IncidentStatus.Resolved)
        {
            throw new InvalidOperationException("Only a resolved incident can be closed.");
        }

        Status = IncidentStatus.Closed;
        MarkUpdated();
    }
}
