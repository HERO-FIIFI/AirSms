using AirSms.Api.Authentication;
using AirSms.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirSms.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]
[Route("api/audit-events")]
public sealed class AuditEventsController(IAirSmsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<AuditEventResponse>> List(
        [FromQuery] Guid? aggregateId,
        [FromQuery] string? eventType,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (page < 1)
        {
            throw new ArgumentException("Page must be greater than zero.", nameof(page));
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentException("PageSize must be between 1 and 100.", nameof(pageSize));
        }

        var auditEvents = dbContext.AuditEvents;
        if (aggregateId is not null)
        {
            auditEvents = auditEvents.Where(auditEvent => auditEvent.AggregateId == aggregateId);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            auditEvents = auditEvents.Where(auditEvent => auditEvent.EventType.Contains(eventType));
        }

        if (actorUserId is not null)
        {
            auditEvents = auditEvents.Where(auditEvent => auditEvent.ActorUserId == actorUserId);
        }

        if (from is not null)
        {
            auditEvents = auditEvents.Where(auditEvent => auditEvent.OccurredAt >= from);
        }

        if (to is not null)
        {
            auditEvents = auditEvents.Where(auditEvent => auditEvent.OccurredAt <= to);
        }

        return Ok(auditEvents
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsEnumerable()
            .Select(auditEvent => new AuditEventResponse(
                auditEvent.Id,
                auditEvent.EventId,
                auditEvent.EventType,
                auditEvent.AggregateId,
                auditEvent.AggregateType,
                auditEvent.ActorUserId,
                auditEvent.OccurredAt,
                auditEvent.Payload,
                auditEvent.RecordedAt))
            .ToList());
    }
}

public sealed record AuditEventResponse(
    Guid Id,
    Guid EventId,
    string EventType,
    Guid AggregateId,
    string AggregateType,
    Guid? ActorUserId,
    DateTime OccurredAt,
    string Payload,
    DateTime RecordedAt);
