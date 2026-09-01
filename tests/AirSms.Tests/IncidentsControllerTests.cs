using AirSms.Api.Controllers;
using AirSms.Application.Incidents;
using Microsoft.AspNetCore.Mvc;

namespace AirSms.Tests;

public class IncidentsControllerTests
{
    [Fact]
    public async Task PostReturnsCreatedAtGetRoute()
    {
        var context = new FakeAirSmsDbContext();
        var controller = new IncidentsController(new IncidentService(context));

        var result = await controller.Create(
            IncidentApplicationTests.CreateRequest(),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<IncidentResponse>(created.Value);
        Assert.Equal(nameof(IncidentsController.GetById), created.ActionName);
        Assert.Equal(response.Id, created.RouteValues!["id"]);
    }

    [Fact]
    public void GetMissingReturnsNotFound()
    {
        var controller = new IncidentsController(
            new IncidentService(new FakeAirSmsDbContext()));

        var result = controller.GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
