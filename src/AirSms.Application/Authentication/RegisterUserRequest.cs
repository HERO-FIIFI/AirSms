namespace AirSms.Application.Authentication;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);
