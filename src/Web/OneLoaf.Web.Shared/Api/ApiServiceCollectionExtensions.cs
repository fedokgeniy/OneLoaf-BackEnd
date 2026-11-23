using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneLoaf.Web.Shared.Api.Handlers;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web.Shared.Api;

/// <summary>Registration of the request pipeline (api-client.md §2).</summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ServiceEndpoints"/>, the handler chain CorrelationId → AuthToken → ProblemDetails → Logging,
    /// one named <see cref="HttpClient"/> per service and the <see cref="ApiClientFactory"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">App configuration with the <c>Api</c> section.</param>
    /// <param name="appBaseAddress">Address the app is served from; relative API URLs resolve against it.</param>
    public static IServiceCollection AddOneLoafApi(
        this IServiceCollection services, IConfiguration configuration, Uri appBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<ServiceEndpoints>()
            .Bind(configuration.GetSection(ServiceEndpoints.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuthEvents>();
        services.AddSingleton<TokenStore>();
        services.AddSingleton<ApiJsonOptions>();
        services.AddApiJsonContext(SharedJsonContext.Default);

        services.AddTransient<CorrelationIdHandler>()
            .AddTransient<AuthTokenHandler>()
            .AddTransient<ProblemDetailsHandler>()
            .AddTransient<LoggingHandler>();

        foreach (var name in ServiceNames.All)
        {
            services.AddHttpClient(name, (sp, client) =>
                    client.BaseAddress = sp.GetRequiredService<IOptions<ServiceEndpoints>>().Value
                        .For(name, appBaseAddress))
                .AddHttpMessageHandler<CorrelationIdHandler>()
                .AddHttpMessageHandler<AuthTokenHandler>()
                .AddHttpMessageHandler<ProblemDetailsHandler>()
                .AddHttpMessageHandler<LoggingHandler>();
        }

        services.AddScoped<ApiClientFactory>();
        return services;
    }

    /// <summary>Adds a source-generated JSON context with the contracts of a feature.</summary>
    public static IServiceCollection AddApiJsonContext(this IServiceCollection services, IJsonTypeInfoResolver context)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(context);
        return services;
    }
}
