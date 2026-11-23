using Microsoft.Extensions.DependencyInjection;

namespace OneLoaf.Web.Features.History;

/// <summary>Registration of the History feature module (bake records, rating, stage log, statistics, photos, analysis (lazy-loaded in F5)).</summary>
public static class HistoryFeature
{
    /// <summary>Registers the services of the feature. Pages, state and API services are added with its screens.</summary>
    public static IServiceCollection AddHistoryFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
