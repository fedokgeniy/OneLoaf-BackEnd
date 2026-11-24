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
