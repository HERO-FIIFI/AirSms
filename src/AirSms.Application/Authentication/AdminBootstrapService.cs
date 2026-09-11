using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Application.Authentication;

public sealed class AdminBootstrapService(
    IAirSmsDbContext dbContext,
    IPasswordHashService passwordHashService)
{
    public Task<UserResponse> EnsureAdminAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        return EnsureUserAsync(request, UserRole.Administrator, cancellationToken);
    }

    public async Task<UserResponse> EnsureUserAsync(
        RegisterUserRequest request,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);
        var existing = await dbContext.FindUserByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role != role)
            {
                throw new InvalidOperationException(
                    $"The seed email '{normalizedEmail}' belongs to an account with role "
                    + $"'{existing.Role}', not '{role}'.");
            }

            return AuthService.ToResponse(existing);
        }

        AuthService.ValidatePassword(request.Password);
        var user = new User(
            normalizedEmail,
            passwordHashService.Hash(request.Password),
            request.FirstName,
            request.LastName,
            role);

        dbContext.AddUser(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AuthService.ToResponse(user);
    }
}
