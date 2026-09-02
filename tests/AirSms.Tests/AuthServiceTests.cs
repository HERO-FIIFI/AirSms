using AirSms.Application.Authentication;
using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task RegistrationHashesPasswordAndCreatesOperationsAgent()
    {
        var context = new FakeAirSmsDbContext();
        var hasher = new FakePasswordHashService();
        var service = new AuthService(context, hasher, new FakeAccessTokenService());

        var response = await service.RegisterAsync(new RegisterUserRequest(
            " Agent@AirSms.Local ",
            "LocalPass123!",
            "Ada",
            "Lovelace"));

        var stored = Assert.Single(context.StoredUsers);
        Assert.Equal("LocalPass123!", hasher.LastHashedPassword);
        Assert.Equal("hashed:LocalPass123!", stored.PasswordHash);
        Assert.Equal("agent@airsms.local", response.Email);
        Assert.Equal(UserRole.OperationsAgent, response.Role);
    }

    [Fact]
    public async Task DuplicateEmailIsRejected()
    {
        var context = new FakeAirSmsDbContext();
        context.AddUser(CreateUser());
        var service = new AuthService(
            context,
            new FakePasswordHashService(),
            new FakeAccessTokenService());

        await Assert.ThrowsAsync<DuplicateEmailException>(() => service.RegisterAsync(
            new RegisterUserRequest(
                "AGENT@AIRSMS.LOCAL",
                "LocalPass123!",
                "Another",
                "Agent")));
    }

    [Fact]
    public async Task CorrectPasswordReturnsAccessToken()
    {
        var context = new FakeAirSmsDbContext();
        var user = CreateUser();
        context.AddUser(user);
        var service = new AuthService(
            context,
            new FakePasswordHashService(),
            new FakeAccessTokenService());

        var response = await service.LoginAsync(
            new LoginRequest(user.Email, "LocalPass123!"));

        Assert.Equal("test-token", response.AccessToken);
        Assert.Equal(user.Id, response.User.Id);
    }

    [Fact]
    public async Task WrongPasswordIsRejectedWithoutDetail()
    {
        var context = new FakeAirSmsDbContext();
        context.AddUser(CreateUser());
        var service = new AuthService(
            context,
            new FakePasswordHashService(),
            new FakeAccessTokenService());

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => service.LoginAsync(
            new LoginRequest("agent@airsms.local", "WrongPassword")));
    }

    [Fact]
    public async Task InactiveUserCannotLogin()
    {
        var context = new FakeAirSmsDbContext();
        var user = CreateUser();
        user.Deactivate();
        context.AddUser(user);
        var service = new AuthService(
            context,
            new FakePasswordHashService(),
            new FakeAccessTokenService());

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => service.LoginAsync(
            new LoginRequest(user.Email, "LocalPass123!")));
    }

    private static User CreateUser()
    {
        return new User(
            "agent@airsms.local",
            "hashed:LocalPass123!",
            "Ada",
            "Lovelace",
            UserRole.OperationsAgent);
    }
}

internal sealed class FakePasswordHashService : IPasswordHashService
{
    public string? LastHashedPassword { get; private set; }

    public string Hash(string password)
    {
        LastHashedPassword = password;
        return $"hashed:{password}";
    }

    public bool Verify(string passwordHash, string password)
    {
        return passwordHash == $"hashed:{password}";
    }
}

internal sealed class FakeAccessTokenService : IAccessTokenService
{
    public AccessTokenResult Create(User user)
    {
        return new AccessTokenResult("test-token", DateTime.UtcNow.AddHours(1));
    }
}
