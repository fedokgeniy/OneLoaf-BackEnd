using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Production.Services;

namespace OneLoaf.Web.Features.Production;

/// <summary>Registration of the Production feature module (batches, new batch, batch execution, calendar, starters, feedings).</summary>
public static class ProductionFeature
{
    /// <summary>Registers the typed API service; Fluxor finds the state, reducers and effects by scanning.</summary>
    public static IServiceCollection AddProductionFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IProductionApi, ProductionApi>();
        return services;
    }
}
