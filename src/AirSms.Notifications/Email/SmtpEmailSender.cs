using System.Net.Mail;
using AirSms.Notifications.Options;
using Microsoft.Extensions.Options;

namespace AirSms.Notifications.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(value.FromAddress, value.FromName),
            Subject = request.Subject,
            Body = request.Body
        };
        message.To.Add(request.To);

        using var client = new SmtpClient(value.Host, value.Port);
        await client.SendMailAsync(message, cancellationToken);
    }
}
