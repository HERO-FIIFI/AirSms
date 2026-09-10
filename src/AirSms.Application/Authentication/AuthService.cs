using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Application.Authentication;

public sealed class AuthService(
    IAirSmsDbContext dbContext,
    IPasswordHashService passwordHashService,
    IAccessTokenService accessTokenService)
{
    public async Task<UserResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);
        if (await dbContext.FindUserByEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            throw new DuplicateEmailException();
        }

        ValidatePassword(request.Password);

        var user = new User(
            normalizedEmail,
            passwordHashService.Hash(request.Password),
            request.FirstName,
            request.LastName,
            UserRole.OperationsAgent);

        dbContext.AddUser(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail;
        try
        {
            normalizedEmail = User.NormalizeEmail(request.Email);
        }
        catch (ArgumentException)
        {
            throw new AuthenticationFailedException();
        }

        var user = await dbContext.FindUserByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null ||
            !user.IsActive ||
            !passwordHashService.Verify(user.PasswordHash, request.Password))
        {
            throw new AuthenticationFailedException();
        }

        var token = accessTokenService.Create(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToResponse(user));
    }

    internal static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128)
        {
            throw new ArgumentException(
                "Password must be between 8 and 128 characters.",
                nameof(password));
        }
    }

    internal static UserResponse ToResponse(User user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt);
    }
}
