namespace AirSms.Notifications.Email;

public sealed record EmailDeliveryRequest(
    string To,
    string Subject,
    string Body);
