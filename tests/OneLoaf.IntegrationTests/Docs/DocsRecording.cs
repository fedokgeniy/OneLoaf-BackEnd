using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.Docs;

/// <summary>One recorded HTTP exchange.</summary>
/// <param name="Case">What the example shows ("typical", "duplicate e-mail", ...).</param>
/// <param name="Method">HTTP method.</param>
/// <param name="Path">Path with query.</param>
/// <param name="Authenticated">The request carried a bearer token.</param>
/// <param name="RequestBody">Request JSON.</param>
/// <param name="Status">Response status code.</param>
/// <param name="ResponseContentType">Response media type.</param>
/// <param name="ResponseBody">Response body (JSON parsed when possible).</param>
public sealed record RecordedExchange(
    string Case, string Method, string Path, bool Authenticated, JsonNode? RequestBody, int Status, string? ResponseContentType,
    JsonNode? ResponseBody);

/// <summary>Recorded exchanges by service and operation id.</summary>
public sealed class ExampleStore
{
    private readonly Dictionary<string, Dictionary<string, List<RecordedExchange>>> _byService = new(StringComparer.Ordinal);

    /// <summary>Examples of a service by operation id.</summary>
    public IReadOnlyDictionary<string, List<RecordedExchange>> For(string service) =>
        _byService.TryGetValue(service, out var operations) ? operations : new Dictionary<string, List<RecordedExchange>>();

    /// <summary>Adds an example.</summary>
    public void Add(string service, string operationId, RecordedExchange exchange)
    {
        if (!_byService.TryGetValue(service, out var operations))
        {
            operations = new Dictionary<string, List<RecordedExchange>>(StringComparer.Ordinal);
            _byService[service] = operations;
        }

        if (!operations.TryGetValue(operationId, out var list))
        {
            list = [];
            operations[operationId] = list;
        }

        list.Add(exchange);
    }
}

/// <summary>A client of one service that records every call under an operation id.</summary>
public sealed class DocClient(string service, HttpMessageHandler handler, Uri baseAddress, ExampleStore store) : IDisposable
{
    private const string AccessTokenProperty = "accessToken";
    private const string RedactedToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.<payload>.<signature>";

    private readonly HttpClient _http = new(handler, disposeHandler: false) { BaseAddress = baseAddress };

    /// <summary>Uses the bearer token for the following calls (null removes it).</summary>
    public DocClient As(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        return this;
    }

    /// <summary>Calls the endpoint and records the exchange; returns the parsed JSON response (or null).</summary>
    public async Task<JsonNode?> CallAsync(string operationId, string caseName, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        JsonNode? requestJson = null;
        if (body is not null)
        {
            requestJson = body as JsonNode ?? JsonSerializer.SerializeToNode(body, body.GetType(), CoreApiFactory.Json);
            request.Content = JsonContent.Create(requestJson, options: CoreApiFactory.Json);
        }

        using var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var text = mediaType?.StartsWith("image/", StringComparison.Ordinal) == true
            ? $"<binary {mediaType}, {(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length} bytes>"
            : await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonNode? json = null;
        if (text.Length > 0 && mediaType is not null && mediaType.Contains("json", StringComparison.Ordinal))
        {
            json = JsonNode.Parse(text);
        }

        var recorded = json?.DeepClone() ?? (text.Length > 0 ? JsonValue.Create(text) : null);
        if (recorded is JsonObject { } withToken && withToken.ContainsKey(AccessTokenProperty))
        {
            // Tokens are signed with the throw-away test key, but a JWT in the repository would still trip secret scanners.
            withToken[AccessTokenProperty] = RedactedToken;
        }
        store.Add(service, operationId, new RecordedExchange(caseName, method.Method, path, _http.DefaultRequestHeaders.Authorization is not null,
            requestJson?.DeepClone(), (int)response.StatusCode, mediaType, recorded?.DeepClone()));
        return json;
    }

    /// <summary>Uploads a file as multipart/form-data and records the exchange (the request body is described, not embedded).</summary>
    public Task<JsonNode?> UploadAsync(string operationId, string caseName, string path, string field, string fileName, string contentType, byte[] content) =>
        UploadAsync(operationId, caseName, HttpMethod.Post, path, field, fileName, contentType, content);

    /// <summary>The same with another method (PUT replaces the one photo of an ingredient or a recipe).</summary>
    public async Task<JsonNode?> UploadAsync(
        string operationId, string caseName, HttpMethod method, string path, string field, string fileName, string contentType, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, field, fileName);
        using var request = new HttpRequestMessage(method, path) { Content = form };
        using var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = text.Length > 0 ? JsonNode.Parse(text) : null;
        var description = new JsonObject
        {
            ["multipart/form-data"] = new JsonObject { [field] = $"<{fileName}, {contentType}, {content.Length} bytes>" },
        };
        store.Add(service, operationId, new RecordedExchange(caseName, method.Method, path, _http.DefaultRequestHeaders.Authorization is not null,
            description, (int)response.StatusCode, response.Content.Headers.ContentType?.MediaType, json?.DeepClone()));
        return json;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
