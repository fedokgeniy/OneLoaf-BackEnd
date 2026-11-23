using FluentValidation;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.Recipes;

/// <summary>Column limits of the recipe tables.</summary>
public static class RecipeLimits
{
    /// <summary><c>recipes.title</c>.</summary>
    public const int TitleMaxLength = 200;

    /// <summary><c>recipes.description</c>, <c>recipes.notes</c>, <c>recipe_stages.instructions</c>.</summary>
    public const int TextMaxLength = 4000;

    /// <summary><c>recipe_ingredients.note</c>.</summary>
    public const int NoteMaxLength = 500;

    /// <summary><c>recipe_stages.name</c>.</summary>
    public const int StageNameMaxLength = 100;

    /// <summary>Largest number of loaves in a request.</summary>
    public const int MaxLoaves = 1000;

    /// <summary>Largest number of lines of one kind in a recipe.</summary>
    public const int MaxLines = 200;

    /// <summary>Largest loaf weight that fits <c>numeric(8,1)</c>.</summary>
    public const decimal MaxLoafWeightG = 9_999_999m;

    /// <summary>Lowest stage temperature.</summary>
    public const decimal MinTemperatureC = -30m;

    /// <summary>Highest stage temperature.</summary>
    public const decimal MaxTemperatureC = 350m;

    /// <summary>Longest stage, one week in minutes.</summary>
    public const int MaxStageMinutes = 7 * 24 * 60;
}

/// <summary>Validates <see cref="RecipeRequest"/>; ownership of ingredients and stage codes are checked by the service.</summary>
public sealed class RecipeRequestValidator : AbstractValidator<RecipeRequest>
{
    /// <summary>Creates the rules.</summary>
    public RecipeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Must(t => !string.IsNullOrWhiteSpace(t)).MaximumLength(RecipeLimits.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(RecipeLimits.TextMaxLength);
        RuleFor(x => x.Notes).MaximumLength(RecipeLimits.TextMaxLength);
        RuleFor(x => x.YieldLoaves).InclusiveBetween(1, RecipeLimits.MaxLoaves);
        RuleFor(x => x.LoafWeightG).GreaterThan(0).LessThan(RecipeLimits.MaxLoafWeightG);
        RuleFor(x => x.Ingredients).NotNull().Must(l => l.Count <= RecipeLimits.MaxLines)
            .Must(HaveDistinctLineIds).WithMessage("Line ids must be unique.");
        RuleFor(x => x.Stages).NotNull().Must(l => l.Count <= RecipeLimits.MaxLines)
            .Must(HaveDistinctLineIds).WithMessage("Line ids must be unique.");
        RuleForEach(x => x.Ingredients).ChildRules(line =>
        {
            line.RuleFor(i => i.IngredientId).NotEmpty();
            line.RuleFor(i => i.Quantity).GreaterThanOrEqualTo(0).LessThan(CatalogLimits.MaxQuantity);
            line.RuleFor(i => i.Note).MaximumLength(RecipeLimits.NoteMaxLength);
            line.RuleFor(i => i.SortOrder).GreaterThanOrEqualTo(0);
        });
        RuleForEach(x => x.Stages).ChildRules(stage =>
        {
            stage.RuleFor(s => s.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n))
                .MaximumLength(RecipeLimits.StageNameMaxLength);
            stage.RuleFor(s => s.StageTypeCode).NotEmpty();
            stage.RuleFor(s => s.MinDurationMinutes).InclusiveBetween(0, RecipeLimits.MaxStageMinutes);
            stage.RuleFor(s => s.MaxDurationMinutes).InclusiveBetween(0, RecipeLimits.MaxStageMinutes)
                .GreaterThanOrEqualTo(s => s.MinDurationMinutes);
            stage.RuleFor(s => s.ActiveMinutes).GreaterThanOrEqualTo(0).LessThanOrEqualTo(s => s.MaxDurationMinutes);
            stage.RuleFor(s => s.TemperatureC).InclusiveBetween(RecipeLimits.MinTemperatureC, RecipeLimits.MaxTemperatureC);
            stage.RuleFor(s => s.ProofMode).IsInEnum();
            stage.RuleFor(s => s.Instructions).MaximumLength(RecipeLimits.TextMaxLength);
            stage.RuleFor(s => s.SortOrder).GreaterThanOrEqualTo(0);
        });
    }

    private static bool HaveDistinctLineIds(IReadOnlyList<RecipeIngredientInput>? lines) =>
        lines is null || DistinctIds(lines.Select(l => l.LineId));

    private static bool HaveDistinctLineIds(IReadOnlyList<RecipeStageInput>? stages) =>
        stages is null || DistinctIds(stages.Select(s => s.LineId));

    private static bool DistinctIds(IEnumerable<Guid?> ids)
    {
        var present = ids.Where(id => id.HasValue).ToList();
        return present.Count == present.Distinct().Count();
    }
}

/// <summary>Validates <see cref="CreateVariantRequest"/>.</summary>
public sealed class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
{
    /// <summary>Creates the rules.</summary>
    public CreateVariantRequestValidator() =>
        RuleFor(x => x.Title).NotEmpty().Must(t => !string.IsNullOrWhiteSpace(t)).MaximumLength(RecipeLimits.TitleMaxLength);
}

/// <summary>Validates <see cref="RecipeListQuery"/>.</summary>
public sealed class RecipeListQueryValidator : AbstractValidator<RecipeListQuery>
{
    /// <summary>Creates the rules.</summary>
    public RecipeListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(RecipeLimits.TitleMaxLength);
        RuleFor(x => x.MinProteinPercent).InclusiveBetween(0, 100);
    }
}

/// <summary>Validates <see cref="LoavesQuery"/>.</summary>
public sealed class LoavesQueryValidator : AbstractValidator<LoavesQuery>
{
    /// <summary>Creates the rules.</summary>
    public LoavesQueryValidator() => RuleFor(x => x.Loaves).InclusiveBetween(1, RecipeLimits.MaxLoaves);
}
