using Microsoft.Extensions.Logging;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Starters;

/// <summary>Sourdough starters and feedings of the current user (US-27, US-28, US-30).</summary>
public sealed partial class StarterService(
    IStarterRepository starters,
    IFlourRepository flours,
    IIngredientRepository ingredients,
    IEventPublisher events,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    ILogger<StarterService> logger)
{
    /// <summary>Message for a missing starter.</summary>
    public const string NotFoundMessage = "The starter was not found.";

    /// <summary>Message for a duplicate name.</summary>
    public const string NameTakenMessage = "A starter with this name already exists.";

    /// <summary>Category of the ingredient a starter is.</summary>
    public const string StarterCategory = "starter";

    /// <summary>Page of starters with their schedule.</summary>
    public Task<PagedResult<StarterDto>> ListAsync(PageQuery query, CancellationToken cancellationToken) =>
        starters.ListAsync(currentUser.Id, query, cancellationToken);

    /// <summary>One starter.</summary>
    public async Task<StarterDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await starters.GetAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Creates a starter (US-27).</summary>
    public async Task<StarterDto> CreateAsync(StarterRequest request, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(request, null, cancellationToken);
        var id = await starters.CreateAsync(currentUser.Id, normalized, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Replaces a starter.</summary>
    public async Task<StarterDto> UpdateAsync(Guid id, StarterRequest request, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(request, id, cancellationToken);
        if (!await starters.UpdateAsync(currentUser.Id, id, normalized, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Deletes a starter; batches keep their items without it.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await starters.DeleteAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    /// <summary>Feeding log, newest first (US-28).</summary>
    public async Task<PagedResult<FeedingDto>> GetFeedingsAsync(Guid id, PageQuery query, CancellationToken cancellationToken) =>
        await starters.GetFeedingsAsync(currentUser.Id, id, query, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Records a feeding; the database computes the next feeding and the peak (US-28, US-30). Publishes <c>starter.fed</c>.</summary>
    public async Task<FeedingDto> AddFeedingAsync(Guid id, FeedingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await starters.ExistsAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        // A starter can be fed with another flour or water than usual; the price of the starter follows what it was fed.
        await EnsureIngredientAsync(request.FlourIngredientId, "flourIngredientId", cancellationToken);
        await EnsureIngredientAsync(request.WaterIngredientId, "waterIngredientId", cancellationToken);
        var feeding = await starters.AddFeedingAsync(id, request, cancellationToken);
        LogFed(logger, id, currentUser.Id);
        await events.PublishAsync(new StarterFed(id, currentUser.Id, feeding.FedAt, feeding.NextFeedingAt, feeding.ExpectedPeakAt,
            timeProvider.GetUtcNow()), cancellationToken);
        return feeding;
    }

    private async Task<StarterRequest> PrepareAsync(StarterRequest request, Guid? id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = request with { Name = request.Name.Trim() };
        if (normalized.FlourIngredientId is { } flourIngredientId)
        {
            // The flour of the starter is an ingredient; its flour details (type, protein) come with it.
            var flourIngredient = await ingredients.GetAsync(currentUser.Id, flourIngredientId, cancellationToken)
                ?? throw ValidationFailures.For("flourIngredientId", IngredientService.NotFoundMessage);
            normalized = normalized with { FlourId = flourIngredient.FlourId };
        }
        else if (normalized.FlourId is { } flourId && await flours.GetAsync(currentUser.Id, flourId, cancellationToken) is null)
        {
            throw ValidationFailures.For("flourId", FlourService.NotFoundMessage);
        }

        // The starter is an ingredient with the same name: the name must be free among the ingredients as well,
        // except for the starter's own ingredient and a starter ingredient that is waiting to be adopted.
        var sameName = await ingredients.FindByNamesAsync(currentUser.Id, [normalized.Name], cancellationToken);
        if (sameName.TryGetValue(normalized.Name, out var ingredientId)
            && await ingredients.GetAsync(currentUser.Id, ingredientId, cancellationToken) is { } taken
            && (taken.StarterId is null ? taken.CategoryCode != StarterCategory : taken.StarterId != id))
        {
            throw new ConflictException(IngredientService.NameTakenMessage);
        }

        await EnsureIngredientAsync(normalized.WaterIngredientId, "waterIngredientId", cancellationToken);
        if (await starters.NameExistsAsync(currentUser.Id, normalized.Name, id, cancellationToken))
        {
            throw new ConflictException(NameTakenMessage);
        }

        return normalized;
    }

    private async Task EnsureIngredientAsync(Guid? ingredientId, string field, CancellationToken cancellationToken)
    {
        if (ingredientId is { } id && (await ingredients.GetOwnedIdsAsync(currentUser.Id, [id], cancellationToken)).Count == 0)
        {
            throw ValidationFailures.For(field, IngredientService.NotFoundMessage);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starter {StarterId} fed by {UserId}")]
    private static partial void LogFed(ILogger logger, Guid starterId, Guid userId);
}
