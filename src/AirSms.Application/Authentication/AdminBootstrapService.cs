using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Application.Authentication;

public sealed class AdminBootstrapService(
    IAirSmsDbContext dbContext,
    IPasswordHashService passwordHashService)
{
    public async Task<UserResponse> EnsureAdminAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);
        var existing = await dbContext.FindUserByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role != UserRole.Administrator)
            {
                throw new InvalidOperationException(
                    "The bootstrap email belongs to a non-administrator account.");
            }

            return AuthService.ToResponse(existing);
        }

        AuthService.ValidatePassword(request.Password);
        var administrator = new User(
            normalizedEmail,
            passwordHashService.Hash(request.Password),
            request.FirstName,
            request.LastName,
            UserRole.Administrator);

        dbContext.AddUser(administrator);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AuthService.ToResponse(administrator);
    }
}
