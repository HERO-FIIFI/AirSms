namespace AirSms.Application.Authentication;

public sealed class AuthenticationFailedException()
    : InvalidOperationException("Invalid email or password.");
