using Microsoft.Extensions.DependencyInjection;

namespace OneLoaf.Web.Features.Production;

/// <summary>Registration of the Production feature module (batches, new batch, batch execution, calendar, starters, feedings).</summary>
public static class ProductionFeature
{
    /// <summary>Registers the services of the feature. Pages, state and API services are added with its screens.</summary>
    public static IServiceCollection AddProductionFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
