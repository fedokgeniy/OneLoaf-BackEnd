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
    private readonly AuthService _service;

    public AuthServiceTests() =>
        _service = new AuthService(_users, _hasher, _tokens, new FixedTimeProvider(Now), NullLogger<AuthService>.Instance);

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
        Assert.Equal("token-for-" + user.Id, response.AccessToken);
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal(Now, response.User.LastLoginAt);
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
}
