using Microsoft.Extensions.DependencyInjection;

namespace OneLoaf.Web.Features.Recipes;

/// <summary>Registration of the Recipes feature module (recipes, editor, variants, calculator, ingredients, prices, kitchen, flours).</summary>
public static class RecipesFeature
{
    /// <summary>Registers the services of the feature. Pages, state and API services are added with its screens.</summary>
    public static IServiceCollection AddRecipesFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
