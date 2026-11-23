using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
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
