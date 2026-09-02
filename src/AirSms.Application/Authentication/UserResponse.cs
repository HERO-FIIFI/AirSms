using AirSms.Domain.Enums;

namespace AirSms.Application.Authentication;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
