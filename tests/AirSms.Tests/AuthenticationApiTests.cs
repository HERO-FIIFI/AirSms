using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AirSms.Application.Authentication;
using AirSms.Application.Incidents.Commands.AssignIncident;
using AirSms.Application.Incidents.Common;
using AirSms.Domain.Enums;

namespace AirSms.Tests;

public class AuthenticationApiTests
{
    [Fact]
    public async Task RegistrationAndLoginReturnSafeUserAndExpectedTokenClaims()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateClient();
        var request = RegistrationRequest();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registerJson = await registerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", registerJson, StringComparison.OrdinalIgnoreCase);

        var user = JsonSerializer.Deserialize<UserResponse>(
            registerJson,
            ApiTestHelpers.JsonOptions)!;
        Assert.Equal(UserRole.OperationsAgent, user.Role);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(request.Email, request.Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(
            ApiTestHelpers.JsonOptions);
        Assert.NotNull(login);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        Assert.Equal(user.Id.ToString(), token.Claims.Single(
            claim => claim.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(user.Email, token.Claims.Single(
            claim => claim.Type == ClaimTypes.Email).Value);
        Assert.Equal(UserRole.OperationsAgent.ToString(), token.Claims.Single(
            claim => claim.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task DuplicateEmailReturnsConflict()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateClient();
        var request = RegistrationRequest();
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RegistrationCanBeDisabled()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext(), allowRegistration: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegistrationRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WrongPasswordReturnsUnauthorized()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateClient();
        var request = RegistrationRequest();
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(request.Email, "IncorrectPassword"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnauthenticatedIncidentRequestReturnsUnauthorized()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/incidents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OperationsAgentCanCreateReadAndListUsingTokenIdentity()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        var tokenUserId = Guid.NewGuid();
        using var client = factory.CreateAuthenticatedClient(
            UserRole.OperationsAgent,
            tokenUserId);
        var spoofedReporterId = Guid.NewGuid();
        var request = new
        {
            title = "Authenticated incident",
            description = "Reporter must come from the JWT.",
            category = "Security",
            severity = "High",
            flightNumber = (string?)null,
            aircraftRegistration = (string?)null,
            reportedByUserId = spoofedReporterId
        };

        var createResponse = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<IncidentResponse>(
            ApiTestHelpers.JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(tokenUserId, created.ReportedByUserId);
        Assert.NotEqual(spoofedReporterId, created.ReportedByUserId);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            $"/api/incidents/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/incidents")).StatusCode);
    }

    [Fact]
    public async Task OperationsAgentCannotAssign()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateAuthenticatedClient(UserRole.OperationsAgent);
        var created = await ApiTestHelpers.CreateIncidentAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{created.Id}/assign",
            new AssignIncidentRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdministratorHasCreateAndWorkflowAccess()
    {
        using var factory = new AirSmsApiFactory(new FakeAirSmsDbContext());
        using var client = factory.CreateAuthenticatedClient(UserRole.Administrator);
        var created = await ApiTestHelpers.CreateIncidentAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/incidents/{created.Id}/assign",
            new AssignIncidentRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static RegisterUserRequest RegistrationRequest()
    {
        return new RegisterUserRequest(
            "agent@airsms.local",
            "LocalPass123!",
            "Ada",
            "Lovelace");
    }
}
