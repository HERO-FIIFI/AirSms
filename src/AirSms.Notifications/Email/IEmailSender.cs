namespace AirSms.Notifications.Email;

public interface IEmailSender
{
    Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default);
}
