using FluentValidation;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.Application.Calculations;

/// <summary>A recipe and the loaves to bake.</summary>
/// <param name="RecipeId">Own recipe.</param>
/// <param name="Loaves">Loaves, 1-1000.</param>
public sealed record OvenLoadingItem(Guid RecipeId, int Loaves);

/// <summary>Body of <c>POST /calculations/oven-loading</c>.</summary>
/// <param name="Items">Recipes to bake, 1-20.</param>
public sealed record OvenLoadingRequest(IReadOnlyList<OvenLoadingItem> Items);

/// <summary>Loaves of one recipe in a round.</summary>
/// <param name="RecipeId">Recipe.</param>
/// <param name="Title">Title.</param>
/// <param name="Loaves">Loaves in this round.</param>
public sealed record OvenRoundItemDto(Guid RecipeId, string Title, int Loaves);

/// <summary>One oven load.</summary>
/// <param name="Round">Number, from 1.</param>
/// <param name="Loaves">Loaves in the oven.</param>
/// <param name="BakeMinutes">Longest oven time of the recipes in the round.</param>
/// <param name="StartOffsetMinutes">Start of the round after the start of the first one (rounds follow each other).</param>
/// <param name="Items">Recipes in the round.</param>
public sealed record OvenRoundDto(int Round, int Loaves, int BakeMinutes, int StartOffsetMinutes, IReadOnlyList<OvenRoundItemDto> Items);

/// <summary>Oven loading plan (US-19).</summary>
/// <param name="OvenCapacityLoaves">Capacity from the kitchen settings.</param>
/// <param name="TotalLoaves">Loaves of all items.</param>
/// <param name="Rounds">Oven loads in order.</param>
/// <param name="TotalOvenMinutes">Oven time of all rounds.</param>
public sealed record OvenLoadingPlanDto(int OvenCapacityLoaves, int TotalLoaves, IReadOnlyList<OvenRoundDto> Rounds, int TotalOvenMinutes);

/// <summary>A recipe prepared for the planner.</summary>
/// <param name="RecipeId">Recipe.</param>
/// <param name="Title">Title.</param>
/// <param name="Loaves">Loaves to bake.</param>
/// <param name="BakeMinutes">Oven time of the recipe (maximum duration of its oven stages).</param>
public sealed record OvenLoad(Guid RecipeId, string Title, int Loaves, int BakeMinutes);

/// <summary>Validates <see cref="OvenLoadingRequest"/>.</summary>
public sealed class OvenLoadingRequestValidator : AbstractValidator<OvenLoadingRequest>
{
    /// <summary>Most items in one request.</summary>
    public const int MaxItems = 20;

    /// <summary>Creates the rules.</summary>
    public OvenLoadingRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= MaxItems).WithMessage($"items can contain at most {MaxItems} recipes.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.RecipeId).NotEmpty();
            item.RuleFor(i => i.Loaves).InclusiveBetween(1, 1000);
        });
    }
}

/// <summary>
/// Splits loaves into oven rounds within the oven capacity. Recipes with the same oven time are baked together;
/// within that, larger quantities are placed first and a recipe is split only when it does not fit in a round.
/// </summary>
public static class OvenLoadingPlanner
{
    /// <summary>Builds the rounds.</summary>
    public static OvenLoadingPlanDto Plan(IReadOnlyList<OvenLoad> loads, int capacity)
    {
        ArgumentNullException.ThrowIfNull(loads);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        var rounds = new List<(int Bake, List<OvenRoundItemDto> Items)>();
        foreach (var group in loads.GroupBy(l => l.BakeMinutes).OrderByDescending(g => g.Key))
        {
            var current = new List<OvenRoundItemDto>();
            var free = capacity;
            foreach (var load in group.OrderByDescending(l => l.Loaves).ThenBy(l => l.Title, StringComparer.Ordinal))
            {
                var remaining = load.Loaves;
                while (remaining > 0)
                {
                    if (free == 0)
                    {
                        rounds.Add((group.Key, current));
                        current = [];
                        free = capacity;
                    }

                    var placed = Math.Min(remaining, free);
                    current.Add(new OvenRoundItemDto(load.RecipeId, load.Title, placed));
                    free -= placed;
                    remaining -= placed;
                }
            }

            if (current.Count > 0)
            {
                rounds.Add((group.Key, current));
            }
        }

        var offset = 0;
        var result = new List<OvenRoundDto>(rounds.Count);
        foreach (var (bake, items) in rounds)
        {
            result.Add(new OvenRoundDto(result.Count + 1, items.Sum(i => i.Loaves), bake, offset, items));
            offset += bake;
        }

        return new OvenLoadingPlanDto(capacity, loads.Sum(l => l.Loaves), result, offset);
    }
}

/// <summary>Oven loading suggestion for own recipes (US-19).</summary>
public sealed class OvenLoadingService(RecipeService recipes, KitchenSettingsService kitchen)
{
    /// <summary>Plans the rounds; 422 when the oven capacity is not set in the kitchen settings.</summary>
    public async Task<OvenLoadingPlanDto> PlanAsync(OvenLoadingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await kitchen.GetAsync(cancellationToken);
        if (settings.OvenCapacityLoaves is null or < 1)
        {
            throw new BusinessRuleException("Set the oven capacity (loaves) in the kitchen settings first.");
        }

        var loads = new List<OvenLoad>();
        foreach (var item in request.Items)
        {
            var recipe = await recipes.GetAsync(item.RecipeId, cancellationToken);
            var bake = recipe.Stages.Where(s => s.UsesOven).Select(s => s.MaxDurationMinutes).DefaultIfEmpty(0).Max();
            loads.Add(new OvenLoad(recipe.Id, recipe.Title, item.Loaves, bake));
        }

        return OvenLoadingPlanner.Plan(loads, settings.OvenCapacityLoaves!.Value);
    }
}
