namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Kitchen and cost settings (<c>GET /kitchen-settings</c>, US-13, US-47–49).</summary>
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

/// <summary><c>PUT /kitchen-settings</c> body.</summary>
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
