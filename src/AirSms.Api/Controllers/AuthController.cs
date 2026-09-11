using AirSms.Api.RateLimiting;
using AirSms.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AirSms.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
[Route("api/auth")]
public sealed class AuthController(
    AuthService authService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Authentication:AllowRegistration"))
        {
            return NotFound();
        }

        var user = await authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.LoginAsync(request, cancellationToken));
    }
}
