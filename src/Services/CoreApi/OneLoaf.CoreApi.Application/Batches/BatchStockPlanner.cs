namespace OneLoaf.CoreApi.Application.Batches;

/// <summary>
/// Turns the stock rows of a batch plan (<c>fn_batch_stock_check</c>) into the answer for the baker: what is missing
/// and the two ways out, fewer loaves or the same loaves from what is in stock.
/// </summary>
public static class BatchStockPlanner
{
    private const int ScaleDigits = 4;

    /// <summary>Smallest ingredient scale a batch may have.</summary>
    public const decimal MinScale = 0.01m;

    /// <summary>
    /// The stock check, or null when the batch uses no ingredient with kept stock.
    /// <paramref name="singleItemLoaves"/> is the number of loaves when the batch has exactly one item.
    /// </summary>
    public static BatchStockDto? Evaluate(IReadOnlyList<BatchStockRow> rows, int? singleItemLoaves)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return null;
        }

        var shortages = rows.Where(r => r.Needed > r.Available)
            .Select(r => new StockShortageDto(r.IngredientId, r.Name, r.UnitCode, r.Needed, r.Available, r.Needed - r.Available))
            .ToList();
        if (shortages.Count == 0)
        {
            return new BatchStockDto([], null, null);
        }

        // The scarcest ingredient decides: the share of the full recipe the stock covers.
        var ratio = rows.Where(r => r.NeededFull > 0).Min(r => Math.Max(r.Available, 0m) / r.NeededFull);
        var scale = Math.Min(1m, Math.Floor(ratio * 10_000m) / 10_000m);
        var maxLoaves = singleItemLoaves is { } loaves ? (int)Math.Floor(loaves * ratio) : 0;
        return new BatchStockDto(
            shortages,
            maxLoaves >= 1 ? maxLoaves : null,
            scale >= MinScale ? Math.Round(scale, ScaleDigits) : null);
    }
}
