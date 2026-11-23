using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.History.Services;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.History;

/// <summary>Lazy-loading entry of the feature (ADR-F04): its effects get the typed API service created here.</summary>
public sealed class HistoryModule : IFeatureModule
{
    /// <inheritdoc />
    public IReadOnlyList<object> CreateServices(IServiceProvider services) => [ActivatorUtilities.CreateInstance<HistoryApi>(services)];
}
