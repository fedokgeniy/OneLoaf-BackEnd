using System.ComponentModel.DataAnnotations;
using System.Globalization;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Recipes.Models;

/// <summary>Ingredient form (US-10) with the limits of the catalog validator.</summary>
public sealed class IngredientForm
{
    /// <summary>Name.</summary>
    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(200, ErrorMessage = "Use at most 200 characters.")]
    public string? Name { get; set; }

    /// <summary>Category code.</summary>
    [Required(ErrorMessage = "Choose a category.")]
    public string? CategoryCode { get; set; }

    /// <summary>Unit code.</summary>
    [Required(ErrorMessage = "Choose a unit.")]
    public string? UnitCode { get; set; } = "g";

    /// <summary>Flour of the flour catalog (US-11), only for flour ingredients.</summary>
    public string? FlourId { get; set; }

    /// <summary>From an existing ingredient.</summary>
    public static IngredientForm From(IngredientDto ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        return new IngredientForm
        {
            Name = ingredient.Name,
            CategoryCode = ingredient.CategoryCode,
            UnitCode = ingredient.UnitCode,
            FlourId = ingredient.FlourId?.ToString(),
        };
    }

    /// <summary>The request; the flour is sent only for a flour category.</summary>
    public IngredientRequest ToRequest(bool isFlour) => new(
        Name!.Trim(), CategoryCode!, UnitCode!,
        isFlour && Guid.TryParse(FlourId, CultureInfo.InvariantCulture, out var flour) ? flour : null);
}

/// <summary>New price (US-12): the amount paid for a quantity, valid from a date.</summary>
public sealed class PriceForm
{
    /// <summary>Amount paid.</summary>
    [Required(ErrorMessage = "Enter the price.")]
    [Range(typeof(decimal), "0", "99999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter a price of 0 or more.")]
    public decimal? Price { get; set; }

    /// <summary>Quantity the price is for, in the ingredient's unit (e.g. 1000 g).</summary>
    [Required(ErrorMessage = "Enter the quantity.")]
    [Range(typeof(decimal), "0.001", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter a quantity above 0.")]
    public decimal? PerQuantity { get; set; } = 1000m;

    /// <summary>Currency code.</summary>
    [Required(ErrorMessage = "Choose a currency.")]
    public string? Currency { get; set; } = "EUR";

    /// <summary>Valid from (yyyy-MM-dd).</summary>
    [Required(ErrorMessage = "Enter the date.")]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Enter a date.")]
    public string? EffectiveFrom { get; set; }

    /// <summary>The request.</summary>
    public PriceRequest ToRequest() => new(
        Price!.Value, PerQuantity!.Value, Currency!,
        DateOnly.ParseExact(EffectiveFrom!, "yyyy-MM-dd", CultureInfo.InvariantCulture));
}
