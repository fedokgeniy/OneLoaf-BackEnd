using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Device;
using OneLoaf.Web.Shared.State.Connectivity;
using OneLoaf.Web.Shared.State.Display;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.UnitTests.State;

public sealed class GlobalStateTests : BunitContext
{
    private readonly FakeTimeProvider _time = new();

    public GlobalStateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddScoped<DeviceContext>();
        Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly));
        Render<Fluxor.Blazor.Web.StoreInitializer>();
    }

    [Fact]
    public void ToastHost_SuccessToast_ShowsThenClosesAfterSixSeconds()
    {
        var cut = Render<OlToastHost>();

        Dispatch(new ShowToast(Toast.Success("Batch saved", "Open batch", "batches/1")));

        cut.WaitForAssertion(() => Assert.Contains("Batch saved", cut.Find(".ol-toast").TextContent));
        Assert.Equal("batches/1", cut.Find(".ol-toast a").GetAttribute("href"));
        _time.Advance(Toast.AutoDismissAfter);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ol-toast")));
    }

    [Fact]
    public void ToastHost_ErrorToast_StaysWithReferenceUntilClosed()
    {
        var cut = Render<OlToastHost>();
        var error = ApiError.FromStatus(500, "cb9380ef06984453a7e289c1bffa2f17");

        Dispatch(new ShowToast(Toast.Error(error)));
        Dispatch(new ShowToast(Toast.Error(error)));
        _time.Advance(TimeSpan.FromMinutes(1));

        cut.WaitForAssertion(() => Assert.Equal("alert", cut.Find(".ol-toast").GetAttribute("role")));
        Assert.Contains("cb9380ef06984453a7e289c1bffa2f17", cut.Find(".ol-toast").TextContent);
        Assert.Single(State<NotificationsState>().Toasts);
        cut.Find("button[aria-label='Close message']").Click();
        Assert.Empty(State<NotificationsState>().Toasts);
    }

    [Fact]
    public void Display_ThemeScaleMotionAndDevice_AreReducedAndApplied()
    {
        Dispatch(new SetTheme(ThemePreference.Dark));
        Dispatch(new SetScale(125));
        Dispatch(new SetScale(77));
        Dispatch(new SetReduceMotion(true));
        Dispatch(new DeviceChanged(DeviceInfo.Unknown with { SizeClass = DeviceInfo.ParseSizeClass("medium") }));
        Dispatch(new ConnectivityChanged(false));

        Assert.Equal(new DisplayPreferences(ThemePreference.Dark, 125, true), State<DisplayState>().Preferences);
        Assert.Equal(SizeClass.Medium, State<DisplayState>().Device.SizeClass);
        Assert.False(State<ConnectivityState>().IsOnline);
    }

    private void Dispatch(object action) => Services.GetRequiredService<IDispatcher>().Dispatch(action);

    private T State<T>() => Services.GetRequiredService<IState<T>>().Value;
}
