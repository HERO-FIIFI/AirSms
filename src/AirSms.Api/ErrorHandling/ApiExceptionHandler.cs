using AirSms.Application.Authentication;
using AirSms.Application.Incidents;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AirSms.Api.ErrorHandling;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                exception.Message),
            IncidentConflictException => (
                StatusCodes.Status409Conflict,
                "Invalid incident transition",
                exception.Message),
            DuplicateEmailException => (
                StatusCodes.Status409Conflict,
                "Email already registered",
                exception.Message),
            AuthenticationFailedException => (
                StatusCodes.Status401Unauthorized,
                "Authentication failed",
                exception.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Server error",
                "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled API exception");
        }

        httpContext.Response.StatusCode = status;

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
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
