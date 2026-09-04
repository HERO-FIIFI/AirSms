using AirSms.Domain.Entities;
using AirSms.Domain.Enums;
using AirSms.Domain.Events;

namespace AirSms.Tests;

public class IncidentTests
{
    [Fact]
    public void CreatingValidIncidentSetsInitialState()
    {
        var reporterId = Guid.NewGuid();
        var beforeCreation = DateTime.UtcNow;

        var incident = CreateIncident(
            reporterId,
            title: "  Hydraulic warning  ",
            description: "  Warning shown during inspection.  ",
            flightNumber: "  AS123  ",
            aircraftRegistration: "  N123AS  ");

        Assert.NotEqual(Guid.Empty, incident.Id);
        Assert.Equal("Hydraulic warning", incident.Title);
        Assert.Equal("Warning shown during inspection.", incident.Description);
        Assert.Equal(IncidentCategory.Technical, incident.Category);
        Assert.Equal(IncidentSeverity.High, incident.Severity);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal("AS123", incident.FlightNumber);
        Assert.Equal("N123AS", incident.AircraftRegistration);
        Assert.Equal(reporterId, incident.ReportedByUserId);
        Assert.Null(incident.AssignedToUserId);
        Assert.Null(incident.ResolvedAt);
        Assert.Null(incident.UpdatedAt);
        Assert.Equal(incident.CreatedAt, incident.ReportedAt);
        Assert.InRange(incident.CreatedAt, beforeCreation, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, incident.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, incident.ReportedAt.Kind);
    }

    [Fact]
    public void BlankTitleIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CreateIncident(Guid.NewGuid(), title: "   "));
    }

    [Fact]
    public void BlankDescriptionIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CreateIncident(Guid.NewGuid(), description: "   "));
    }

    [Fact]
    public void EmptyReporterIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CreateIncident(Guid.Empty));
    }

    [Fact]
    public void TitleLongerThanTwoHundredCharactersIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CreateIncident(Guid.NewGuid(), title: new string('A', 201)));
    }

    [Fact]
    public void AssignmentSucceeds()
    {
        var incident = CreateIncident(Guid.NewGuid());
        var assigneeId = Guid.NewGuid();

        incident.AssignTo(assigneeId);

        Assert.Equal(assigneeId, incident.AssignedToUserId);
        Assert.Equal(IncidentStatus.Assigned, incident.Status);
        Assert.NotNull(incident.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, incident.UpdatedAt!.Value.Kind);
    }

    [Fact]
    public void EmptyAssigneeIdIsRejected()
    {
        var incident = CreateIncident(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => incident.AssignTo(Guid.Empty));
    }

    [Fact]
    public void CannotStartProgressBeforeAssignment()
    {
        var incident = CreateIncident(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => incident.StartProgress());
    }

    [Fact]
    public void FullIncidentWorkflowSucceeds()
    {
        var incident = CreateIncident(Guid.NewGuid());

        incident.AssignTo(Guid.NewGuid());
        incident.StartProgress();
        Assert.Equal(IncidentStatus.InProgress, incident.Status);

        incident.Resolve();
        Assert.Equal(IncidentStatus.Resolved, incident.Status);

        incident.Close();
        Assert.Equal(IncidentStatus.Closed, incident.Status);
    }

    [Fact]
    public void CannotResolveOpenIncident()
    {
        var incident = CreateIncident(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => incident.Resolve());
    }

    [Fact]
    public void ResolvePopulatesUtcResolvedTimestamp()
    {
        var incident = CreateIncident(Guid.NewGuid());
        incident.AssignTo(Guid.NewGuid());
        incident.StartProgress();
        var beforeResolve = DateTime.UtcNow;

        incident.Resolve();

        Assert.NotNull(incident.ResolvedAt);
        Assert.InRange(incident.ResolvedAt!.Value, beforeResolve, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, incident.ResolvedAt.Value.Kind);
        Assert.NotNull(incident.UpdatedAt);
    }

    [Fact]
    public void CreationRaisesIncidentCreatedDomainEvent()
    {
        var reporterId = Guid.NewGuid();

        var incident = CreateIncident(reporterId);

        var domainEvent = Assert.IsType<IncidentCreatedDomainEvent>(
            Assert.Single(incident.DomainEvents));
        Assert.Equal(incident.Id, domainEvent.IncidentId);
        Assert.Equal(reporterId, domainEvent.ReportedByUserId);
    }

    [Fact]
    public void AssignmentRaisesIncidentAssignedDomainEvent()
    {
        var incident = CreateIncident(Guid.NewGuid());
        incident.ClearDomainEvents();
        var assigneeId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        incident.AssignTo(assigneeId, actorId);

        var domainEvent = Assert.IsType<IncidentAssignedDomainEvent>(
            Assert.Single(incident.DomainEvents));
        Assert.Equal(incident.Id, domainEvent.IncidentId);
        Assert.Equal(assigneeId, domainEvent.AssignedToUserId);
        Assert.Equal(actorId, domainEvent.ActorUserId);
    }

    [Fact]
    public void InvalidTransitionRaisesNoDomainEvent()
    {
        var incident = CreateIncident(Guid.NewGuid());
        incident.ClearDomainEvents();

        Assert.Throws<InvalidOperationException>(() => incident.Resolve());

        Assert.Empty(incident.DomainEvents);
    }

    [Fact]
    public void ResolutionRaisesEventWithResolvedTimestamp()
    {
        var incident = CreateIncident(Guid.NewGuid());
        incident.AssignTo(Guid.NewGuid());
        incident.StartProgress();
        incident.ClearDomainEvents();

        incident.Resolve(Guid.NewGuid());

        var domainEvent = Assert.IsType<IncidentResolvedDomainEvent>(
            Assert.Single(incident.DomainEvents));
        Assert.Equal(incident.Id, domainEvent.IncidentId);
        Assert.Equal(incident.ResolvedAt, domainEvent.ResolvedAt);
    }

    [Fact]
    public void ClearingEventsRemovesPendingDomainEvents()
    {
        var incident = CreateIncident(Guid.NewGuid());

        incident.ClearDomainEvents();

        Assert.Empty(incident.DomainEvents);
    }

    private static Incident CreateIncident(
        Guid reporterId,
        string title = "Hydraulic warning",
        string description = "Warning shown during inspection.",
        string? flightNumber = null,
        string? aircraftRegistration = null)
    {
        return new Incident(
            title,
            description,
            IncidentCategory.Technical,
            IncidentSeverity.High,
            reporterId,
            flightNumber,
            aircraftRegistration);
    }
}
