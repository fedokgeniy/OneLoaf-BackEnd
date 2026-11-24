using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.Starters;

/// <summary>Body of <c>POST/PUT /starters</c> (US-27).</summary>
/// <param name="Name">Name, unique within the user.</param>
/// <param name="HydrationPercent">Hydration, %.</param>
/// <param name="FeedingMode">regular, weekly, fridge.</param>
/// <param name="FeedingIntervalHours">Hours between feedings.</param>
/// <param name="FlourId">Flour of the user's catalog.</param>
/// <param name="RiseHours">Hours from feeding to peak (US-30).</param>
/// <param name="IsActive">Reminders are created for active starters only.</param>
/// <param name="Notes">Notes.</param>
/// <param name="FlourIngredientId">Ingredient whose stock a feeding takes the flour from.</param>
/// <param name="WaterIngredientId">Ingredient whose stock a feeding takes the water from.</param>
/// <param name="UsableShare">
/// Share of the fed starter that can go into dough once it is ripe, 0.05-1; the rest stays for the next feeding.
/// Defaults to two thirds.
/// </param>
public sealed record StarterRequest(
    string Name, decimal HydrationPercent, FeedingMode FeedingMode, int FeedingIntervalHours, Guid? FlourId = null,
    decimal? RiseHours = null, bool IsActive = true, string? Notes = null, Guid? FlourIngredientId = null,
    Guid? WaterIngredientId = null, decimal? UsableShare = null);

/// <summary>A starter with its schedule from the last feeding (US-27, US-28, US-30).</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="FlourId">Flour.</param>
/// <param name="HydrationPercent">Hydration, %.</param>
/// <param name="FeedingMode">Mode.</param>
/// <param name="FeedingIntervalHours">Hours between feedings.</param>
/// <param name="RiseHours">Hours to peak.</param>
/// <param name="IsActive">Active.</param>
/// <param name="Notes">Notes.</param>
/// <param name="LastFedAt">Last feeding.</param>
/// <param name="NextFeedingAt">Next feeding from the last feeding.</param>
/// <param name="ExpectedPeakAt">Expected peak from the last feeding.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="UpdatedAt">Last change.</param>
/// <param name="FlourIngredientId">Ingredient whose stock a feeding takes the flour from.</param>
/// <param name="WaterIngredientId">Ingredient whose stock a feeding takes the water from.</param>
/// <param name="AmountG">Starter after the last feeding, g: flour + water + starter kept.</param>
/// <param name="IngredientId">The starter as an ingredient of the catalog: recipes use it, its stock is the ripe starter.</param>
/// <param name="UsableShare">Share of the fed starter that becomes stock when it is ripe.</param>
/// <param name="UsableG">Ripe starter in stock now, g; 0 while the starter is ripening.</param>
/// <param name="SuggestedKeptG">Starter left for the next feeding, g: the reserve plus what was not used.</param>
public sealed record StarterDto(
    Guid Id, string Name, Guid? FlourId, decimal HydrationPercent, FeedingMode FeedingMode, int FeedingIntervalHours,
    decimal? RiseHours, bool IsActive, string? Notes, DateTimeOffset? LastFedAt, DateTimeOffset? NextFeedingAt,
    DateTimeOffset? ExpectedPeakAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? FlourIngredientId = null,
    Guid? WaterIngredientId = null, decimal? AmountG = null, Guid? IngredientId = null, decimal UsableShare = StarterLimits.DefaultUsableShare,
    decimal? UsableG = null, decimal? SuggestedKeptG = null);

/// <summary>Body of <c>POST /starters/{id}/feedings</c> (US-28).</summary>
/// <param name="FedAt">Feeding time.</param>
/// <param name="FlourG">Flour, g.</param>
/// <param name="WaterG">Water, g.</param>
/// <param name="StarterKeptG">Starter kept, g.</param>
/// <param name="TemperatureC">Room temperature.</param>
/// <param name="Note">Note.</param>
public sealed record FeedingRequest(
    DateTimeOffset FedAt, decimal FlourG, decimal WaterG, decimal? StarterKeptG = null, decimal? TemperatureC = null,
    string? Note = null);

/// <summary>A feeding with the schedule computed by the database.</summary>
/// <param name="Id">Id.</param>
/// <param name="FedAt">Feeding time.</param>
/// <param name="FlourG">Flour, g.</param>
/// <param name="WaterG">Water, g.</param>
/// <param name="StarterKeptG">Starter kept, g.</param>
/// <param name="TemperatureC">Room temperature.</param>
/// <param name="NextFeedingAt">Next feeding (US-28).</param>
/// <param name="ExpectedPeakAt">Expected peak (US-30).</param>
/// <param name="Note">Note.</param>
public sealed record FeedingDto(
    Guid Id, DateTimeOffset FedAt, decimal FlourG, decimal WaterG, decimal? StarterKeptG, decimal? TemperatureC,
    DateTimeOffset? NextFeedingAt, DateTimeOffset? ExpectedPeakAt, string? Note);

/// <summary>Query of <c>GET /starters</c> and <c>GET /starters/{id}/feedings</c>.</summary>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record PageQuery(int Page = 1, int PageSize = Paging.DefaultPageSize);
