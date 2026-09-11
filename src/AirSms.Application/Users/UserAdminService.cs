using AirSms.Application.Authentication;
using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;
using AirSms.Domain.Enums;

namespace AirSms.Application.Users;

public sealed class UserAdminService(
    IAirSmsDbContext dbContext,
    IPasswordHashService passwordHashService)
{
    // ponytail: unpaged; the roster is staff-sized. Add paging when it outgrows one screen.
    public IReadOnlyList<UserResponse> List(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return dbContext.Users
            .OrderBy(user => user.Email)
            .AsEnumerable()
            .Select(AuthService.ToResponse)
            .ToList();
    }

    public async Task<UserResponse?> ChangeRoleAsync(
        Guid userId,
        Guid actorUserId,
        ChangeUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.FindUserForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (userId == actorUserId && request.Role != UserRole.Administrator)
        {
            throw new UserConflictException("You cannot remove your own administrator role.");
        }

        if (user.Role == UserRole.Administrator && request.Role != UserRole.Administrator)
        {
            await GuardLastAdministratorAsync(user, cancellationToken);
        }

        user.ChangeRole(request.Role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthService.ToResponse(user);
    }

    public async Task<UserResponse?> SetActiveAsync(
        Guid userId,
        Guid actorUserId,
        SetUserActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.FindUserForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!request.IsActive)
        {
            if (userId == actorUserId)
            {
                throw new UserConflictException("You cannot deactivate your own account.");
            }

            if (user.Role == UserRole.Administrator)
            {
                await GuardLastAdministratorAsync(user, cancellationToken);
            }
        }

        if (request.IsActive)
        {
            user.Reactivate();
        }
        else
        {
            user.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthService.ToResponse(user);
    }

    public async Task<UserResponse?> ResetPasswordAsync(
        Guid userId,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        AuthService.ValidatePassword(request.Password);

        var user = await dbContext.FindUserForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.ResetPassword(passwordHashService.Hash(request.Password));
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthService.ToResponse(user);
    }

    // Demoting or deactivating the final administrator locks everyone out of user
    // administration with no in-app way back, so it is refused.
    private Task GuardLastAdministratorAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var otherActiveAdministrators = dbContext.Users.Any(candidate =>
            candidate.Id != user.Id &&
            candidate.IsActive &&
            candidate.Role == UserRole.Administrator);

        return otherActiveAdministrators
            ? Task.CompletedTask
            : throw new UserConflictException(
                "The last active administrator cannot be demoted or deactivated.");
    }
}
