using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Catalog;

/// <summary>
/// Stock of the ingredients of the current user: purchases, corrections and the ledger. Batches and starter feedings
/// take from the stock in the database (<c>sp_apply_batch_stock</c>, <c>trg_starter_feedings_stock</c>).
/// </summary>
public sealed class StockService(
    IIngredientRepository ingredients, ReferenceGuard reference, TimeProvider timeProvider, ICurrentUser currentUser)
{
    /// <summary>Message when a correction does not change the stock.</summary>
    public const string NothingToAdjustMessage = "The stock already equals this quantity.";

    /// <summary>Page of the ledger, newest first.</summary>
    public async Task<PagedResult<StockMovementDto>> ListAsync(
        Guid ingredientId, StockMovementListQuery query, CancellationToken cancellationToken) =>
        await ingredients.ListMovementsAsync(currentUser.Id, ingredientId, query, cancellationToken)
        ?? throw new NotFoundException(IngredientService.NotFoundMessage);

    /// <summary>
    /// Stores a purchase: the stock grows by packs x pack size, the price of the purchase date becomes the amount paid
    /// for that quantity, and the stock of the ingredient is kept from now on.
    /// </summary>
    public async Task<StockMovementDto> AddPurchaseAsync(Guid ingredientId, PurchaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureOwnedAsync(ingredientId, cancellationToken);
        await reference.EnsureCurrencyAsync(request.Currency, "currency", cancellationToken);
        var normalized = request with { Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim() };
        var id = await ingredients.AddPurchaseAsync(ingredientId, normalized, cancellationToken);
        return await ingredients.GetMovementAsync(id, cancellationToken);
    }

    /// <summary>Sets the stock to the counted quantity by writing the difference to the ledger.</summary>
    public async Task<StockMovementDto> AdjustAsync(Guid ingredientId, StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureOwnedAsync(ingredientId, cancellationToken);
        var difference = request.ActualQuantity - await ingredients.GetStockAsync(ingredientId, cancellationToken);
        if (difference == 0)
        {
            throw ValidationFailures.For("actualQuantity", NothingToAdjustMessage);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var id = await ingredients.AddAdjustmentAsync(ingredientId, difference, today, request.Note.Trim(), cancellationToken);
        return await ingredients.GetMovementAsync(id, cancellationToken);
    }

    private async Task EnsureOwnedAsync(Guid ingredientId, CancellationToken cancellationToken)
    {
        if (await ingredients.GetAsync(currentUser.Id, ingredientId, cancellationToken) is null)
        {
            throw new NotFoundException(IngredientService.NotFoundMessage);
        }
    }
}
