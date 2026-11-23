using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OneLoaf.Domain;
using OneLoaf.Notification.Application;
using OneLoaf.Notification.Application.Push;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.Notification.Infrastructure;

/// <summary>
/// Web Push with VAPID (Lib.Net.Http.WebPush). Sends to every device of the baker; a subscription the push service
/// reports as gone (404/410) is deleted. Throws when no device could be reached because of a push service failure.
/// </summary>
public sealed partial class WebPushChannel(
    PushServiceClient client,
    IPushSubscriptionRepository subscriptions,
    NotificationOptions options,
    ILogger<WebPushChannel> logger) : INotificationChannel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan TimeToLive = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public ReminderChannel Channel => ReminderChannel.Push;

    /// <inheritdoc />
    public async Task<DeliveryOutcome> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        if (!options.PushConfigured)
        {
            LogNotConfigured(logger);
            return DeliveryOutcome.NoTarget;
        }

        var targets = await subscriptions.ListTargetsAsync(recipient.UserId, cancellationToken);
        if (targets.Count == 0)
        {
            return DeliveryOutcome.NoTarget;
        }

        using var authentication = new VapidAuthentication(options.VapidPublicKey, options.VapidPrivateKey) { Subject = options.VapidSubject };
        var payload = JsonSerializer.Serialize(new { title = message.Title, body = message.Body, url = message.Url, tag = message.Tag }, Json);
        var delivered = 0;
        Exception? lastError = null;
        foreach (var target in targets)
        {
            var subscription = new PushSubscription { Endpoint = target.Endpoint };
            subscription.SetKey(PushEncryptionKeyName.P256DH, target.P256dh);
            subscription.SetKey(PushEncryptionKeyName.Auth, target.Auth);
            var push = new PushMessage(payload) { Topic = TopicOf(message.Tag), TimeToLive = (int)TimeToLive.TotalSeconds, Urgency = PushMessageUrgency.High };
            try
            {
                await client.RequestPushMessageDeliveryAsync(subscription, push, authentication, cancellationToken);
                delivered++;
            }
            catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                await subscriptions.DeleteExpiredAsync(target.Id, cancellationToken);
                LogExpired(logger, target.Id);
            }
            catch (Exception ex) when (ex is PushServiceClientException or HttpRequestException)
            {
                lastError = ex;
                LogPushFailed(logger, ex, target.Id);
            }
        }

        return delivered > 0 ? DeliveryOutcome.Delivered
            : lastError is not null ? throw new ExternalServiceUnavailableException("The push service is unavailable.", lastError)
            : DeliveryOutcome.NoTarget;
    }

    // A topic replaces an undelivered message with the same topic; at most 32 base64url characters.
    private static string TopicOf(string tag) => tag.Length <= 32 ? tag.Replace('_', '-') : tag[..32];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Web Push is not configured (VAPID keys missing); push skipped")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Push subscription {SubscriptionId} expired and was removed")]
    private static partial void LogExpired(ILogger logger, Guid subscriptionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Push to subscription {SubscriptionId} failed")]
    private static partial void LogPushFailed(ILogger logger, Exception exception, Guid subscriptionId);
}

/// <summary>SMTP settings (<c>Smtp__Host</c>, <c>Smtp__Port</c>, <c>Smtp__User</c>, <c>Smtp__Password</c>, <c>Smtp__From</c>).</summary>
public sealed class SmtpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Smtp";

    /// <summary>SMTP host; e-mail is disabled when empty.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Port (587 with STARTTLS).</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>User name.</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>Password; kept only in the environment.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Sender, e.g. <c>OneLoaf &lt;no-reply@example.com&gt;</c>.</summary>
    public string From { get; set; } = "OneLoaf <no-reply@example.com>";
}

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
