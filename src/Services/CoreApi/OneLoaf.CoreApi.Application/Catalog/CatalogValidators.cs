using FluentValidation;
using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.Catalog;

/// <summary>Column limits of the catalog tables.</summary>
public static class CatalogLimits
{
    /// <summary><c>flours.name</c>, <c>ingredients.name</c>.</summary>
    public const int NameMaxLength = 200;

    /// <summary><c>flours.grind</c>.</summary>
    public const int GrindMaxLength = 50;

    /// <summary><c>flours.notes</c>.</summary>
    public const int NotesMaxLength = 2000;

    /// <summary>Largest price that fits <c>numeric(12,4)</c>.</summary>
    public const decimal MaxPrice = 99_999_999m;

    /// <summary>Largest quantity that fits <c>numeric(12,3)</c>.</summary>
    public const decimal MaxQuantity = 999_999_999m;
}

/// <summary>Validates <see cref="FlourRequest"/>.</summary>
public sealed class FlourRequestValidator : AbstractValidator<FlourRequest>
{
    /// <summary>Creates the rules.</summary>
    public FlourRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).MaximumLength(CatalogLimits.NameMaxLength);
        RuleFor(x => x.FlourTypeCode).NotEmpty();
        RuleFor(x => x.ProteinPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.Grind).MaximumLength(CatalogLimits.GrindMaxLength);
        RuleFor(x => x.Notes).MaximumLength(CatalogLimits.NotesMaxLength);
    }
}

/// <summary>Validates <see cref="FlourListQuery"/>.</summary>
public sealed class FlourListQueryValidator : AbstractValidator<FlourListQuery>
{
    /// <summary>Creates the rules.</summary>
    public FlourListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(CatalogLimits.NameMaxLength);
    }
}

/// <summary>Validates <see cref="IngredientRequest"/>.</summary>
public sealed class IngredientRequestValidator : AbstractValidator<IngredientRequest>
{
    /// <summary>Creates the rules.</summary>
    public IngredientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).MaximumLength(CatalogLimits.NameMaxLength);
        RuleFor(x => x.CategoryCode).NotEmpty();
        RuleFor(x => x.UnitCode).NotEmpty();
        RuleFor(x => x.FlourId).NotEqual(Guid.Empty);
    }
}

/// <summary>Validates <see cref="IngredientListQuery"/>.</summary>
public sealed class IngredientListQueryValidator : AbstractValidator<IngredientListQuery>
{
    /// <summary>Creates the rules.</summary>
    public IngredientListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(CatalogLimits.NameMaxLength);
    }
}

/// <summary>Validates <see cref="PriceRequest"/>.</summary>
public sealed class PriceRequestValidator : AbstractValidator<PriceRequest>
{
    /// <summary>Creates the rules.</summary>
    public PriceRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).LessThan(CatalogLimits.MaxPrice);
        RuleFor(x => x.PerQuantity).GreaterThan(0).LessThan(CatalogLimits.MaxQuantity);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.EffectiveFrom).NotEqual(default(DateOnly));
    }
}
