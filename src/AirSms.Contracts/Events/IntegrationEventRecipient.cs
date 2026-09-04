namespace AirSms.Contracts.Events;

public sealed record IntegrationEventRecipient(
    Guid UserId,
    string Email,
    string? DisplayName = null);
