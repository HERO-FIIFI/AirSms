namespace AirSms.Application.Authentication;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserResponse User);
