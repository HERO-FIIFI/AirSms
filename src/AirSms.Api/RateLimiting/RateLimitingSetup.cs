using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AirSms.Api.RateLimiting;

public static class RateLimitingSetup
{
    // Named so controllers can opt the credential endpoints into the tighter bucket.
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddAirSmsRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        var permitLimit = section.GetValue("PermitLimit", 120);
        var window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", 60));
        var authPermitLimit = section.GetValue("AuthPermitLimit", 8);
        var authWindow = TimeSpan.FromSeconds(section.GetValue("AuthWindowSeconds", 60));

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window
                    }));

            // Login and register are the brute-force surface, so they get their own
            // much smaller budget, always keyed by IP rather than by identity.
            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientAddress(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermitLimit,
                        Window = authWindow
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetailsService = context.HttpContext.RequestServices
                    .GetRequiredService<IProblemDetailsService>();

                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Request rate limit exceeded. Please retry shortly."
                    }
                });
            };
        });

        return services;
    }

    // Authenticated callers get their own bucket so one noisy client cannot spend
    // a shared NAT address's budget for everyone behind it.
    private static string PartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrEmpty(userId) ? ClientAddress(context) : $"user:{userId}";
    }

    private static string ClientAddress(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
