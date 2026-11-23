using FluentValidation;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Kitchen;

/// <summary>Equipment and cost parameters of the current user (US-13, US-47 - US-49).</summary>
/// <param name="OvenPowerKw">Oven power, kW.</param>
/// <param name="OvenCapacityLoaves">Loaves per oven load.</param>
/// <param name="MixerBowlCapacityKg">Mixer bowl capacity, kg.</param>
/// <param name="ElectricityTariffPerKwh">Price of one kWh.</param>
/// <param name="PackagingCostPerLoaf">Packaging cost per loaf.</param>
/// <param name="LossRatePercent">Loss rate, 0-100.</param>
/// <param name="LaborRatePerHour">Hourly rate of own work; null = labor not counted.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="TimeZone">IANA time zone.</param>
public sealed record KitchenSettingsRequest(
    decimal? OvenPowerKw,
    int? OvenCapacityLoaves,
    decimal? MixerBowlCapacityKg,
    decimal? ElectricityTariffPerKwh,
    decimal PackagingCostPerLoaf,
    decimal LossRatePercent,
    decimal? LaborRatePerHour,
    string Currency,
    string TimeZone);

/// <summary>Kitchen settings as stored.</summary>
/// <param name="OvenPowerKw">Oven power, kW.</param>
/// <param name="OvenCapacityLoaves">Loaves per oven load.</param>
/// <param name="MixerBowlCapacityKg">Mixer bowl capacity, kg.</param>
/// <param name="ElectricityTariffPerKwh">Price of one kWh.</param>
/// <param name="PackagingCostPerLoaf">Packaging cost per loaf.</param>
/// <param name="LossRatePercent">Loss rate, 0-100.</param>
/// <param name="LaborRatePerHour">Hourly rate of own work.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="TimeZone">IANA time zone.</param>
/// <param name="UpdatedAt">Time of the last change.</param>
public sealed record KitchenSettingsDto(
    decimal? OvenPowerKw,
    int? OvenCapacityLoaves,
    decimal? MixerBowlCapacityKg,
    decimal? ElectricityTariffPerKwh,
    decimal PackagingCostPerLoaf,
    decimal LossRatePercent,
    decimal? LaborRatePerHour,
    string Currency,
    string TimeZone,
    DateTimeOffset UpdatedAt);

/// <summary>Data access for <c>kitchen_settings</c>.</summary>
public interface IKitchenSettingsRepository
{
    /// <summary>Settings of the user, or null.</summary>
    Task<KitchenSettingsDto?> GetAsync(Guid ownerId, CancellationToken cancellationToken);

    /// <summary>Replaces the settings; false when the user has no settings row.</summary>
    Task<bool> UpdateAsync(Guid ownerId, KitchenSettingsRequest settings, CancellationToken cancellationToken);
}

/// <summary>Validates <see cref="KitchenSettingsRequest"/> with the ranges of the database columns.</summary>
public sealed class KitchenSettingsRequestValidator : AbstractValidator<KitchenSettingsRequest>
{
    /// <summary>Creates the rules.</summary>
    public KitchenSettingsRequestValidator()
    {
        RuleFor(x => x.OvenPowerKw).GreaterThan(0).LessThan(10_000);
        RuleFor(x => x.OvenCapacityLoaves).GreaterThan(0).LessThanOrEqualTo(10_000);
        RuleFor(x => x.MixerBowlCapacityKg).GreaterThan(0).LessThan(10_000);
        RuleFor(x => x.ElectricityTariffPerKwh).GreaterThanOrEqualTo(0).LessThan(10_000);
        RuleFor(x => x.PackagingCostPerLoaf).GreaterThanOrEqualTo(0).LessThan(1_000_000);
        RuleFor(x => x.LossRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.LaborRatePerHour).GreaterThanOrEqualTo(0).LessThan(100_000_000);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64)
            .Must(BeKnownTimeZone).WithMessage("'Time Zone' must be an IANA time zone, for example Europe/Vilnius.");
    }

    private static bool BeKnownTimeZone(string timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _);
}

/// <summary>The current user's kitchen settings (US-13, US-47 - US-49).</summary>
public sealed class KitchenSettingsService(
    IKitchenSettingsRepository settings,
    ReferenceGuard reference,
    ICurrentUser currentUser)
{
    /// <summary>Message when the settings row is missing.</summary>
    public const string NotFoundMessage = "Kitchen settings were not found.";

    /// <summary>Settings of the current user.</summary>
    /// <exception cref="NotFoundException">No settings row (account deleted).</exception>
    public async Task<KitchenSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        await settings.GetAsync(currentUser.Id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Replaces the settings and returns them as stored.</summary>
    public async Task<KitchenSettingsDto> UpdateAsync(KitchenSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await reference.EnsureCurrencyAsync(request.Currency, "currency", cancellationToken);

        if (!await settings.UpdateAsync(currentUser.Id, request, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await GetAsync(cancellationToken);
    }

    /// <summary>Time zone of the current user's kitchen (UTC when it is unknown on this machine).</summary>
    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(currentUser.Id, cancellationToken);
        return current is not null && TimeZoneInfo.TryFindSystemTimeZoneById(current.TimeZone, out var zone)
            ? zone
            : TimeZoneInfo.Utc;
    }
}
