using System.Buffers.Text;
using System.Text;
using Fluxor;
using Microsoft.Extensions.Time.Testing;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web.UnitTests.Auth;

public sealed class SessionMonitorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly AuthEvents _events = new();
    private readonly TokenStore _tokens;

    public SessionMonitorTests() => _tokens = new TokenStore(new BunitJSInterop { Mode = JSRuntimeMode.Loose }.JSRuntime, _time);

    [Fact]
    public async Task Start_KeptToken_RestoresSessionThenAsksForANewTokenAndExpiresWithoutOne()
    {
        await _tokens.SetAsync(Token(), _time.GetUtcNow().AddMinutes(60));
        using var monitor = new SessionMonitor(_tokens, _events, _dispatcher, _time);

        monitor.Start();
        var restored = Assert.IsType<SessionRestored>(Assert.Single(_dispatcher.Actions));
        Assert.Equal("Docs Baker", restored.User.DisplayName);

        _time.Advance(TimeSpan.FromMinutes(59));
        Assert.IsType<RefreshSession>(_dispatcher.Actions[^1]);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.IsType<SessionExpired>(_dispatcher.Actions[^1]);
        Assert.Equal(3, _dispatcher.Actions.Count);
    }

    [Fact]
    public async Task Refresh_NewTokenArrives_KeepsTheSessionAndPlansTheNextRefresh()
    {
        await _tokens.SetAsync(Token(), _time.GetUtcNow().AddMinutes(60));
        using var monitor = new SessionMonitor(_tokens, _events, _dispatcher, _time);
        monitor.Start();

        _time.Advance(TimeSpan.FromMinutes(59));
        await _tokens.SetAsync(Token(), _time.GetUtcNow().AddMinutes(60));
        _time.Advance(TimeSpan.FromMinutes(30));
        Assert.Empty(_dispatcher.Actions.OfType<SessionExpired>());

        _time.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(2, _dispatcher.Actions.OfType<RefreshSession>().Count());
    }

    [Fact]
    public async Task Unauthorized_SeveralAtOnce_AsksForANewTokenOnceAndExpiresOnce()
    {
        await _tokens.SetAsync(Token(), _time.GetUtcNow().AddMinutes(60));
        using var monitor = new SessionMonitor(_tokens, _events, _dispatcher, _time);
        monitor.Start();

        _events.RaiseUnauthorized();
        _events.RaiseUnauthorized();
        _time.Advance(TimeSpan.FromHours(2));

        Assert.Single(_dispatcher.Actions.OfType<RefreshSession>());
        Assert.Single(_dispatcher.Actions.OfType<SessionExpired>());
    }

    [Fact]
    public async Task ClearAsync_SignedOut_CancelsTimersAndDropsToken()
    {
        await _tokens.SetAsync(Token(), _time.GetUtcNow().AddMinutes(60));
        using var monitor = new SessionMonitor(_tokens, _events, _dispatcher, _time);
        monitor.Start();

        await _tokens.ClearAsync();
        _time.Advance(TimeSpan.FromHours(2));

        Assert.Null(_tokens.ValidToken);
        Assert.IsType<SessionRestored>(Assert.Single(_dispatcher.Actions));
    }

    private static string Token()
    {
        var payload = """{"sub":"99cf6d64-ee83-4a4b-9f20-8cb359c15b6a","email":"baker@example.com","name":"Docs Baker","role":"user"}""";
        return $"e30.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}.sig";
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public List<object> Actions { get; } = [];

        public void Dispatch(object action)
        {
            Actions.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }
}
