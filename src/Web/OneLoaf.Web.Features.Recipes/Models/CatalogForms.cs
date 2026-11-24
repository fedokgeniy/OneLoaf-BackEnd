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

    /// <summary>Keep the stock: batches and starter feedings take from it.</summary>
    public bool TrackStock { get; set; }

    /// <summary>Stock level below which the ingredient is shown as low; empty = no level.</summary>
    [Range(typeof(decimal), "0", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter a level of 0 or more.")]
    public decimal? ReorderLevel { get; set; }

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
            TrackStock = ingredient.TrackStock,
            ReorderLevel = ingredient.ReorderLevel,
        };
    }

    /// <summary>The request; the flour is sent only for a flour category.</summary>
    public IngredientRequest ToRequest(bool isFlour) => new(
        Name!.Trim(), CategoryCode!, UnitCode!,
        isFlour && Guid.TryParse(FlourId, CultureInfo.InvariantCulture, out var flour) ? flour : null,
        TrackStock, TrackStock ? ReorderLevel : null);
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

    /// <summary>From an existing price, to correct it.</summary>
    public static PriceForm From(PriceDto price)
    {
        ArgumentNullException.ThrowIfNull(price);
        return new PriceForm
        {
            Price = price.Price,
            PerQuantity = price.PerQuantity,
            Currency = price.Currency,
            EffectiveFrom = price.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>The request.</summary>
    public PriceRequest ToRequest() => new(
        Price!.Value, PerQuantity!.Value, Currency!,
        DateOnly.ParseExact(EffectiveFrom!, "yyyy-MM-dd", CultureInfo.InvariantCulture));
}

/// <summary>Purchase: a number of packs of one size bought for an amount.</summary>
public sealed class PurchaseForm
{
    /// <summary>Number of packs.</summary>
    [Required(ErrorMessage = "Enter the number of packs.")]
    [Range(1, 10000, ErrorMessage = "Enter 1 to 10000 packs.")]
    public int? PackCount { get; set; } = 1;

    /// <summary>Content of one pack in the ingredient unit.</summary>
    [Required(ErrorMessage = "Enter the pack size.")]
    [Range(typeof(decimal), "0.001", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter a size above 0.")]
    public decimal? PackSize { get; set; }

    /// <summary>Amount paid for all packs.</summary>
    [Required(ErrorMessage = "Enter the amount paid.")]
    [Range(typeof(decimal), "0", "99999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter an amount of 0 or more.")]
    public decimal? TotalCost { get; set; }

    /// <summary>Currency code.</summary>
    [Required(ErrorMessage = "Choose a currency.")]
    public string? Currency { get; set; } = "EUR";

    /// <summary>Purchase date (yyyy-MM-dd).</summary>
    [Required(ErrorMessage = "Enter the date.")]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Enter a date.")]
    public string? PurchasedOn { get; set; }

    /// <summary>Note, for example the shop.</summary>
    [StringLength(500, ErrorMessage = "Use at most 500 characters.")]
    public string? Note { get; set; }

    /// <summary>The request.</summary>
    public PurchaseRequest ToRequest() => new(
        PackCount!.Value, PackSize!.Value, TotalCost!.Value, Currency!,
        DateOnly.ParseExact(PurchasedOn!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());
}

/// <summary>Stock correction: the quantity counted on the shelf and the reason.</summary>
public sealed class StockAdjustmentForm
{
    /// <summary>Counted quantity in the ingredient unit.</summary>
    [Required(ErrorMessage = "Enter the quantity you counted.")]
    [Range(typeof(decimal), "0", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter a quantity of 0 or more.")]
    public decimal? ActualQuantity { get; set; }

    /// <summary>Why the stock is corrected.</summary>
    [Required(ErrorMessage = "Say why the stock is corrected.")]
    [StringLength(500, ErrorMessage = "Use at most 500 characters.")]
    public string? Note { get; set; }

    /// <summary>The request.</summary>
    public StockAdjustmentRequest ToRequest() => new(ActualQuantity!.Value, Note!.Trim());
}
