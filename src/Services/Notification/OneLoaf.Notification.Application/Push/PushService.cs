using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Notification.Application.Push;

/// <summary>Push subscriptions of the current user and the test notification.</summary>
public sealed class PushService(
    IPushSubscriptionRepository subscriptions,
    IRecipientDirectory recipients,
    IEnumerable<INotificationChannel> channels,
    NotificationOptions options,
    ICurrentUser currentUser)
{
    /// <summary>The VAPID public key; 503 when Web Push is not configured.</summary>
    public VapidPublicKeyResponse GetVapidPublicKey() =>
        options.PushConfigured
            ? new VapidPublicKeyResponse(options.VapidPublicKey)
            : throw new ExternalServiceUnavailableException("Web Push is not configured on this server.");

    /// <summary>Stores the device (upsert by endpoint).</summary>
    public Task<(PushSubscriptionDto Subscription, bool Created)> SubscribeAsync(SubscribeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return subscriptions.UpsertAsync(currentUser.Id, request.Endpoint, request.Keys.P256dh, request.Keys.Auth, cancellationToken);
    }

    /// <summary>Removes an own device.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await subscriptions.DeleteAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException("Push subscription not found.");
        }
    }

    /// <summary>Sends a test notification to the own devices; 422 when there is none.</summary>
    public async Task<TestPushResponse> SendTestAsync(CancellationToken cancellationToken)
    {
        var recipient = await recipients.GetAsync(currentUser.Id, cancellationToken)
                        ?? throw new NotFoundException("Account not found.");
        var push = channels.First(c => c.Channel == ReminderChannel.Push);
        var outcome = await push.SendAsync(recipient,
            new NotificationMessage("OneLoaf", "Notifications work on this device.", null, "test"), cancellationToken);
        return outcome == DeliveryOutcome.Delivered
            ? new TestPushResponse(true)
            : throw new BusinessRuleException("No device is subscribed to notifications. Allow notifications in the app first.");
    }
}
