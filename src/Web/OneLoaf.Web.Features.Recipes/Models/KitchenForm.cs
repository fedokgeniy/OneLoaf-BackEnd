using System.ComponentModel.DataAnnotations;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Recipes.Models;

/// <summary>Kitchen and cost settings form (US-13, US-47–49). Empty optional values leave that cost line out.</summary>
public sealed class KitchenForm
{
    /// <summary>Oven power in kW (electricity cost).</summary>
    [Range(typeof(decimal), "0.1", "100", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0.1 to 100 kW.")]
    public decimal? OvenPowerKw { get; set; }

    /// <summary>Loaves per oven load.</summary>
    [Range(1, 1000, ErrorMessage = "Enter 1 to 1000 loaves.")]
    public int? OvenCapacityLoaves { get; set; }

    /// <summary>Mixer bowl capacity in kg of dough (kneading cycles).</summary>
    [Range(typeof(decimal), "0.1", "500", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0.1 to 500 kg.")]
    public decimal? MixerBowlCapacityKg { get; set; }

    /// <summary>Electricity price per kWh.</summary>
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0 to 100.")]
    public decimal? ElectricityTariffPerKwh { get; set; }

    /// <summary>Packaging cost per loaf (US-47).</summary>
    [Required(ErrorMessage = "Enter the packaging cost (0 if none).")]
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0 to 1000.")]
    public decimal? PackagingCostPerLoaf { get; set; } = 0m;

    /// <summary>Expected loss in percent (US-48).</summary>
    [Required(ErrorMessage = "Enter the loss (0 if none).")]
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0 to 100 %.")]
    public decimal? LossRatePercent { get; set; } = 0m;

    /// <summary>Labor rate per hour (US-49); empty = no labor line.</summary>
    [Range(typeof(decimal), "0", "10000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0 to 10 000.")]
    public decimal? LaborRatePerHour { get; set; }

    /// <summary>Currency of all costs.</summary>
    [Required(ErrorMessage = "Choose a currency.")]
    public string? Currency { get; set; } = "EUR";

    /// <summary>IANA time zone, e.g. Europe/Vilnius (times of batches and reminders).</summary>
    [Required(ErrorMessage = "Enter the time zone.")]
    [StringLength(100, ErrorMessage = "Use at most 100 characters.")]
    public string? TimeZone { get; set; }

    /// <summary>From the saved settings, or defaults with the browser's time zone.</summary>
    public static KitchenForm From(KitchenSettingsDto? settings, string browserTimeZone) => settings is null
        ? new KitchenForm { TimeZone = browserTimeZone }
        : new KitchenForm
        {
            OvenPowerKw = settings.OvenPowerKw,
            OvenCapacityLoaves = settings.OvenCapacityLoaves,
            MixerBowlCapacityKg = settings.MixerBowlCapacityKg,
            ElectricityTariffPerKwh = settings.ElectricityTariffPerKwh,
            PackagingCostPerLoaf = settings.PackagingCostPerLoaf,
            LossRatePercent = settings.LossRatePercent,
            LaborRatePerHour = settings.LaborRatePerHour,
            Currency = settings.Currency,
            TimeZone = settings.TimeZone,
        };

    /// <summary>The request.</summary>
    public KitchenSettingsRequest ToRequest() => new(
        OvenPowerKw, OvenCapacityLoaves, MixerBowlCapacityKg, ElectricityTariffPerKwh,
        PackagingCostPerLoaf ?? 0m, LossRatePercent ?? 0m, LaborRatePerHour, Currency!, TimeZone!.Trim());
}
