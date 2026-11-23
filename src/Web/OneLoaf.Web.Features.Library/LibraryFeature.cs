using Microsoft.Extensions.DependencyInjection;

namespace OneLoaf.Web.Features.Library;

/// <summary>Registration of the Library feature module (public catalog, detail, comments, reactions, copy, publish, moderation (lazy-loaded in F5)).</summary>
public static class LibraryFeature
{
    /// <summary>Registers the services of the feature. Pages, state and API services are added with its screens.</summary>
    public static IServiceCollection AddLibraryFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
