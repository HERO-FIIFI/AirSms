namespace AirSms.Application.Incidents;

public sealed class IncidentConflictException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);
