namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary><c>POST /api/v1/auth/register</c> body.</summary>
public sealed record RegisterRequest(string Email, string Password, string DisplayName);

/// <summary><c>POST /api/v1/auth/register</c> 201 response.</summary>
public sealed record RegisterResponse(Guid UserId);

/// <summary><c>POST /api/v1/auth/login</c> body.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary><c>POST /api/v1/auth/login</c> 200 response: a 60-minute access token, no refresh token.</summary>
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

/// <summary>The signed-in user (<c>GET /api/v1/me</c>, also part of <see cref="LoginResponse"/>).</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary><c>DELETE /me</c> body (US-04): the password confirms the deletion.</summary>
public sealed record DeleteAccountRequest(string Password);
