using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents;
using AirSms.Application.Incidents.Commands.AssignIncident;
using AirSms.Application.Incidents.Commands.CloseIncident;
using AirSms.Application.Incidents.Commands.CreateIncident;
using AirSms.Application.Incidents.Commands.ResolveIncident;
using AirSms.Application.Incidents.Commands.StartIncident;
using AirSms.Application.Incidents.Common;
using AirSms.Application.Incidents.Queries.GetIncidentById;
using AirSms.Application.Incidents.Queries.ListIncidents;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Tests;

public class IncidentApplicationTests
{
    [Fact]
    public async Task CreatePersistsAndReturnsIncident()
    {
        var context = new FakeAirSmsDbContext();
        var reporterId = Guid.NewGuid();

        var response = await new CreateIncidentCommandHandler(context)
            .Handle(CreateCommand(reporterId));

        Assert.Single(context.StoredIncidents);
        Assert.Equal(context.StoredIncidents[0].Id, response.Id);
        Assert.Equal(reporterId, response.ReportedByUserId);
        Assert.Equal(IncidentStatus.Open, response.Status);
        Assert.Equal(1, context.SaveChangesCalls);
    }

    [Fact]
    public void GetReturnsExistingIncident()
    {
        var incident = CreateIncident();
        var handler = new GetIncidentByIdQueryHandler(new FakeAirSmsDbContext(incident));

        var response = handler.Handle(new GetIncidentByIdQuery(incident.Id));

        Assert.NotNull(response);
        Assert.Equal(incident.Id, response.Id);
    }

    [Fact]
    public void GetReturnsNullForMissingIncident()
    {
        var handler = new GetIncidentByIdQueryHandler(new FakeAirSmsDbContext());

        Assert.Null(handler.Handle(new GetIncidentByIdQuery(Guid.NewGuid())));
    }

    [Fact]
    public void ListOrdersNewestIncidentFirst()
    {
        var older = CreateIncident();
        Thread.Sleep(1);
        var newer = CreateIncident();
        var handler = new ListIncidentsQueryHandler(new FakeAirSmsDbContext(older, newer));

        var responses = handler.Handle();

        Assert.Equal(new[] { newer.Id, older.Id }, responses.Select(response => response.Id));
    }

    [Fact]
    public void ListFiltersByStatus()
    {
        var open = CreateIncident();
        var assigned = CreateIncident();
        assigned.AssignTo(Guid.NewGuid());
        var handler = new ListIncidentsQueryHandler(new FakeAirSmsDbContext(open, assigned));

        var responses = handler.Handle(new ListIncidentsQuery { Status = IncidentStatus.Assigned });

        var response = Assert.Single(responses);
        Assert.Equal(assigned.Id, response.Id);
    }

    [Fact]
    public void ListPaginatesAfterNewestOrder()
    {
        var oldest = CreateIncident();
        Thread.Sleep(1);
        var middle = CreateIncident();
        Thread.Sleep(1);
        var newest = CreateIncident();
        var handler = new ListIncidentsQueryHandler(new FakeAirSmsDbContext(oldest, middle, newest));

        var responses = handler.Handle(new ListIncidentsQuery { Page = 2, PageSize = 1 });

        var response = Assert.Single(responses);
        Assert.Equal(middle.Id, response.Id);
    }

    [Fact]
    public void ListRejectsInvalidPageSize()
    {
        var handler = new ListIncidentsQueryHandler(new FakeAirSmsDbContext());

        Assert.Throws<ArgumentException>(() =>
            handler.Handle(new ListIncidentsQuery { PageSize = 101 }));
    }

    [Fact]
    public async Task AssignOpenIncidentPersistsAssigneeAndAssignedStatus()
    {
        var incident = CreateIncident();
        var context = new FakeAirSmsDbContext(incident);
        var assigneeId = Guid.NewGuid();

        var response = await new AssignIncidentCommandHandler(context)
            .Handle(new AssignIncidentCommand(incident.Id, assigneeId, Guid.NewGuid()));

        Assert.NotNull(response);
        Assert.Equal(IncidentStatus.Assigned, response.Status);
        Assert.Equal(assigneeId, response.AssignedToUserId);
        Assert.Equal(1, context.SaveChangesCalls);
    }

    [Fact]
    public async Task StartAssignedIncidentChangesStatusToInProgress()
    {
        var incident = CreateIncident();
        incident.AssignTo(Guid.NewGuid());

        var response = await new StartIncidentCommandHandler(new FakeAirSmsDbContext(incident))
            .Handle(new StartIncidentCommand(incident.Id, Guid.NewGuid()));

        Assert.NotNull(response);
        Assert.Equal(IncidentStatus.InProgress, response.Status);
    }

