using AirSms.Domain.Enums;

namespace AirSms.Application.Users;

public sealed record ChangeUserRoleRequest(UserRole Role);

public sealed record SetUserActiveRequest(bool IsActive);

public sealed record ResetUserPasswordRequest(string Password);
