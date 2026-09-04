using System.Security.Claims;
using AirSms.Api.Authentication;
using AirSms.Application.Incidents.Commands.AssignIncident;
using AirSms.Application.Incidents.Commands.CloseIncident;
using AirSms.Application.Incidents.Commands.CreateIncident;
using AirSms.Application.Incidents.Commands.ResolveIncident;
using AirSms.Application.Incidents.Commands.StartIncident;
using AirSms.Application.Incidents.Common;
using AirSms.Application.Incidents.Queries.GetIncidentById;
using AirSms.Application.Incidents.Queries.ListIncidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirSms.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.IncidentAccess)]
[Route("api/incidents")]
public sealed class IncidentsController(
    CreateIncidentCommandHandler createIncident,
    GetIncidentByIdQueryHandler getIncidentById,
    ListIncidentsQueryHandler listIncidents,
    AssignIncidentCommandHandler assignIncident,
    StartIncidentCommandHandler startIncident,
    ResolveIncidentCommandHandler resolveIncident,
    CloseIncidentCommandHandler closeIncident) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IncidentResponse>> Create(
        CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId();
        var incident = await createIncident.Handle(new CreateIncidentCommand(
            request.Title,
            request.Description,
            request.Category,
            request.Severity,
            actorUserId,
            request.FlightNumber,
            request.AircraftRegistration), cancellationToken);

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
        var incident = getIncidentById.Handle(new GetIncidentByIdQuery(id), cancellationToken);
        return incident is null ? NotFound() : Ok(incident);
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<IncidentResponse>> List(
        [FromQuery] ListIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        return Ok(listIncidents.Handle(request, cancellationToken));
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Assign(
        Guid id,
        AssignIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await assignIncident.Handle(
            new AssignIncidentCommand(id, request.AssignedToUserId, GetActorUserId()),
            cancellationToken);

        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Start(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident = await startIncident.Handle(
            new StartIncidentCommand(id, GetActorUserId()),
            cancellationToken);
        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Resolve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident = await resolveIncident.Handle(
            new ResolveIncidentCommand(id, GetActorUserId()),
            cancellationToken);
        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
    public async Task<ActionResult<IncidentResponse>> Close(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident = await closeIncident.Handle(
            new CloseIncidentCommand(id, GetActorUserId()),
            cancellationToken);
        return incident is null ? IncidentNotFound(id) : Ok(incident);
    }

    private Guid GetActorUserId()
    {
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private ObjectResult IncidentNotFound(Guid id)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Incident not found",
            detail: $"Incident '{id}' was not found.");
    }
}
