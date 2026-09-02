using AirSms.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace AirSms.Infrastructure.Authentication;

public sealed class PasswordHashService : IPasswordHashService
{
    private static readonly object UserMarker = new();
    private readonly PasswordHasher<object> _passwordHasher = new();

    public string Hash(string password)
    {
        return _passwordHasher.HashPassword(UserMarker, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        return _passwordHasher.VerifyHashedPassword(UserMarker, passwordHash, password)
            != PasswordVerificationResult.Failed;
    }
}
