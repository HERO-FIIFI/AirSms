using System.Security.Claims;
using AirSms.Api.Controllers;
using AirSms.Application.Incidents.Commands.AssignIncident;
using AirSms.Application.Incidents.Commands.CloseIncident;
using AirSms.Application.Incidents.Commands.CreateIncident;
using AirSms.Application.Incidents.Commands.ResolveIncident;
using AirSms.Application.Incidents.Commands.StartIncident;
using AirSms.Application.Incidents.Common;
using AirSms.Application.Incidents.Queries.GetIncidentById;
using AirSms.Application.Incidents.Queries.ListIncidents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace AirSms.Tests;

public class IncidentsControllerTests
{
    [Fact]
    public async Task PostReturnsCreatedAtGetRoute()
    {
        var context = new FakeAirSmsDbContext();
        var controller = CreateController(context);
        var reporterId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, reporterId.ToString())],
                    "Test"))
            }
        };

        var result = await controller.Create(
            IncidentApplicationTests.CreateRequest(),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<IncidentResponse>(created.Value);
        Assert.Equal(reporterId, response.ReportedByUserId);
        Assert.Equal(nameof(IncidentsController.GetById), created.ActionName);
        Assert.Equal(response.Id, created.RouteValues!["id"]);
    }

    [Fact]
    public void GetMissingReturnsNotFound()
    {
        var controller = CreateController(new FakeAirSmsDbContext());

        var result = controller.GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static IncidentsController CreateController(FakeAirSmsDbContext context)
    {
        return new IncidentsController(
            new CreateIncidentCommandHandler(context),
            new GetIncidentByIdQueryHandler(context),
            new ListIncidentsQueryHandler(context),
            new AssignIncidentCommandHandler(context),
            new StartIncidentCommandHandler(context),
            new ResolveIncidentCommandHandler(context),
            new CloseIncidentCommandHandler(context));
    }
}