    [Fact]
    public async Task StartOpenIncidentThrowsInvalidOperation()
    {
        var incident = CreateIncident();
        var handler = new StartIncidentCommandHandler(new FakeAirSmsDbContext(incident));

        await Assert.ThrowsAsync<IncidentConflictException>(() =>
            handler.Handle(new StartIncidentCommand(incident.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task ResolveInProgressIncidentSetsResolvedStatusAndTimestamp()
    {
        var incident = CreateIncident();
        incident.AssignTo(Guid.NewGuid());
        incident.StartProgress();

        var response = await new ResolveIncidentCommandHandler(new FakeAirSmsDbContext(incident))
            .Handle(new ResolveIncidentCommand(incident.Id, Guid.NewGuid()));

        Assert.NotNull(response);
        Assert.Equal(IncidentStatus.Resolved, response.Status);
        Assert.NotNull(response.ResolvedAt);
    }

    [Fact]
    public async Task ResolveOpenIncidentThrowsInvalidOperation()
    {
        var incident = CreateIncident();
        var handler = new ResolveIncidentCommandHandler(new FakeAirSmsDbContext(incident));

        await Assert.ThrowsAsync<IncidentConflictException>(() =>
            handler.Handle(new ResolveIncidentCommand(incident.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task CloseResolvedIncidentChangesStatusToClosed()
    {
        var incident = CreateIncident();
        incident.AssignTo(Guid.NewGuid());
        incident.StartProgress();
        incident.Resolve();

        var response = await new CloseIncidentCommandHandler(new FakeAirSmsDbContext(incident))
            .Handle(new CloseIncidentCommand(incident.Id, Guid.NewGuid()));

        Assert.NotNull(response);
        Assert.Equal(IncidentStatus.Closed, response.Status);
    }

    [Fact]
    public async Task CloseBeforeResolvedThrowsInvalidOperation()
    {
        var incident = CreateIncident();
        var handler = new CloseIncidentCommandHandler(new FakeAirSmsDbContext(incident));

        await Assert.ThrowsAsync<IncidentConflictException>(() =>
            handler.Handle(new CloseIncidentCommand(incident.Id, Guid.NewGuid())));
    }

    internal static CreateIncidentRequest CreateRequest()
    {
        return new CreateIncidentRequest(
            "Hydraulic warning",
            "Warning shown during inspection.",
            IncidentCategory.Technical,
            IncidentSeverity.High,
            "AS123",
            "N123AS");
    }

    internal static CreateIncidentCommand CreateCommand(Guid? reporterId = null)
    {
        var request = CreateRequest();
        return new CreateIncidentCommand(
            request.Title,
            request.Description,
            request.Category,
            request.Severity,
            reporterId ?? Guid.NewGuid(),
            request.FlightNumber,
            request.AircraftRegistration);
    }

    private static Incident CreateIncident()
    {
        var request = CreateRequest();
        return new Incident(
            request.Title,
            request.Description,
            request.Category,
            request.Severity,
            Guid.NewGuid(),
            request.FlightNumber,
            request.AircraftRegistration);
    }
}

internal sealed class FakeAirSmsDbContext(params Incident[] incidents) : IAirSmsDbContext
{
    public List<Incident> StoredIncidents { get; } = [.. incidents];
    public List<User> StoredUsers { get; } = [];
    public List<AuditEvent> StoredAuditEvents { get; } = [];
    public List<Notification> StoredNotifications { get; } = [];
    public IQueryable<Incident> Incidents => StoredIncidents.AsQueryable();
    public IQueryable<User> Users => StoredUsers.AsQueryable();
    public IQueryable<AuditEvent> AuditEvents => StoredAuditEvents.AsQueryable();
    public IQueryable<Notification> Notifications => StoredNotifications.AsQueryable();
    public int SaveChangesCalls { get; private set; }

    public Task<Incident?> FindIncidentForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(StoredIncidents.SingleOrDefault(incident => incident.Id == id));
    }

    public void AddIncident(Incident incident)
    {
        StoredIncidents.Add(incident);
    }

    public void AddAuditEvent(AuditEvent auditEvent)
    {
        StoredAuditEvents.Add(auditEvent);
    }

    public void AddNotification(Notification notification)
    {
        StoredNotifications.Add(notification);
    }

    public Task<User?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(StoredUsers.SingleOrDefault(user => user.Email == normalizedEmail));
    }

    public void AddUser(User user)
    {
        StoredUsers.Add(user);
    }

    public Task<Notification?> FindNotificationForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(StoredNotifications.SingleOrDefault(notification => notification.Id == id));
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}
