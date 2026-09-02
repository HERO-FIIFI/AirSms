using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Tests;

public class UserTests
{
    [Fact]
    public void ValidUserIsNormalizedAndDefaultsActive()
    {
        var user = new User(
            "  AGENT@AirSms.Local  ",
            "stored-password-hash",
            "  Ada  ",
            "  Lovelace  ",
            UserRole.OperationsAgent);

        Assert.Equal("agent@airsms.local", user.Email);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.True(user.IsActive);
        Assert.Equal(UserRole.OperationsAgent, user.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Name <agent@airsms.local>")]
    public void InvalidEmailIsRejected(string email)
    {
        Assert.Throws<ArgumentException>(() => new User(
            email,
            "stored-password-hash",
            "Ada",
            "Lovelace",
            UserRole.OperationsAgent));
    }

    [Theory]
    [InlineData("", "Lovelace")]
    [InlineData("Ada", "   ")]
    public void RequiredNamesAreEnforced(string firstName, string lastName)
    {
        Assert.Throws<ArgumentException>(() => new User(
            "agent@airsms.local",
            "stored-password-hash",
            firstName,
            lastName,
            UserRole.OperationsAgent));
    }
}
