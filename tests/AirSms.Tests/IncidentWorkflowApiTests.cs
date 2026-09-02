using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirSms.Api.Authentication;
using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents;
using AirSms.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AirSms.Tests;

public class IncidentWorkflowApiTests
{
    [Fact]
    public async Task WorkflowEndpointReturnsNotFoundForMissingIncident()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateAuthenticatedClient(UserRole.Supervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{Guid.NewGuid()}/assign",
            new AssignIncidentRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task WorkflowEndpointReturnsConflictForInvalidTransition()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var agent = factory.CreateAuthenticatedClient(UserRole.OperationsAgent);
        using var supervisor = factory.CreateAuthenticatedClient(UserRole.Supervisor);
        var created = await ApiTestHelpers.CreateIncidentAsync(agent);

        var response = await supervisor.PostAsync(
            $"/api/incidents/{created.Id}/start",
            null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AssignEndpointReturnsBadRequestForEmptyAssignee()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var agent = factory.CreateAuthenticatedClient(UserRole.OperationsAgent);
        using var supervisor = factory.CreateAuthenticatedClient(UserRole.Supervisor);
        var created = await ApiTestHelpers.CreateIncidentAsync(agent);

        var response = await supervisor.PostAsJsonAsync(
            $"/api/incidents/{created.Id}/assign",
            new AssignIncidentRequest(Guid.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SupervisorCanCompleteFullLifecycle()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var agent = factory.CreateAuthenticatedClient(UserRole.OperationsAgent);
        using var supervisor = factory.CreateAuthenticatedClient(UserRole.Supervisor);
        var created = await ApiTestHelpers.CreateIncidentAsync(agent);

        var assigned = await ApiTestHelpers.PostAndReadAsync(
            supervisor,
            $"/api/incidents/{created.Id}/assign",
            new AssignIncidentRequest(Guid.NewGuid()));
        Assert.Equal(IncidentStatus.Assigned, assigned.Status);

        var started = await ApiTestHelpers.PostAndReadAsync(
            supervisor,
            $"/api/incidents/{created.Id}/start");
        Assert.Equal(IncidentStatus.InProgress, started.Status);

        var resolved = await ApiTestHelpers.PostAndReadAsync(
            supervisor,
            $"/api/incidents/{created.Id}/resolve");
        Assert.Equal(IncidentStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        var closed = await ApiTestHelpers.PostAndReadAsync(
            supervisor,
            $"/api/incidents/{created.Id}/close");
        Assert.Equal(IncidentStatus.Closed, closed.Status);

        var final = await agent.GetFromJsonAsync<IncidentResponse>(
            $"/api/incidents/{created.Id}",
            ApiTestHelpers.JsonOptions);
        Assert.NotNull(final);
        Assert.Equal(IncidentStatus.Closed, final.Status);
        Assert.NotNull(final.ResolvedAt);
    }
}

internal static class ApiTestHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<IncidentResponse> CreateIncidentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/incidents",
            IncidentApplicationTests.CreateRequest(),
            JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<IncidentResponse>(JsonOptions))!;
    }

    public static async Task<IncidentResponse> PostAndReadAsync(
        HttpClient client,
        string uri,
        AssignIncidentRequest? request = null)
    {
        var response = request is null
            ? await client.PostAsync(uri, null)
            : await client.PostAsJsonAsync(uri, request, JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<IncidentResponse>(JsonOptions))!;
    }
}

internal sealed class AirSmsApiFactory(FakeAirSmsDbContext context)
    : WebApplicationFactory<Program>
{
    public HttpClient CreateAuthenticatedClient(UserRole role, Guid? userId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(role, userId ?? Guid.NewGuid()));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAirSmsDbContext>();
            services.AddSingleton<IAirSmsDbContext>(context);
        });
    }

    private string CreateToken(UserRole role, Guid userId)
    {
        var options = Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, $"{role}@airsms.test"),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, $"{role}@airsms.test"),
                new Claim(ClaimTypes.Role, role.ToString())
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
