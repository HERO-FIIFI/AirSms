using System.Security.Claims;
using AirSms.Api.Authentication;
using AirSms.Application.Authentication;
using AirSms.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirSms.Api.Controllers;

// Supervisors need to read the roster to pick an assignee; only administrators
// may change roles, activation, or passwords.
[ApiController]
[Authorize(Policy = AuthorizationPolicies.IncidentWorkflow)]
[Route("api/users")]
public sealed class UsersController(UserAdminService userAdmin) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<UserResponse>> List(CancellationToken cancellationToken)
    {
        return Ok(userAdmin.List(cancellationToken));
    }

    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]
    public async Task<ActionResult<UserResponse>> ChangeRole(
        Guid id,
        ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userAdmin.ChangeRoleAsync(id, GetActorUserId(), request, cancellationToken);
        return user is null ? UserNotFound(id) : Ok(user);
    }

    [HttpPut("{id:guid}/active")]
    [Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]
    public async Task<ActionResult<UserResponse>> SetActive(
        Guid id,
        SetUserActiveRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userAdmin.SetActiveAsync(id, GetActorUserId(), request, cancellationToken);
        return user is null ? UserNotFound(id) : Ok(user);
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]
    public async Task<ActionResult<UserResponse>> ResetPassword(
        Guid id,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userAdmin.ResetPasswordAsync(id, request, cancellationToken);
        return user is null ? UserNotFound(id) : Ok(user);
    }

    private Guid GetActorUserId()
    {
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private ObjectResult UserNotFound(Guid id)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "User not found",
            detail: $"User '{id}' was not found.");
    }
}
