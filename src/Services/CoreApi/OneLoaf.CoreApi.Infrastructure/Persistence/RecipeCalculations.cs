using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>
/// Gateway to the calculation functions of the database. Every query is parameterized (interpolated
/// <c>SqlQuery</c>); instants are passed in UTC as <c>timestamptz</c> requires.
/// </summary>
public sealed class RecipeCalculations(OneLoafDbContext db) : IRecipeCalculations
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CostLine>> GetCostAsync(Guid recipeId, int loaves, DateOnly asOf, CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQuery<CostRow>($"""
            SELECT line_no AS "LineNo", line_type AS "LineType", ingredient_id AS "IngredientId",
                   description AS "Description", quantity AS "Quantity", unit_code AS "UnitCode",
                   unit_price AS "UnitPrice", amount AS "Amount", currency AS "Currency", is_complete AS "IsComplete"
            FROM fn_recipe_cost({recipeId}, {loaves}, {asOf})
            ORDER BY line_no
            """).ToListAsync(cancellationToken);
        return rows.Select(r => new CostLine(r.LineNo, r.LineType, r.IngredientId, r.Description, r.Quantity, r.UnitCode,
            r.UnitPrice, r.Amount, r.Currency.Trim(), r.IsComplete)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimelineStageDto>> GetTimelineAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken)
    {
        var utcStart = start.ToUniversalTime();
        var rows = await db.Database.SqlQuery<TimelineRow>($"""
            SELECT recipe_stage_id AS "LineId", sort_order AS "SortOrder", name AS "Name",
                   stage_type_code AS "StageTypeCode", min_minutes AS "MinMinutes", max_minutes AS "MaxMinutes",
                   planned_start_at AS "PlannedStartAt", planned_end_min_at AS "PlannedEndMinAt",
                   planned_end_max_at AS "PlannedEndMaxAt"
            FROM fn_recipe_timeline({recipeId}, {utcStart})
            """).ToListAsync(cancellationToken);
        return rows.Select(r => new TimelineStageDto(r.LineId, r.SortOrder, r.Name, r.StageTypeCode, r.MinMinutes,
            r.MaxMinutes, ToOffset(r.PlannedStartAt, start), ToOffset(r.PlannedEndMinAt, start),
            ToOffset(r.PlannedEndMaxAt, start))).ToList();
    }

    /// <inheritdoc />
    public async Task<TimeWindow> GetReadyWindowAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken)
    {
        var utcStart = start.ToUniversalTime();
        var row = await db.Database.SqlQuery<WindowRow>($"""
            SELECT ready_from AS "From", ready_to AS "To" FROM fn_recipe_ready_window({recipeId}, {utcStart})
            """).SingleAsync(cancellationToken);
        return new TimeWindow(ToOffset(row.From, start), ToOffset(row.To, start));
    }

    /// <inheritdoc />
    public async Task<TimeWindow> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken cancellationToken)
    {
        var utcReady = readyAt.ToUniversalTime();
        var row = await db.Database.SqlQuery<WindowRow>($"""
            SELECT start_from AS "From", start_to AS "To" FROM fn_recipe_start_window({recipeId}, {utcReady})
            """).SingleAsync(cancellationToken);
        return new TimeWindow(ToOffset(row.From, readyAt), ToOffset(row.To, readyAt));
    }

    /// <inheritdoc />
    public Task<ProductionFigures> GetProductionFiguresAsync(Guid recipeId, int loaves, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<FiguresRow>($"""
            SELECT fn_dough_weight_g({recipeId}, {loaves}) AS "DoughWeightG",
                   fn_kneading_cycles({recipeId}, {loaves}) AS "KneadingCycles",
                   fn_starter_amount_g({recipeId}, {loaves}) AS "StarterAmountG"
            """).Select(r => new ProductionFigures(r.DoughWeightG, r.KneadingCycles, r.StarterAmountG))
            .SingleAsync(cancellationToken);

    /// <summary>Returns the instant in the offset of the request, so the client sees its own local times.</summary>
    private static DateTimeOffset ToOffset(DateTimeOffset value, DateTimeOffset like) => value.ToOffset(like.Offset);

    private sealed class CostRow
    {
        public int LineNo { get; set; }

        public string LineType { get; set; } = string.Empty;

        public Guid? IngredientId { get; set; }

        public string Description { get; set; } = string.Empty;

        public decimal? Quantity { get; set; }

        public string? UnitCode { get; set; }

        public decimal? UnitPrice { get; set; }

        public decimal? Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public bool IsComplete { get; set; }
    }

    private sealed class TimelineRow
    {
        public Guid LineId { get; set; }

        public int SortOrder { get; set; }

        public string Name { get; set; } = string.Empty;

        public string StageTypeCode { get; set; } = string.Empty;

        public int MinMinutes { get; set; }

        public int MaxMinutes { get; set; }

        public DateTimeOffset PlannedStartAt { get; set; }

        public DateTimeOffset PlannedEndMinAt { get; set; }

        public DateTimeOffset PlannedEndMaxAt { get; set; }
    }

    private sealed class WindowRow
    {
        public DateTimeOffset From { get; set; }

        public DateTimeOffset To { get; set; }
    }

    private sealed class FiguresRow
    {
        public decimal DoughWeightG { get; set; }

        public int? KneadingCycles { get; set; }

        public decimal StarterAmountG { get; set; }
    }
}
