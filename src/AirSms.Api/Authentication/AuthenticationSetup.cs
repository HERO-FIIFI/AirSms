using System.Security.Claims;
using System.Text;
using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AirSms.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddAirSmsAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        var issuer = section[nameof(JwtOptions.Issuer)];
        var audience = section[nameof(JwtOptions.Audience)];
        var signingKey = section[nameof(JwtOptions.SigningKey)];
        var expiration = section.GetValue<int>(nameof(JwtOptions.ExpirationMinutes));

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(signingKey) ||
            Encoding.UTF8.GetByteCount(signingKey) < 32 ||
            expiration <= 0)
        {
            throw new InvalidOperationException(
                "JWT configuration is missing or invalid. SigningKey must be at least 32 bytes.");
        }

        services.Configure<JwtOptions>(section);
        services.AddSingleton<IAccessTokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Authentication required",
                            "A valid access token is required.");
                    },
                    OnForbidden = context => WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Forbidden",
                        "You do not have permission to perform this action.")
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.IncidentAccess,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(ClaimTypes.NameIdentifier)
                    .RequireRole(
                        nameof(UserRole.OperationsAgent),
                        nameof(UserRole.Supervisor),
                        nameof(UserRole.Administrator)));

            options.AddPolicy(
                AuthorizationPolicies.IncidentWorkflow,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(ClaimTypes.NameIdentifier)
                    .RequireRole(
                        nameof(UserRole.Supervisor),
                        nameof(UserRole.Administrator)));

            options.AddPolicy(
                AuthorizationPolicies.AdministratorOnly,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(ClaimTypes.NameIdentifier)
                    .RequireRole(nameof(UserRole.Administrator)));
        });

        return services;
    }

    private static async Task WriteProblemAsync(
        HttpContext httpContext,
        int status,
        string title,
        string detail)
    {
        httpContext.Response.StatusCode = status;
        var problemDetailsService = httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
    }
}
