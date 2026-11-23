using Microsoft.AspNetCore.Http.HttpResults;
using OneLoaf.Notification.Application.Push;
using OneLoaf.Notification.Application.Reminders;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.Notification.Endpoints;

/// <summary>Names used by the notification endpoints.</summary>
public static class NotificationApi
{
    /// <summary>OpenAPI tag of the push endpoints.</summary>
    public const string PushTag = "Push";

    /// <summary>OpenAPI tag of the reminders.</summary>
    public const string RemindersTag = "Reminders";

    /// <summary>Route of the push subscriptions.</summary>
    public const string SubscriptionsRoute = Routes.ApiV1 + "/push/subscriptions";
}

/// <summary>Push subscriptions, test notification and the baker's reminders (US-29).</summary>
public static class NotificationEndpoints
{
    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var push = app.MapGroup(Routes.ApiV1 + "/push").WithTags(NotificationApi.PushTag);

        push.MapGet("/vapid-public-key", (PushService s) => TypedResults.Ok(s.GetVapidPublicKey()))
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("GetVapidPublicKey").WithSummary("VAPID public key")
            .WithDescription("applicationServerKey for PushManager.subscribe in the browser; 503 when Web Push is not configured.");

        var authorized = push.MapGroup(string.Empty).RequireAuthorization().ProducesProblem(StatusCodes.Status401Unauthorized);
        authorized.MapPost("/subscriptions", SubscribeAsync).WithValidation<SubscribeRequest>()
            .WithName("Subscribe").WithSummary("Register a device for notifications")
            .WithDescription("US-29. Body is PushSubscription.toJSON() of the browser. Upsert by endpoint: 201 for a new device, 200 when known.");
        authorized.MapDelete("/subscriptions/{id:guid}", DeleteAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("Unsubscribe").WithSummary("Remove a device").WithDescription("Only own devices; another user's id gives 404.");
        authorized.MapPost("/test", async (PushService s, CancellationToken ct) => TypedResults.Ok(await s.SendTestAsync(ct)))
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("SendTestPush").WithSummary("Send a test notification to own devices")
            .WithDescription("422 when no device is subscribed, 503 when the push service cannot be reached.");

        app.MapGet(Routes.ApiV1 + "/reminders", async ([AsParameters] ReminderListQuery query, ReminderQueryService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ListAsync(query, ct)))
            .WithValidation<ReminderListQuery>()
            .WithTags(NotificationApi.RemindersTag).RequireAuthorization().ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("ListReminders").WithSummary("Own reminders")
            .WithDescription("US-29. Filter by status and due time (from inclusive, to exclusive); ordered by due time, at most 200.");
        return app;
    }

    private static async Task<Results<Created<PushSubscriptionDto>, Ok<PushSubscriptionDto>>> SubscribeAsync(
        SubscribeRequest request, PushService push, CancellationToken ct)
    {
        var (subscription, created) = await push.SubscribeAsync(request, ct);
        return created
            ? TypedResults.Created($"{NotificationApi.SubscriptionsRoute}/{subscription.Id}", subscription)
            : TypedResults.Ok(subscription);
    }

    private static async Task<NoContent> DeleteAsync(Guid id, PushService push, CancellationToken ct)
    {
        await push.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }
}

/// <summary>A type of the notification assembly, used to locate the entry point by the test host.</summary>
public sealed class NotificationEntryPoint;
