using System.Net;
using System.Net.Http.Json;
using AirSms.Api.Controllers;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Tests;

public class AuditAndNotificationApiTests
{
    [Fact]
    public async Task NonAdminCannotAccessAuditEvents()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateAuthenticatedClient(UserRole.Supervisor);

        var response = await client.GetAsync("/api/audit-events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanAccessAuditEvents()
    {
        var context = new FakeAirSmsDbContext();
        context.AddAuditEvent(new AuditEvent(
            Guid.NewGuid(),
            "incident.created",
            Guid.NewGuid(),
            "Incident",
            Guid.NewGuid(),
            DateTime.UtcNow,
            "{}"));
        using var factory = new AirSmsApiFactory(context);
        using var client = factory.CreateAuthenticatedClient(UserRole.Administrator);

        var response = await client.GetAsync("/api/audit-events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auditEvents = await response.Content.ReadFromJsonAsync<IReadOnlyList<AuditEventResponse>>(
            ApiTestHelpers.JsonOptions);
        Assert.NotNull(auditEvents);
        Assert.Single(auditEvents);
    }
}
