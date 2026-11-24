using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Catalog;

/// <summary>Flour catalog of the current user (US-11).</summary>
public sealed class FlourService(IFlourRepository flours, ReferenceGuard reference, ICurrentUser currentUser)
{
    /// <summary>Message for a missing flour.</summary>
    public const string NotFoundMessage = "The flour was not found.";

    /// <summary>Message for a duplicate name.</summary>
    public const string NameTakenMessage = "A flour with this name already exists.";

    /// <summary>Page of flours.</summary>
    public Task<PagedResult<FlourDto>> ListAsync(FlourListQuery query, CancellationToken cancellationToken) =>
        flours.ListAsync(currentUser.Id, query, cancellationToken);

    /// <summary>One flour.</summary>
    public async Task<FlourDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await flours.GetAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Creates a flour.</summary>
    public async Task<FlourDto> CreateAsync(FlourRequest request, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(request, null, cancellationToken);
        var id = await flours.CreateAsync(currentUser.Id, normalized, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Replaces a flour.</summary>
    public async Task<FlourDto> UpdateAsync(Guid id, FlourRequest request, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(request, id, cancellationToken);
        if (!await flours.UpdateAsync(currentUser.Id, id, normalized, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Deletes a flour; linked ingredients and starters keep existing without it.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await flours.DeleteAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    private async Task<FlourRequest> PrepareAsync(FlourRequest request, Guid? id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = request with { Name = request.Name.Trim() };
        await reference.EnsureFlourTypeAsync(normalized.FlourTypeCode, "flourTypeCode", cancellationToken);
        if (await flours.NameExistsAsync(currentUser.Id, normalized.Name, id, cancellationToken))
        {
            throw new ConflictException(NameTakenMessage);
        }

        return normalized;
    }
}

/// <summary>Ingredient catalog and price history of the current user (US-10, US-12).</summary>
public sealed class IngredientService(
    IIngredientRepository ingredients,
    IFlourRepository flours,
    ReferenceGuard reference,
    ICurrentUser currentUser)
{
    /// <summary>Message for a missing ingredient.</summary>
    public const string NotFoundMessage = "The ingredient was not found.";

    /// <summary>Message for a duplicate name.</summary>
    public const string NameTakenMessage = "An ingredient with this name already exists.";

    /// <summary>Message when an ingredient used in recipes is deleted.</summary>
    public const string InUseMessage = "The ingredient is used in recipes and cannot be deleted.";

    /// <summary>Message for a second price on the same date.</summary>
    public const string PriceDateTakenMessage = "A price for this date already exists.";

    /// <summary>Message for a missing price.</summary>
    public const string PriceNotFoundMessage = "The price was not found.";

    /// <summary>Page of ingredients with today's price.</summary>
    public Task<PagedResult<IngredientDto>> ListAsync(IngredientListQuery query, CancellationToken cancellationToken) =>
        ingredients.ListAsync(currentUser.Id, query, cancellationToken);

    /// <summary>One ingredient.</summary>
    public async Task<IngredientDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ingredients.GetAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Creates an ingredient.</summary>
    public async Task<IngredientDto> CreateAsync(IngredientRequest request, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(request, null, cancellationToken);
        var id = await ingredients.CreateAsync(currentUser.Id, normalized, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Replaces an ingredient.</summary>
    public async Task<IngredientDto> UpdateAsync(Guid id, IngredientRequest request, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(request, id, cancellationToken);
        if (!await ingredients.UpdateAsync(currentUser.Id, id, normalized, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Deletes an ingredient that no recipe uses (a used one is protected by the database as well).</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await ingredients.IsUsedAsync(currentUser.Id, id, cancellationToken))
        {
            throw new ConflictException(InUseMessage);
        }

        if (!await ingredients.DeleteAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    /// <summary>Price history, newest first, with the change against the previous price.</summary>
    public async Task<IReadOnlyList<PriceDto>> GetPricesAsync(Guid ingredientId, CancellationToken cancellationToken)
    {
        var rows = await ingredients.GetPricesAsync(currentUser.Id, ingredientId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        return PriceHistory.WithChanges(rows);
    }

    /// <summary>Adds a price; older prices are kept (US-12).</summary>
    public async Task<PriceDto> AddPriceAsync(Guid ingredientId, PriceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await ingredients.GetPricesAsync(currentUser.Id, ingredientId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        await reference.EnsureCurrencyAsync(request.Currency, "currency", cancellationToken);
        if (existing.Any(p => p.EffectiveFrom == request.EffectiveFrom))
        {
            throw new ConflictException(PriceDateTakenMessage);
        }

        var id = await ingredients.AddPriceAsync(ingredientId, request, cancellationToken);
        var history = await GetPricesAsync(ingredientId, cancellationToken);
        return history.Single(p => p.Id == id);
    }

    /// <summary>Replaces a price entered by mistake; the date must stay free of another price.</summary>
    public async Task<PriceDto> UpdatePriceAsync(Guid ingredientId, Guid priceId, PriceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await ingredients.GetPricesAsync(currentUser.Id, ingredientId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        if (existing.All(p => p.Id != priceId))
        {
            throw new NotFoundException(PriceNotFoundMessage);
        }

        await reference.EnsureCurrencyAsync(request.Currency, "currency", cancellationToken);
        if (existing.Any(p => p.Id != priceId && p.EffectiveFrom == request.EffectiveFrom))
        {
            throw new ConflictException(PriceDateTakenMessage);
        }

        if (!await ingredients.UpdatePriceAsync(ingredientId, priceId, request, cancellationToken))
        {
            throw new NotFoundException(PriceNotFoundMessage);
        }

        var history = await GetPricesAsync(ingredientId, cancellationToken);
        return history.Single(p => p.Id == priceId);
    }

    /// <summary>Deletes a price; costs on its dates fall back to the previous price.</summary>
    public async Task DeletePriceAsync(Guid ingredientId, Guid priceId, CancellationToken cancellationToken)
    {
        if (await ingredients.GetPricesAsync(currentUser.Id, ingredientId, cancellationToken) is null)
        {
            throw new NotFoundException(NotFoundMessage);
        }

        if (!await ingredients.DeletePriceAsync(ingredientId, priceId, cancellationToken))
        {
            throw new NotFoundException(PriceNotFoundMessage);
        }
    }

    private async Task<IngredientRequest> PrepareAsync(IngredientRequest request, Guid? id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = request with { Name = request.Name.Trim() };
        await reference.EnsureIngredientCategoryAsync(normalized.CategoryCode, "categoryCode", cancellationToken);
        await reference.EnsureUnitAsync(normalized.UnitCode, "unitCode", cancellationToken);
        if (normalized.FlourId is { } flourId && await flours.GetAsync(currentUser.Id, flourId, cancellationToken) is null)
        {
            throw ValidationFailures.For("flourId", FlourService.NotFoundMessage);
        }

        if (await ingredients.NameExistsAsync(currentUser.Id, normalized.Name, id, cancellationToken))
        {
            throw new ConflictException(NameTakenMessage);
        }

        return normalized;
    }
}

/// <summary>Price history calculations done in C# (presentation of stored rows).</summary>
public static class PriceHistory
{
    private const int UnitPriceDigits = 6;
    private const int PercentDigits = 1;

    /// <summary>
    /// Orders prices newest first and adds the change of the unit price against the next older price in the same
    /// currency (null for the oldest price, after a currency change, or when the older price is zero).
    /// </summary>
    public static IReadOnlyList<PriceDto> WithChanges(IEnumerable<PriceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var ordered = rows.OrderByDescending(r => r.EffectiveFrom).ToList();
        var result = new List<PriceDto>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var row = ordered[i];
            var unitPrice = UnitPrice(row);
            decimal? change = null;
            if (i + 1 < ordered.Count && ordered[i + 1] is var older && older.Currency == row.Currency)
            {
                var olderUnit = UnitPrice(older);
                if (olderUnit != 0)
                {
                    change = Math.Round((unitPrice - olderUnit) / olderUnit * 100, PercentDigits, MidpointRounding.AwayFromZero);
                }
            }

            result.Add(new PriceDto(row.Id, row.Price, row.PerQuantity, unitPrice, row.Currency, row.EffectiveFrom, change));
        }

        return result;
    }

    private static decimal UnitPrice(PriceRow row) =>
        Math.Round(row.Price / row.PerQuantity, UnitPriceDigits, MidpointRounding.AwayFromZero);
}
