namespace OneLoaf.Notification.Application.Push;

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
