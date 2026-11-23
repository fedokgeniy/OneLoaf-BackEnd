using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Library.Services;

namespace OneLoaf.Web.Features.Library;

/// <summary>Registration of the Library feature module (public catalog, detail, comments, reactions, copy, publish, moderation (lazy-loaded in F5)).</summary>
public static class LibraryFeature
{
    /// <summary>Registers the typed API service; Fluxor finds the state, reducers and effects by scanning.</summary>
    public static IServiceCollection AddLibraryFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ILibraryApi, LibraryApi>();
        return services;
    }
}
