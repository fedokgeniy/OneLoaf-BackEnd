using System.Net.Http.Headers;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web.Shared.Api.Handlers;

/// <summary>Adds <c>Authorization: Bearer …</c> from the <see cref="TokenStore"/> when a valid token exists.</summary>
public sealed class AuthTokenHandler(TokenStore tokens) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (tokens.ValidToken is { } token && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
