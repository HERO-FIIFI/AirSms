namespace AirSms.Application.Users;

public sealed class UserConflictException(string message)
    : InvalidOperationException(message);
