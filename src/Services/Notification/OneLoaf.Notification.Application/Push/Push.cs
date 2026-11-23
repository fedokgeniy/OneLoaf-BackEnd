using FluentValidation;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Notification.Application.Push;

/// <summary>Browser keys of a push subscription (from <c>PushSubscription.toJSON()</c>).</summary>
/// <param name="P256dh">Public key of the browser (base64url).</param>
/// <param name="Auth">Authentication secret (base64url).</param>
public sealed record PushKeys(string P256dh, string Auth);

/// <summary>Body of <c>POST /push/subscriptions</c>.</summary>
/// <param name="Endpoint">Push service URL of the device.</param>
/// <param name="Keys">Encryption keys.</param>
public sealed record SubscribeRequest(string Endpoint, PushKeys Keys);

/// <summary>A stored push subscription.</summary>
/// <param name="Id">Id.</param>
/// <param name="Endpoint">Push service URL.</param>
/// <param name="CreatedAt">Creation time.</param>
public sealed record PushSubscriptionDto(Guid Id, string Endpoint, DateTimeOffset CreatedAt);

/// <summary>A device to push to.</summary>
/// <param name="Id">Subscription id.</param>
/// <param name="Endpoint">Push service URL.</param>
/// <param name="P256dh">Browser public key.</param>
/// <param name="Auth">Authentication secret.</param>
public sealed record PushTarget(Guid Id, string Endpoint, string P256dh, string Auth);

/// <summary>Response of <c>GET /push/vapid-public-key</c>.</summary>
/// <param name="PublicKey">VAPID public key (base64url) for <c>applicationServerKey</c>.</param>
public sealed record VapidPublicKeyResponse(string PublicKey);

/// <summary>Response of <c>POST /push/test</c>.</summary>
/// <param name="Delivered">At least one device accepted the notification.</param>
public sealed record TestPushResponse(bool Delivered);

/// <summary>The <c>push_subscriptions</c> table.</summary>
public interface IPushSubscriptionRepository
{
    /// <summary>Creates the subscription, or moves an existing endpoint to this user and updates its keys.</summary>
    Task<(PushSubscriptionDto Subscription, bool Created)> UpsertAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken cancellationToken);

    /// <summary>Deletes an own subscription; false when not found.</summary>
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>Devices of the user.</summary>
    Task<IReadOnlyList<PushTarget>> ListTargetsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Deletes a subscription the push service reported as gone (HTTP 404/410).</summary>
    Task DeleteExpiredAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Limits of the push requests.</summary>
public static class PushLimits
{
    /// <summary>Longest endpoint URL.</summary>
    public const int MaxEndpointLength = 1024;

    /// <summary>Longest key.</summary>
    public const int MaxKeyLength = 256;
}

/// <summary>Validates <see cref="SubscribeRequest"/>.</summary>
public sealed class SubscribeRequestValidator : AbstractValidator<SubscribeRequest>
{
    private const string Base64UrlPattern = "^[A-Za-z0-9_-]+={0,2}$";

    /// <summary>Creates the rules.</summary>
    public SubscribeRequestValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(PushLimits.MaxEndpointLength)
            .Must(e => Uri.TryCreate(e, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("endpoint must be an absolute https URL.");
        RuleFor(x => x.Keys).NotNull();
        RuleFor(x => x.Keys.P256dh).NotEmpty().MaximumLength(PushLimits.MaxKeyLength).Matches(Base64UrlPattern).When(x => x.Keys is not null);
        RuleFor(x => x.Keys.Auth).NotEmpty().MaximumLength(PushLimits.MaxKeyLength).Matches(Base64UrlPattern).When(x => x.Keys is not null);
    }
}

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
