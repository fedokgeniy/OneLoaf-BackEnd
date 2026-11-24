namespace OneLoaf.CoreApi.Application.Calculations;

/// <summary>Values of <c>line_type</c> returned by <c>fn_recipe_cost</c>.</summary>
public static class CostLineTypes
{
    /// <summary>Mandatory ingredient.</summary>
    public const string Ingredient = "ingredient";

    /// <summary>Electricity of the oven.</summary>
    public const string Electricity = "electricity";

    /// <summary>Packaging.</summary>
    public const string Packaging = "packaging";

    /// <summary>Own work.</summary>
    public const string Labor = "labor";

    /// <summary>Loss rate.</summary>
    public const string Loss = "loss";

    /// <summary>Total of the rounded lines.</summary>
    public const string Total = "total";
}

/// <summary>A row of <c>fn_recipe_cost</c>.</summary>
/// <param name="LineNo">Line number.</param>
/// <param name="LineType">One of <see cref="CostLineTypes"/>.</param>
/// <param name="IngredientId">Ingredient of an ingredient line.</param>
/// <param name="Description">Description.</param>
/// <param name="Quantity">Quantity.</param>
/// <param name="UnitCode">Unit of the quantity.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Amount">Amount; null when an input is missing.</param>
/// <param name="Currency">Currency of the kitchen.</param>
/// <param name="IsComplete">False when an input is missing.</param>
public sealed record CostLine(
    int LineNo, string LineType, Guid? IngredientId, string Description, decimal? Quantity, string? UnitCode,
    decimal? UnitPrice, decimal? Amount, string Currency, bool IsComplete);

/// <summary>A row of <c>fn_recipe_timeline</c>.</summary>
/// <param name="LineId">Stage line id.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="Name">Name.</param>
/// <param name="StageTypeCode">Stage type.</param>
/// <param name="MinMinutes">Minimum duration.</param>
/// <param name="MaxMinutes">Maximum duration.</param>
/// <param name="PlannedStartAt">Planned start (with the minimum durations of the previous stages).</param>
/// <param name="PlannedEndMinAt">Earliest end.</param>
/// <param name="PlannedEndMaxAt">Latest end.</param>
public sealed record TimelineStageDto(
    Guid LineId, int SortOrder, string Name, string StageTypeCode, int MinMinutes, int MaxMinutes,
    DateTimeOffset PlannedStartAt, DateTimeOffset PlannedEndMinAt, DateTimeOffset PlannedEndMaxAt);

/// <summary>A time window.</summary>
/// <param name="From">Start of the window.</param>
/// <param name="To">End of the window.</param>
public sealed record TimeWindow(DateTimeOffset From, DateTimeOffset To);

/// <summary>Results of <c>fn_dough_weight_g</c>, <c>fn_kneading_cycles</c>, <c>fn_starter_amount_g</c>.</summary>
/// <param name="DoughWeightG">Dough weight, g.</param>
/// <param name="KneadingCycles">Kneading cycles; null when no bowl capacity is set.</param>
/// <param name="StarterAmountG">Starter amount, g.</param>
public sealed record ProductionFigures(decimal DoughWeightG, int? KneadingCycles, decimal StarterAmountG);

/// <summary>Query of <c>GET /recipes/{id}/cost</c>.</summary>
/// <param name="Loaves">Number of loaves, 1-1000.</param>
/// <param name="AsOf">Price date; defaults to today in the kitchen time zone (US-15).</param>
public sealed record CostQuery(int Loaves = 1, DateOnly? AsOf = null);

/// <summary>Query of <c>GET /recipes/{id}/timeline</c>.</summary>
/// <param name="Start">Start of the first stage, ISO 8601 with offset.</param>
public sealed record TimelineQuery(DateTimeOffset Start);

/// <summary>Query of <c>GET /recipes/{id}/start-window</c>.</summary>
/// <param name="ReadyAt">Desired ready time, ISO 8601 with offset.</param>
public sealed record StartWindowQuery(DateTimeOffset ReadyAt);

/// <summary>Cost line in the response.</summary>
/// <param name="LineNo">Line number.</param>
/// <param name="Type">One of <see cref="CostLineTypes"/>.</param>
/// <param name="IngredientId">Ingredient of an ingredient line.</param>
/// <param name="Description">Description.</param>
/// <param name="Quantity">Quantity.</param>
/// <param name="Unit">Unit.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Amount">Amount; null when an input is missing.</param>
/// <param name="IsComplete">False when an input is missing.</param>
public sealed record CostLineDto(
    int LineNo, string Type, Guid? IngredientId, string Description, decimal? Quantity, string? Unit, decimal? UnitPrice,
    decimal? Amount, bool IsComplete);

/// <summary>Ingredient without a usable price on the date (UC-01 alternative flow A4).</summary>
/// <param name="IngredientId">Ingredient.</param>
/// <param name="Name">Name.</param>
public sealed record MissingPriceDto(Guid IngredientId, string Name);

/// <summary>Cost breakdown of N loaves (US-14, US-15, US-47 - US-49).</summary>
/// <param name="RecipeId">Recipe.</param>
/// <param name="Loaves">Loaves.</param>
/// <param name="AsOf">Price date.</param>
/// <param name="Currency">Currency of the kitchen.</param>
/// <param name="IsComplete">False when some input is missing.</param>
/// <param name="Lines">Lines in order, ending with the total.</param>
/// <param name="Total">Total amount (sum of the rounded lines).</param>
/// <param name="MissingPrices">Ingredients without a usable price.</param>
public sealed record CostBreakdownDto(
    Guid RecipeId, int Loaves, DateOnly AsOf, string Currency, bool IsComplete, IReadOnlyList<CostLineDto> Lines,
    decimal? Total, IReadOnlyList<MissingPriceDto> MissingPrices);

/// <summary>Timeline of a recipe for a start time (US-16).</summary>
/// <param name="RecipeId">Recipe.</param>
/// <param name="Start">Start.</param>
/// <param name="Stages">Stages with planned times.</param>
/// <param name="ReadyFrom">Earliest ready time.</param>
/// <param name="ReadyTo">Latest ready time.</param>
public sealed record TimelineDto(
    Guid RecipeId, DateTimeOffset Start, IReadOnlyList<TimelineStageDto> Stages, DateTimeOffset ReadyFrom, DateTimeOffset ReadyTo);

/// <summary>Start window for a desired ready time (US-52).</summary>
/// <param name="RecipeId">Recipe.</param>
/// <param name="ReadyAt">Desired ready time.</param>
/// <param name="StartFrom">Earliest start (with the longest durations).</param>
/// <param name="StartTo">Latest start (with the shortest durations).</param>
public sealed record StartWindowDto(Guid RecipeId, DateTimeOffset ReadyAt, DateTimeOffset StartFrom, DateTimeOffset StartTo);

/// <summary>Production figures for N loaves (US-17, US-18).</summary>
/// <param name="RecipeId">Recipe.</param>
/// <param name="Loaves">Loaves.</param>
/// <param name="DoughWeightG">Dough weight, g.</param>
/// <param name="KneadingCycles">Kneading cycles; null when no bowl capacity is set.</param>
/// <param name="StarterAmountG">Starter amount, g.</param>
public sealed record ProductionFiguresDto(Guid RecipeId, int Loaves, decimal DoughWeightG, int? KneadingCycles, decimal StarterAmountG);
