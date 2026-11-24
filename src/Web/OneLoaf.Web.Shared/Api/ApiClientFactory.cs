using Microsoft.Extensions.Logging;

namespace OneLoaf.Web.Shared.Api;

/// <summary>Creates an <see cref="ApiClient"/> per service over the named <see cref="HttpClient"/>s.</summary>
public sealed class ApiClientFactory(IHttpClientFactory httpClients, ApiJsonOptions json, ILogger<ApiClient> logger)
{
    /// <summary>Core API client.</summary>
    public ApiClient Core => Create(ServiceNames.Core);

    /// <summary>AI Assistant client.</summary>
    public ApiClient Assistant => Create(ServiceNames.Assistant);

    /// <summary>Notification client.</summary>
    public ApiClient Notification => Create(ServiceNames.Notification);

    private ApiClient Create(string name) => new(httpClients.CreateClient(name), json, logger);
}
