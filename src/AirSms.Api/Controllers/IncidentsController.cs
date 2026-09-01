using AirSms.Application.Incidents;
using Microsoft.AspNetCore.Mvc;

namespace AirSms.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController(IncidentService incidentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IncidentResponse>> Create(
        CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var incident = await incidentService.CreateAsync(request, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = incident.Id },
                incident);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid incident",
                Detail = exception.Message
            });
        }
    }

    [HttpGet("{id:guid}")]
    public ActionResult<IncidentResponse> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident = incidentService.GetById(id, cancellationToken);
        return incident is null ? NotFound() : Ok(incident);
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<IncidentResponse>> List(
        CancellationToken cancellationToken)
    {
        return Ok(incidentService.List(cancellationToken));
    }
}
