using Microsoft.Extensions.DependencyInjection;

namespace OneLoaf.ServiceDefaults.Correlation;

/// <summary>Registration of the correlation id services.</summary>
public static class CorrelationExtensions
{
    /// <summary>Registers <see cref="ICorrelationContext"/> and the outgoing request handler.</summary>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationContext, CorrelationContext>();
        services.AddTransient<CorrelationIdDelegatingHandler>();
        return services;
    }

    /// <summary>Adds the correlation id header to every request of this client.</summary>
    public static IHttpClientBuilder AddCorrelationIdPropagation(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
}
