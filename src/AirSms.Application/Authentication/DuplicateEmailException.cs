namespace AirSms.Application.Authentication;

public sealed class DuplicateEmailException()
    : InvalidOperationException("A user with this email already exists.");
