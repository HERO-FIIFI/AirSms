using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirSms.Application.Common.Interfaces;
using AirSms.Application.Incidents;
using AirSms.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AirSms.Tests;

public class IncidentWorkflowApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task WorkflowEndpointReturnsNotFoundForMissingIncident()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateClient();

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
        using var client = factory.CreateClient();
        var created = await CreateIncidentAsync(client);

        var response = await client.PostAsync(
            $"/api/incidents/{created.Id}/start",
            null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AssignEndpointReturnsBadRequestForEmptyAssignee()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateClient();
        var created = await CreateIncidentAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{created.Id}/assign",
            new AssignIncidentRequest(Guid.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FullApiLifecycleEndsClosedWithResolvedTimestamp()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateClient();
        var created = await CreateIncidentAsync(client);

        var assigned = await PostAndReadAsync(
            client,
            $"/api/incidents/{created.Id}/assign",
            new AssignIncidentRequest(Guid.NewGuid()));
        Assert.Equal(IncidentStatus.Assigned, assigned.Status);

        var started = await PostAndReadAsync(
            client,
            $"/api/incidents/{created.Id}/start");
        Assert.Equal(IncidentStatus.InProgress, started.Status);

        var resolved = await PostAndReadAsync(
            client,
            $"/api/incidents/{created.Id}/resolve");
        Assert.Equal(IncidentStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        var closed = await PostAndReadAsync(
            client,
            $"/api/incidents/{created.Id}/close");
        Assert.Equal(IncidentStatus.Closed, closed.Status);

        var final = await client.GetFromJsonAsync<IncidentResponse>(
            $"/api/incidents/{created.Id}",
            JsonOptions);
        Assert.NotNull(final);
        Assert.Equal(IncidentStatus.Closed, final.Status);
        Assert.NotNull(final.ResolvedAt);
    }

    private static async Task<IncidentResponse> CreateIncidentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/incidents",
            IncidentApplicationTests.CreateRequest(),
            JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<IncidentResponse>(JsonOptions))!;
    }

    private static async Task<IncidentResponse> PostAndReadAsync(
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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAirSmsDbContext>();
            services.AddSingleton<IAirSmsDbContext>(context);
        });
    }
}
