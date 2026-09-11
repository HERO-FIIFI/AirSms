using AirSms.Application.Users;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Tests;

public class UserAdminServiceTests
{
    [Fact]
    public void ListReturnsEveryAccountOrderedByEmail()
    {
        var context = new FakeAirSmsDbContext();
        context.AddUser(CreateUser("zoe@airsms.local", UserRole.Supervisor));
        context.AddUser(CreateUser("adam@airsms.local", UserRole.OperationsAgent));

        var users = CreateService(context).List();

        Assert.Equal(
            ["adam@airsms.local", "zoe@airsms.local"],
            users.Select(user => user.Email));
    }

    [Fact]
    public async Task ChangeRolePromotesAgentToSupervisor()
    {
        var context = new FakeAirSmsDbContext();
        var agent = CreateUser("agent@airsms.local", UserRole.OperationsAgent);
        context.AddUser(agent);

        var updated = await CreateService(context).ChangeRoleAsync(
            agent.Id,
            Guid.NewGuid(),
            new ChangeUserRoleRequest(UserRole.Supervisor));

        Assert.NotNull(updated);
        Assert.Equal(UserRole.Supervisor, updated.Role);
        Assert.Equal(1, context.SaveChangesCalls);
    }

    [Fact]
    public async Task MissingUserReturnsNull()
    {
        var context = new FakeAirSmsDbContext();

        var updated = await CreateService(context).ChangeRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeUserRoleRequest(UserRole.Supervisor));

        Assert.Null(updated);
    }

    [Fact]
    public async Task AdministratorCannotDemoteThemselves()
    {
        var context = new FakeAirSmsDbContext();
        var admin = CreateUser("admin@airsms.local", UserRole.Administrator);
        context.AddUser(admin);
        context.AddUser(CreateUser("other@airsms.local", UserRole.Administrator));

        await Assert.ThrowsAsync<UserConflictException>(() => CreateService(context)
            .ChangeRoleAsync(
                admin.Id,
                admin.Id,
                new ChangeUserRoleRequest(UserRole.Supervisor)));
    }

    [Fact]
    public async Task LastAdministratorCannotBeDemoted()
    {
        var context = new FakeAirSmsDbContext();
        var admin = CreateUser("admin@airsms.local", UserRole.Administrator);
        context.AddUser(admin);
        context.AddUser(CreateUser("agent@airsms.local", UserRole.OperationsAgent));

        await Assert.ThrowsAsync<UserConflictException>(() => CreateService(context)
            .ChangeRoleAsync(
                admin.Id,
                Guid.NewGuid(),
                new ChangeUserRoleRequest(UserRole.OperationsAgent)));
    }

    [Fact]
    public async Task LastAdministratorCannotBeDeactivated()
    {
        var context = new FakeAirSmsDbContext();
        var admin = CreateUser("admin@airsms.local", UserRole.Administrator);
        context.AddUser(admin);

        await Assert.ThrowsAsync<UserConflictException>(() => CreateService(context)
            .SetActiveAsync(admin.Id, Guid.NewGuid(), new SetUserActiveRequest(false)));
    }

    [Fact]
    public async Task DeactivateAndReactivateRoundTrips()
    {
        var context = new FakeAirSmsDbContext();
        var agent = CreateUser("agent@airsms.local", UserRole.OperationsAgent);
        context.AddUser(agent);
        var service = CreateService(context);
        var actor = Guid.NewGuid();

        var deactivated = await service.SetActiveAsync(
            agent.Id, actor, new SetUserActiveRequest(false));
        var reactivated = await service.SetActiveAsync(
            agent.Id, actor, new SetUserActiveRequest(true));

        Assert.False(deactivated!.IsActive);
        Assert.True(reactivated!.IsActive);
    }

    [Fact]
    public async Task AdministratorCannotDeactivateThemselves()
    {
        var context = new FakeAirSmsDbContext();
        var admin = CreateUser("admin@airsms.local", UserRole.Administrator);
        context.AddUser(admin);
        context.AddUser(CreateUser("other@airsms.local", UserRole.Administrator));

        await Assert.ThrowsAsync<UserConflictException>(() => CreateService(context)
            .SetActiveAsync(admin.Id, admin.Id, new SetUserActiveRequest(false)));
    }

    [Fact]
    public async Task ResetPasswordStoresNewHash()
    {
        var context = new FakeAirSmsDbContext();
        var agent = CreateUser("agent@airsms.local", UserRole.OperationsAgent);
        context.AddUser(agent);
        var hasher = new FakePasswordHashService();

        var updated = await new UserAdminService(context, hasher).ResetPasswordAsync(
            agent.Id,
            new ResetUserPasswordRequest("BrandNewPass1!"));

        Assert.NotNull(updated);
        Assert.Equal("BrandNewPass1!", hasher.LastHashedPassword);
        Assert.Equal("hashed:BrandNewPass1!", agent.PasswordHash);
    }

    [Fact]
    public async Task ResetPasswordRejectsShortPassword()
    {
        var context = new FakeAirSmsDbContext();
        var agent = CreateUser("agent@airsms.local", UserRole.OperationsAgent);
        context.AddUser(agent);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(context)
            .ResetPasswordAsync(agent.Id, new ResetUserPasswordRequest("short")));
    }

    private static UserAdminService CreateService(FakeAirSmsDbContext context)
    {
        return new UserAdminService(context, new FakePasswordHashService());
    }

    private static User CreateUser(string email, UserRole role)
    {
        return new User(email, $"hashed:{email}", "Test", "User", role);
    }
}
