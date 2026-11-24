using Microsoft.JSInterop;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Contracts.Notification;

namespace OneLoaf.Web.Account;

/// <summary>State of push reminders on this device.</summary>
public enum PushStatus
{
    /// <summary>Not known yet.</summary>
    Unknown,

    /// <summary>The browser or this build has no service worker / Web Push.</summary>
    Unsupported,

    /// <summary>The user blocked notifications for the site.</summary>
    Denied,

    /// <summary>Subscribed.</summary>
    On,

    /// <summary>Not subscribed.</summary>
    Off,
}

/// <summary>Browser subscription as push.js returns it.</summary>
public sealed record BrowserSubscription(string Endpoint, string P256dh, string Auth);

/// <summary>Settings actions of the host (US-04, US-29, US-50).</summary>
public interface ISettingsService
{
    /// <summary><c>DELETE /me</c> with the password; 403 when it is wrong.</summary>
    Task<ApiResult> DeleteAccountAsync(string password, CancellationToken ct = default);

    /// <summary>Downloads a CSV with the token and hands it to the browser as a file.</summary>
    Task<ApiResult> ExportAsync(string path, string fileName, CancellationToken ct = default);

    /// <summary>Push state of this device.</summary>
    Task<PushStatus> GetPushStatusAsync();

    /// <summary>Asks for permission (after a click), subscribes and registers the device.</summary>
    Task<(PushStatus Status, ApiError? Error)> EnablePushAsync();

    /// <summary>Removes the device and unsubscribes the browser.</summary>
    Task<ApiError?> DisablePushAsync();

    /// <summary><c>POST /push/test</c>.</summary>
    Task<ApiResult<TestPushResponse>> SendTestPushAsync();
}

/// <summary>
/// Settings actions of the host (US-04, US-29, US-50): account deletion, CSV export, push subscription. The page calls
/// this service; the HTTP calls go through the shared pipeline and the browser parts through push.js.
/// </summary>
public sealed class SettingsService(ApiClientFactory api, IJSRuntime js) : ISettingsService, IAsyncDisposable
{
    /// <summary>CSV exports of the Core API with their file names.</summary>
    public static IReadOnlyList<(string Path, string FileName, string Text)> Exports { get; } =
    [
        ("api/v1/export/recipes.csv", "oneloaf-recipes.csv", "Recipes"),
        ("api/v1/export/bake-records.csv", "oneloaf-bakes.csv", "Bake history"),
    ];

    private IJSObjectReference? _module;

    /// <summary><c>DELETE /me</c> with the password; 403 when it is wrong.</summary>
    public Task<ApiResult> DeleteAccountAsync(string password, CancellationToken ct = default) =>
        api.Core.DeleteAsync("api/v1/me", new DeleteAccountRequest(password), ct);

    /// <summary>Downloads a CSV with the token and hands it to the browser as a file.</summary>
    public async Task<ApiResult> ExportAsync(string path, string fileName, CancellationToken ct = default)
    {
        switch (await api.Core.DownloadAsync(path, ct))
        {
            case ApiResult<FileDownload>.Success { Value: var file }:
                var module = await Module();
                await module.InvokeVoidAsync("saveFile", ct, file.FileName ?? fileName, file.ContentType, Convert.ToBase64String(file.Content));
                return ApiResult.Ok;
            case ApiResult<FileDownload>.Failure { Error: var error }:
                return ApiResult.Fail(error);
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>Push state of this device.</summary>
    public async Task<PushStatus> GetPushStatusAsync()
    {
        try
        {
            var module = await Module();
            return await module.InvokeAsync<string>("status") switch
            {
                "on" => PushStatus.On,
                "off" => PushStatus.Off,
                "denied" => PushStatus.Denied,
                _ => PushStatus.Unsupported,
            };
        }
        catch (JSException)
        {
            return PushStatus.Unsupported;
        }
    }

    /// <summary>Asks the browser for permission (after the user's click), subscribes and registers the device.</summary>
    public async Task<(PushStatus Status, ApiError? Error)> EnablePushAsync()
    {
        string key;
        switch (await api.Notification.GetAsync<VapidPublicKeyResponse>("api/v1/push/vapid-public-key"))
        {
            case ApiResult<VapidPublicKeyResponse>.Success { Value: var vapid }:
                key = vapid.PublicKey;
                break;
            case ApiResult<VapidPublicKeyResponse>.Failure { Error: var keyError }:
                return (PushStatus.Off, keyError);
            default:
                throw new InvalidOperationException();
        }

        var module = await Module();
        var subscription = await module.InvokeAsync<BrowserSubscription?>("subscribe", key);
        if (subscription is null)
        {
            return (await GetPushStatusAsync(), null);
        }

        var result = await Register(subscription);
        return result is ApiResult<PushSubscriptionDto>.Failure { Error: var error } ? (PushStatus.Off, error) : (PushStatus.On, null);
    }

    /// <summary>Removes the device from the Notification service and unsubscribes the browser.</summary>
    public async Task<ApiError?> DisablePushAsync()
    {
        var module = await Module();
        if (await module.InvokeAsync<BrowserSubscription?>("current") is { } subscription
            && await Register(subscription) is ApiResult<PushSubscriptionDto>.Success { Value: var registered }
            && await api.Notification.DeleteAsync($"api/v1/push/subscriptions/{registered.Id}") is ApiResult.Failure { Error: var error })
        {
            return error;
        }

        await module.InvokeVoidAsync("unsubscribe");
        return null;
    }

    /// <summary><c>POST /push/test</c>.</summary>
    public Task<ApiResult<TestPushResponse>> SendTestPushAsync() =>
        api.Notification.PostEmptyAsync<TestPushResponse>("api/v1/push/test");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is gone.
            }
        }
    }

    // Subscribing a known endpoint again returns its id (200), which DELETE needs: the client stores nothing.
    private Task<ApiResult<PushSubscriptionDto>> Register(BrowserSubscription subscription) =>
        api.Notification.PostAsync<SubscribeRequest, PushSubscriptionDto>("api/v1/push/subscriptions",
            new SubscribeRequest(subscription.Endpoint, new PushKeys(subscription.P256dh, subscription.Auth)));

    private async Task<IJSObjectReference> Module() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/push.js");
}
