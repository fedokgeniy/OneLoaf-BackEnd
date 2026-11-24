using FluentValidation;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Calculations;

/// <summary>
/// Calculator of the current user's recipes. Checks ownership, then delegates every number to the database
/// functions (no formula is repeated in C#).
/// </summary>
public sealed class CalculationService(
    IRecipeCalculations calculations,
    IRecipeRepository recipes,
    KitchenSettingsService kitchen,
    TimeProvider timeProvider,
    ICurrentUser currentUser)
{
    /// <summary>Cost breakdown (US-14, US-15, US-47 - US-49).</summary>
    public async Task<CostBreakdownDto> GetCostAsync(Guid recipeId, CostQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await EnsureOwnedAsync(recipeId, cancellationToken);

        var asOf = query.AsOf ?? await TodayInKitchenAsync(cancellationToken);
        var lines = await calculations.GetCostAsync(recipeId, query.Loaves, asOf, cancellationToken);
        var total = lines.SingleOrDefault(l => l.LineType == CostLineTypes.Total);
        var missing = lines
            .Where(l => l.LineType == CostLineTypes.Ingredient && !l.IsComplete && l.IngredientId.HasValue)
            .Select(l => new MissingPriceDto(l.IngredientId!.Value, l.Description))
            .ToList();

        return new CostBreakdownDto(
            recipeId, query.Loaves, asOf, lines.Count > 0 ? lines[0].Currency : string.Empty,
            total?.IsComplete ?? false,
            lines.Select(l => new CostLineDto(l.LineNo, l.LineType, l.IngredientId, l.Description, l.Quantity, l.UnitCode,
                l.UnitPrice, l.Amount, l.IsComplete)).ToList(),
            total?.Amount, missing);
    }

    /// <summary>Stage timeline and ready window for a start time (US-16).</summary>
    public async Task<TimelineDto> GetTimelineAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken)
    {
        await EnsureOwnedAsync(recipeId, cancellationToken);
        var stages = await calculations.GetTimelineAsync(recipeId, start, cancellationToken);
        var ready = await calculations.GetReadyWindowAsync(recipeId, start, cancellationToken);
        return new TimelineDto(recipeId, start, stages, ready.From, ready.To);
    }

    /// <summary>Start window for a desired ready time (US-52).</summary>
    public async Task<StartWindowDto> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken cancellationToken)
    {
        await EnsureOwnedAsync(recipeId, cancellationToken);
        var window = await calculations.GetStartWindowAsync(recipeId, readyAt, cancellationToken);
        return new StartWindowDto(recipeId, readyAt, window.From, window.To);
    }

    /// <summary>Dough weight, kneading cycles and starter amount (US-17, US-18).</summary>
    public async Task<ProductionFiguresDto> GetProductionFiguresAsync(Guid recipeId, int loaves, CancellationToken cancellationToken)
    {
        await EnsureOwnedAsync(recipeId, cancellationToken);
        var figures = await calculations.GetProductionFiguresAsync(recipeId, loaves, cancellationToken);
        return new ProductionFiguresDto(recipeId, loaves, figures.DoughWeightG, figures.KneadingCycles, figures.StarterAmountG);
    }

    private async Task EnsureOwnedAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        if (!await recipes.ExistsAsync(currentUser.Id, recipeId, cancellationToken))
        {
            throw new NotFoundException(RecipeService.NotFoundMessage);
        }
    }

    private async Task<DateOnly> TodayInKitchenAsync(CancellationToken cancellationToken)
    {
        var zone = await kitchen.GetTimeZoneAsync(cancellationToken);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
    }
}
