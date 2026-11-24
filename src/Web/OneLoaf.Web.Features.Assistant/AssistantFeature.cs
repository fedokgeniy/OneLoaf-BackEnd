using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Assistant.Services;

namespace OneLoaf.Web.Features.Assistant;

/// <summary>Registration of the Assistant feature module (conversations, chat, plan card, confirm/decline, usage (lazy-loaded in F5)).</summary>
public static class AssistantFeature
{
    /// <summary>Registers the typed API service; Fluxor finds the state, reducers and effects by scanning.</summary>
    public static IServiceCollection AddAssistantFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IAssistantApi, AssistantApi>();
        return services;
    }
}
