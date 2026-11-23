using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Recipes.Services;

namespace OneLoaf.Web.Features.Recipes;

/// <summary>Registration of the Recipes feature module (recipes, editor, variants, calculator, ingredients, prices, kitchen, flours).</summary>
public static class RecipesFeature
{
    /// <summary>Registers the typed API services of the feature; Fluxor finds its state, reducers and effects by scanning.</summary>
    public static IServiceCollection AddRecipesFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRecipesApi, RecipesApi>();
        return services;
    }
}
