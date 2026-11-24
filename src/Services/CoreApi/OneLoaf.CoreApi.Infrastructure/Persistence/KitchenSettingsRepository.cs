using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IKitchenSettingsRepository"/>.</summary>
public sealed class KitchenSettingsRepository(OneLoafDbContext db) : IKitchenSettingsRepository
{
    /// <inheritdoc />
    public Task<KitchenSettingsDto?> GetAsync(Guid ownerId, CancellationToken cancellationToken) =>
        db.KitchenSettings.AsNoTracking().Where(k => k.UserId == ownerId)
            .Select(k => new KitchenSettingsDto(k.OvenPowerKw, k.OvenCapacityLoaves, k.MixerBowlCapacityKg,
                k.ElectricityTariffPerKwh, k.PackagingCostPerLoaf, k.LossRatePercent, k.LaborRatePerHour, k.Currency,
                k.TimeZone, new DateTimeOffset(k.UpdatedAt, TimeSpan.Zero)))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid ownerId, KitchenSettingsRequest settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var updated = await db.KitchenSettings.Where(k => k.UserId == ownerId).ExecuteUpdateAsync(s => s
            .SetProperty(k => k.OvenPowerKw, settings.OvenPowerKw)
            .SetProperty(k => k.OvenCapacityLoaves, settings.OvenCapacityLoaves)
            .SetProperty(k => k.MixerBowlCapacityKg, settings.MixerBowlCapacityKg)
            .SetProperty(k => k.ElectricityTariffPerKwh, settings.ElectricityTariffPerKwh)
            .SetProperty(k => k.PackagingCostPerLoaf, settings.PackagingCostPerLoaf)
            .SetProperty(k => k.LossRatePercent, settings.LossRatePercent)
            .SetProperty(k => k.LaborRatePerHour, settings.LaborRatePerHour)
            .SetProperty(k => k.Currency, settings.Currency)
            .SetProperty(k => k.TimeZone, settings.TimeZone), cancellationToken);
        return updated > 0;
    }
}
