using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.History.Services;

namespace OneLoaf.Web.Features.History;

/// <summary>Registration of the History feature module (bake records, rating, stage log, statistics, photos, analysis (lazy-loaded in F5)).</summary>
public static class HistoryFeature
{
    /// <summary>Registers the typed API service; Fluxor finds the state, reducers and effects by scanning.</summary>
    public static IServiceCollection AddHistoryFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IHistoryApi, HistoryApi>();
        return services;
    }
}
