using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.CoreApi.UnitTests.Fakes;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.UnitTests.Auth;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeTokenIssuer _tokens = new();
    private readonly FakeRefreshTokenStore _refresh = new();
    private readonly AuthService _service;

    public AuthServiceTests() => _service = Service(Now);

    private AuthService Service(DateTimeOffset now) =>
        new(_users, _hasher, _tokens, _refresh, new SessionOptions(TimeSpan.FromDays(30)), new FixedTimeProvider(now),
            NullLogger<AuthService>.Instance);

    [Fact]
    public async Task US01_RegisterNewEmail_CreatesUserWithEmptyWorkspace()
    {
        var response = await _service.RegisterAsync(
            new RegisterRequest("  Anna@Example.com ", "secret-pass", " Anna "), TestContext.Current.CancellationToken);

        var user = Assert.Single(_users.Users);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal("Anna@Example.com", user.Email);
        Assert.Equal("ANNA@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("Anna", user.DisplayName);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Equal(FakePasswordHasher.Prefix + "secret-pass", user.PasswordHash);

        var kitchen = Assert.IsType<OneLoaf.Domain.Entities.KitchenSettings>(user.KitchenSettings);
        Assert.Equal(KitchenSettingsDefaults.Currency, kitchen.Currency);
        Assert.Equal(KitchenSettingsDefaults.TimeZone, kitchen.TimeZone);
        Assert.Equal(0m, kitchen.PackagingCostPerLoaf);
        Assert.Equal(0m, kitchen.LossRatePercent);
        Assert.Null(kitchen.OvenPowerKw);
        Assert.Null(kitchen.LaborRatePerHour);
    }

    [Fact]
    public async Task RegisterAsync_EmailTakenInAnotherCase_ThrowsConflict()
    {
        _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "x");

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(
            new RegisterRequest("ANNA@example.com", "secret-pass", "Anna 2"), TestContext.Current.CancellationToken));

        Assert.Equal(AuthService.EmailTakenMessage, exception.Message);
        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task US02_LoginWithCorrectPassword_ReturnsTokenAndRecordsLogin()
    {
        var user = _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");

        var response = await _service.LoginAsync(new LoginRequest("Anna@Example.com", "secret-pass"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal("token-for-" + user.Id, response.Response.AccessToken);
        Assert.Equal(user.Id, response.Response.User.Id);
        Assert.Equal(Now, response.Response.User.LastLoginAt);
        Assert.Equal(Now.AddDays(30), response.RefreshToken.ExpiresAt);
        Assert.Equal(RefreshTokenValues.Hash(response.RefreshToken.Value), Assert.Single(_refresh.Tokens).Hash);
        Assert.Equal((user.Id, Now, (string?)null), Assert.Single(_users.Logins));
        Assert.Equal(new TokenSubject(user.Id, "anna@example.com", "Anna", UserRole.User), _tokens.LastSubject);
    }

    [Fact]
    public async Task LoginAsync_OutdatedHash_RehashesPassword()
    {
        var user = _users.Seed("anna@example.com", FakePasswordHasher.OldPrefix + "secret-pass");

        var response = await _service.LoginAsync(new LoginRequest("anna@example.com", "secret-pass"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(FakePasswordHasher.Prefix + "secret-pass", Assert.Single(_users.Logins).NewHash);
        Assert.Equal(FakePasswordHasher.Prefix + "secret-pass", user.PasswordHash);
    }

    [Fact]
    public async Task US02_LoginWithWrongPassword_ReturnsNullAndRecordsNothing()
    {
        _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");

        var response = await _service.LoginAsync(new LoginRequest("anna@example.com", "wrong-pass"),
            TestContext.Current.CancellationToken);

        Assert.Null(response);
        Assert.Empty(_users.Logins);
        Assert.Null(_tokens.LastSubject);
    }

    [Fact]
    public async Task US02_LoginWithUnknownEmail_ReturnsNullAfterTheSameHashCheck()
    {
        var response = await _service.LoginAsync(new LoginRequest("nobody@example.com", "secret-pass"),
            TestContext.Current.CancellationToken);

        Assert.Null(response);
        Assert.Equal(1, _hasher.VerifyCalls);
        Assert.Null(_tokens.LastSubject);
    }

    [Fact]
    public async Task US02_Refresh_RotatesTheTokenAndKeepsTheFamily()
    {
        _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");
        var login = await _service.LoginAsync(new LoginRequest("anna@example.com", "secret-pass"), TestContext.Current.CancellationToken);

        var refreshed = await Service(Now.AddHours(2)).RefreshAsync(login!.RefreshToken.Value, TestContext.Current.CancellationToken);

        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken.Value, refreshed.RefreshToken.Value);
        Assert.Equal(Now.AddHours(2).AddDays(30), refreshed.RefreshToken.ExpiresAt);
        Assert.Equal(2, _refresh.Tokens.Count);
        Assert.Single(_refresh.Tokens.Select(t => t.FamilyId).Distinct());
        Assert.NotNull(_refresh.Tokens[0].RotatedAt);
        Assert.Null(_refresh.Tokens[1].RotatedAt);
    }

    [Fact]
    public async Task US02_Refresh_ExchangedTokenUsedAgain_RevokesTheWholeSignIn()
    {
        _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest("anna@example.com", "secret-pass"), ct);
        var next = await _service.RefreshAsync(login!.RefreshToken.Value, ct);

        var later = Service(Now.AddMinutes(5));
        var stolen = await later.RefreshAsync(login.RefreshToken.Value, ct);
        var owner = await later.RefreshAsync(next!.RefreshToken.Value, ct);

        Assert.Null(stolen);
        Assert.Null(owner);
        Assert.All(_refresh.Tokens, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task US02_Refresh_SecondTabExchangesTheSameTokenAtOnce_KeepsTheSignIn()
    {
        _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest("anna@example.com", "secret-pass"), ct);

        var firstTab = await _service.RefreshAsync(login!.RefreshToken.Value, ct);
        var secondTab = await Service(Now.AddSeconds(5)).RefreshAsync(login.RefreshToken.Value, ct);

        Assert.NotNull(firstTab);
        Assert.NotNull(secondTab);
        Assert.All(_refresh.Tokens, t => Assert.Null(t.RevokedAt));
    }

    [Fact]
    public async Task US02_Refresh_ExpiredUnknownOrSignedOutToken_ReturnsNull()
    {
        _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest("anna@example.com", "secret-pass"), ct);

        Assert.Null(await _service.RefreshAsync(null, ct));
        Assert.Null(await _service.RefreshAsync("not-a-token", ct));
        Assert.Null(await Service(Now.AddDays(31)).RefreshAsync(login!.RefreshToken.Value, ct));

        await _service.LogoutAsync(login.RefreshToken.Value, ct);
        Assert.Null(await _service.RefreshAsync(login.RefreshToken.Value, ct));
    }
}
