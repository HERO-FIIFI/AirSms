using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Tests;

public class IncidentApplicationTests
{
    [Fact]
    public async Task CreatePersistsAndReturnsIncident()
    {
        var context = new FakeAirSmsDbContext();
        var service = new IncidentService(context);
        var reporterId = Guid.NewGuid();

        var response = await service.CreateAsync(CreateRequest(reporterId));

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
        var service = new IncidentService(new FakeAirSmsDbContext(incident));

        var response = service.GetById(incident.Id);

        Assert.NotNull(response);
        Assert.Equal(incident.Id, response.Id);
    }

    [Fact]
    public void GetReturnsNullForMissingIncident()
    {
        var service = new IncidentService(new FakeAirSmsDbContext());

        Assert.Null(service.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void ListOrdersNewestIncidentFirst()
    {
        var older = CreateIncident();
        Thread.Sleep(1);
        var newer = CreateIncident();
        var service = new IncidentService(new FakeAirSmsDbContext(older, newer));

        var responses = service.List();

        Assert.Equal(new[] { newer.Id, older.Id }, responses.Select(response => response.Id));
    }

    [Fact]
    public async Task AssignOpenIncidentPersistsAssigneeAndAssignedStatus()
    {
        var incident = CreateIncident();
        var context = new FakeAirSmsDbContext(incident);
        var assigneeId = Guid.NewGuid();

        var response = await new IncidentService(context).AssignAsync(incident.Id, assigneeId);

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

        var response = await new IncidentService(new FakeAirSmsDbContext(incident))
            .StartAsync(incident.Id);

        Assert.NotNull(response);
        Assert.Equal(IncidentStatus.InProgress, response.Status);
    }

    [Fact]
    public async Task StartOpenIncidentThrowsInvalidOperation()
    {
        var incident = CreateIncident();
        var service = new IncidentService(new FakeAirSmsDbContext(incident));

        await Assert.ThrowsAsync<IncidentConflictException>(() => service.StartAsync(incident.Id));
    }

    [Fact]
    public async Task ResolveInProgressIncidentSetsResolvedStatusAndTimestamp()
    {
        var incident = CreateIncident();
        incident.AssignTo(Guid.NewGuid());
        incident.StartProgress();

        var response = await new IncidentService(new FakeAirSmsDbContext(incident))
            .ResolveAsync(incident.Id);

        Assert.NotNull(response);
        Assert.Equal(IncidentStatus.Resolved, response.Status);
        Assert.NotNull(response.ResolvedAt);
    }

    [Fact]
    public async Task ResolveOpenIncidentThrowsInvalidOperation()
    {
        var incident = CreateIncident();
        var service = new IncidentService(new FakeAirSmsDbContext(incident));

        await Assert.ThrowsAsync<IncidentConflictException>(() => service.ResolveAsync(incident.Id));
    }

    [Fact]
    public async Task CloseResolvedIncidentChangesStatusToClosed()
    {
        var incident = CreateIncident();
        incident.AssignTo(Guid.NewGuid());
        incident.StartProgress();
        incident.Resolve();

        var response = await new IncidentService(new FakeAirSmsDbContext(incident))
            .CloseAsync(incident.Id);

        Assert.NotNull(response);
        Assert.Equal(IncidentStatus.Closed, response.Status);
    }

    [Fact]
    public async Task CloseBeforeResolvedThrowsInvalidOperation()
    {
        var incident = CreateIncident();
        var service = new IncidentService(new FakeAirSmsDbContext(incident));

        await Assert.ThrowsAsync<IncidentConflictException>(() => service.CloseAsync(incident.Id));
    }

    internal static CreateIncidentRequest CreateRequest(Guid? reporterId = null)
    {
        return new CreateIncidentRequest(
            "Hydraulic warning",
            "Warning shown during inspection.",
            IncidentCategory.Technical,
            IncidentSeverity.High,
            "AS123",
            "N123AS",
            reporterId ?? Guid.NewGuid());
    }

    private static Incident CreateIncident()
    {
        var request = CreateRequest();
        return new Incident(
            request.Title,
            request.Description,
            request.Category,
            request.Severity,
            request.ReportedByUserId,
            request.FlightNumber,
            request.AircraftRegistration);
    }
}

internal sealed class FakeAirSmsDbContext(params Incident[] incidents) : IAirSmsDbContext
{
    public List<Incident> StoredIncidents { get; } = [.. incidents];
    public IQueryable<Incident> Incidents => StoredIncidents.AsQueryable();
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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}
