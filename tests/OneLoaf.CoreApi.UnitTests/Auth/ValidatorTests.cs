using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;

namespace OneLoaf.CoreApi.UnitTests.Auth;

public sealed class ValidatorTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly LoginRequestValidator _login = new();
    private readonly DeleteAccountRequestValidator _delete = new();

    [Fact]
    public void Register_ValidRequest_Passes() =>
        Assert.True(_register.Validate(new RegisterRequest("anna@example.com", "secret-pass", "Anna")).IsValid);

    [Theory]
    [InlineData("", "secret-pass", "Anna", "Email")]
    [InlineData("anna@example", "secret-pass", "Anna", "Email")]
    [InlineData("anna example@x.com", "secret-pass", "Anna", "Email")]
    [InlineData("anna@example.com", "short", "Anna", "Password")]
    [InlineData("anna@example.com", "secret-pass", "   ", "DisplayName")]
    [InlineData("anna@example.com", "secret-pass", "", "DisplayName")]
    public void Register_InvalidField_Fails(string email, string password, string displayName, string field)
    {
        var result = _register.Validate(new RegisterRequest(email, password, displayName));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void Register_TooLongValues_Fail()
    {
        var result = _register.Validate(new RegisterRequest(
            new string('a', 250) + "@example.com", new string('p', 129), new string('n', 101)));

        Assert.Equal(["DisplayName", "Email", "Password"],
            result.Errors.Select(e => e.PropertyName).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Login_ShortPassword_IsNotAValidationError() =>
        Assert.True(_login.Validate(new LoginRequest("anna@example.com", "x")).IsValid);

    [Fact]
    public void Login_EmptyFields_Fail() =>
        Assert.Equal(2, _login.Validate(new LoginRequest("", "")).Errors.Count);

    [Fact]
    public void Delete_EmptyPassword_Fails() =>
        Assert.False(_delete.Validate(new DeleteAccountRequest("")).IsValid);

    [Theory]
    [InlineData(" Anna@Example.com ", "Anna@Example.com", "ANNA@EXAMPLE.COM")]
    [InlineData("boris@x.io", "boris@x.io", "BORIS@X.IO")]
    public void EmailNormalizer_TrimsAndUppercases(string input, string clean, string normalized)
    {
        Assert.Equal(clean, EmailNormalizer.Clean(input));
        Assert.Equal(normalized, EmailNormalizer.Normalize(input));
    }
}
