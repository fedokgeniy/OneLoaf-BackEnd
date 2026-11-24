using Fluxor;
using Fluxor.Blazor.Web.Components;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Account;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.State.Display;
using OneLoaf.Web.Shared.State.Notifications;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Pages;

/// <summary>Settings (US-04, US-29, US-50, adaptive UI): display, push reminders, account.</summary>
public partial class SettingsPage : FluxorComponent
{
    private static readonly IReadOnlyList<SegmentOption<ThemePreference>> ThemeOptions =
        [new(ThemePreference.Light, "Light"), new(ThemePreference.Dark, "Dark"), new(ThemePreference.System, "System")];

    private static readonly IReadOnlyList<SegmentOption<int>> ScaleOptions = [.. DisplayPreferences.Scales.Select(s => new SegmentOption<int>(s, $"{s}%"))];

    private PushStatus _push;
    private bool _pushBusy;
    private bool _testBusy;
    private ApiError? _pushError;
    private string? _exporting;
    private bool _deleteOpen;
    private bool _deleting;
    private string? _password;
    private string? _deleteError;

    [Inject] private IState<DisplayState> Display { get; set; } = default!;

    [Inject] private IState<SessionState> Session { get; set; } = default!;

    [Inject] private IDispatcher Dispatcher { get; set; } = default!;

    [Inject] private ISettingsService Settings { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            _push = await Settings.GetPushStatusAsync();
            StateHasChanged();
        }
    }

    private async Task TogglePush(bool on)
    {
        _pushBusy = true;
        _pushError = null;
        if (on)
        {
            (_push, _pushError) = await Settings.EnablePushAsync();
        }
        else
        {
            _pushError = await Settings.DisablePushAsync();
            _push = await Settings.GetPushStatusAsync();
        }

        _pushBusy = false;
    }

    private async Task SendTest()
    {
        _testBusy = true;
        var result = await Settings.SendTestPushAsync();
        _testBusy = false;
        Dispatcher.Dispatch(new ShowToast(result switch
        {
            ApiResult<Shared.Contracts.Notification.TestPushResponse>.Success { Value.Delivered: true } => Toast.Success("Test reminder sent"),
            ApiResult<Shared.Contracts.Notification.TestPushResponse>.Failure { Error: var error } => Toast.Error(error),
            _ => Toast.Warning("No device received the test. Turn reminders off and on again."),
        }));
    }

    private async Task Export(string path, string fileName)
    {
        _exporting = path;
        var result = await Settings.ExportAsync(path, fileName);
        _exporting = null;
        if (result is ApiResult.Failure { Error: var error })
        {
            Dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
        }
    }

    private void OpenDelete()
    {
        _password = null;
        _deleteError = null;
        _deleteOpen = true;
    }

    private async Task DeleteAccount()
    {
        _deleteError = null;
        if (string.IsNullOrEmpty(_password))
        {
            _deleteError = "Enter your password to confirm.";
            return;
        }

        if (_deleting)
        {
            return;
        }

        _deleting = true;
        var result = await Settings.DeleteAccountAsync(_password);
        _deleting = false;
        switch (result)
        {
            case ApiResult.Failure { Error.Kind: ApiErrorKind.Forbidden }:
                _deleteError = "The password is incorrect.";
                break;
            case ApiResult.Failure { Error: var error }:
                _deleteError = error.Message;
                break;
            default:
                _deleteOpen = false;
                Dispatcher.Dispatch(new ShowToast(Toast.Info("Your account was deleted.")));
                Dispatcher.Dispatch(new Logout());
                break;
        }
    }
}
