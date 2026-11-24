using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.Catalog;

/// <summary>A row of the stock ledger of an ingredient.</summary>
/// <param name="Id">Id.</param>
/// <param name="Type">purchase, batch_consumption, batch_return, starter_feeding or adjustment.</param>
/// <param name="Quantity">Signed quantity in the ingredient unit: positive adds to the stock.</param>
/// <param name="OccurredOn">Date of the movement.</param>
/// <param name="BatchId">Batch that took or returned the quantity; null after the batch is deleted.</param>
/// <param name="BatchName">Name of that batch.</param>
/// <param name="PackCount">Purchase: number of packs.</param>
/// <param name="PackSize">Purchase: content of one pack in the ingredient unit.</param>
/// <param name="TotalCost">Purchase: amount paid.</param>
/// <param name="Currency">Purchase: currency.</param>
/// <param name="Note">Note; always present for a correction.</param>
/// <param name="CreatedAt">When the row was written.</param>
public sealed record StockMovementDto(
    Guid Id, StockMovementType Type, decimal Quantity, DateOnly OccurredOn, Guid? BatchId, string? BatchName, int? PackCount,
    decimal? PackSize, decimal? TotalCost, string? Currency, string? Note, DateTimeOffset CreatedAt);

/// <summary>Query of <c>GET /ingredients/{id}/stock-movements</c>.</summary>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record StockMovementListQuery(int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>
/// Body of <c>POST /ingredients/{id}/records</c>: what was bought and what it cost. The record sets the price of its
/// date; unless it is price only, it also adds packs x pack size to the stock.
/// </summary>
/// <param name="PackCount">Number of packs, 1-10000.</param>
/// <param name="PackSize">Content of one pack in the ingredient unit (1000 for a 1 kg bag of an ingredient kept in g).</param>
/// <param name="TotalCost">Amount paid for all packs, at least 0.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="Date">Date of the purchase; the price applies from this date.</param>
/// <param name="Note">Note, for example the shop.</param>
/// <param name="PriceOnly">True records only the price: the stock does not change and is not switched on.</param>
public sealed record IngredientRecordRequest(
    int PackCount, decimal PackSize, decimal TotalCost, string Currency, DateOnly Date, string? Note = null, bool PriceOnly = false);

/// <summary>Body of <c>POST /ingredients/{id}/stock-adjustments</c>: the quantity that is really on the shelf.</summary>
/// <param name="ActualQuantity">Counted stock in the ingredient unit, at least 0.</param>
/// <param name="Note">Why the stock is corrected.</param>
public sealed record StockAdjustmentRequest(decimal ActualQuantity, string Note);
