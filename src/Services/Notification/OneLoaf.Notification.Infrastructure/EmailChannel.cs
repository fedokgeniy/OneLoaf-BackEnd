using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using OneLoaf.Domain;
using OneLoaf.Notification.Application;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.Notification.Infrastructure;

/// <summary>E-mail over SMTP (MailKit). Not configured → no target.</summary>
public sealed class EmailChannel(IOptions<SmtpOptions> options) : INotificationChannel
{
    /// <inheritdoc />
    public ReminderChannel Channel => ReminderChannel.Email;

    /// <inheritdoc />
    public async Task<DeliveryOutcome> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(recipient.Email))
        {
            return DeliveryOutcome.NoTarget;
        }

        using var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(settings.From));
        mail.To.Add(MailboxAddress.Parse(recipient.Email));
        mail.Subject = message.Title;
        mail.Body = new TextPart("plain") { Text = message.Body };

        using var smtp = new SmtpClient();
        try
        {
            await smtp.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            if (!string.IsNullOrEmpty(settings.User))
            {
                await smtp.AuthenticateAsync(settings.User, settings.Password, cancellationToken);
            }

            await smtp.SendAsync(mail, cancellationToken);
            await smtp.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception ex) when (ex is MailKit.ProtocolException or MailKit.CommandException or System.Net.Sockets.SocketException or IOException)
        {
            throw new ExternalServiceUnavailableException("The mail server is unavailable.", ex);
        }

        return DeliveryOutcome.Delivered;
    }
}
