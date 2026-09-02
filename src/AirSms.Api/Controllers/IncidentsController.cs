using System.Security.Claims;
using AirSms.Api.Authentication;
using AirSms.Application.Incidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirSms.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.IncidentAccess)]
[Route("api/incidents")]
public sealed class IncidentsController(IncidentService incidentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IncidentResponse>> Create(
        CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var reporterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var incident = await incidentService.CreateAsync(
            request,
            reporterId,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = incident.Id },
            incident);
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
        [FromQuery] ListIncidentsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(incidentService.List(request, cancellationToken));
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Assign(
        Guid id,
        AssignIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await incidentService.AssignAsync(
            id,
            request.AssignedToUserId,
            cancellationToken);

        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Start(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident = await incidentService.StartAsync(id, cancellationToken);
        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Resolve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident = await incidentService.ResolveAsync(id, cancellationToken);
        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Close(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident = await incidentService.CloseAsync(id, cancellationToken);
        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    private ObjectResult IncidentNotFound(Guid id)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Incident not found",
            detail: $"Incident '{id}' was not found.");
    }
}
