using Microsoft.Extensions.DependencyInjection;

namespace OneLoaf.Web.Features.Assistant;

/// <summary>Registration of the Assistant feature module (conversations, chat, plan card, confirm/decline, usage (lazy-loaded in F5)).</summary>
public static class AssistantFeature
{
    /// <summary>Registers the services of the feature. Pages, state and API services are added with its screens.</summary>
    public static IServiceCollection AddAssistantFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
