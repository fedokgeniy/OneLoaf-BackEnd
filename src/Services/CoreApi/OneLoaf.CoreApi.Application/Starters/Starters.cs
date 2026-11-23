using FluentValidation;
using Microsoft.Extensions.Logging;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

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
public sealed record StarterRequest(
    string Name, decimal HydrationPercent, FeedingMode FeedingMode, int FeedingIntervalHours, Guid? FlourId = null,
    decimal? RiseHours = null, bool IsActive = true, string? Notes = null);

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
public sealed record StarterDto(
    Guid Id, string Name, Guid? FlourId, decimal HydrationPercent, FeedingMode FeedingMode, int FeedingIntervalHours,
    decimal? RiseHours, bool IsActive, string? Notes, DateTimeOffset? LastFedAt, DateTimeOffset? NextFeedingAt,
    DateTimeOffset? ExpectedPeakAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

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

/// <summary>Data access for starters and feedings; scoped to the owner.</summary>
public interface IStarterRepository
{
    /// <summary>Page of starters ordered by name.</summary>
    Task<PagedResult<StarterDto>> ListAsync(Guid ownerId, PageQuery query, CancellationToken cancellationToken);

    /// <summary>The starter, or null.</summary>
    Task<StarterDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when the starter exists for the owner.</summary>
    Task<bool> ExistsAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when the name is taken by another starter of the user.</summary>
    Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Creates a starter; returns its id.</summary>
    Task<Guid> CreateAsync(Guid ownerId, StarterRequest starter, CancellationToken cancellationToken);

    /// <summary>Replaces a starter; false when not found.</summary>
    Task<bool> UpdateAsync(Guid ownerId, Guid id, StarterRequest starter, CancellationToken cancellationToken);

    /// <summary>Deletes a starter; false when not found.</summary>
    Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Feedings newest first; null when the starter is not found.</summary>
    Task<PagedResult<FeedingDto>?> GetFeedingsAsync(Guid ownerId, Guid starterId, PageQuery query, CancellationToken cancellationToken);

    /// <summary>Inserts a feeding (the database computes the schedule and replaces the reminder) and returns it.</summary>
    Task<FeedingDto> AddFeedingAsync(Guid starterId, FeedingRequest feeding, CancellationToken cancellationToken);
}

/// <summary>Limits of starter requests.</summary>
public static class StarterLimits
{
    /// <summary><c>starters.name</c>.</summary>
    public const int NameMaxLength = 100;

    /// <summary><c>starters.notes</c>.</summary>
    public const int NotesMaxLength = 2000;

    /// <summary><c>starter_feedings.note</c>.</summary>
    public const int FeedingNoteMaxLength = 1000;

    /// <summary>Largest weight that fits <c>numeric(8,1)</c>.</summary>
    public const decimal MaxGrams = 9_999_999m;

    /// <summary>Longest feeding interval, hours (four weeks).</summary>
    public const int MaxIntervalHours = 24 * 28;
}

/// <summary>Validates <see cref="StarterRequest"/>.</summary>
public sealed class StarterRequestValidator : AbstractValidator<StarterRequest>
{
    /// <summary>Creates the rules.</summary>
    public StarterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).MaximumLength(StarterLimits.NameMaxLength);
        RuleFor(x => x.HydrationPercent).GreaterThan(0).LessThan(10_000);
        RuleFor(x => x.FeedingMode).IsInEnum();
        RuleFor(x => x.FeedingIntervalHours).InclusiveBetween(1, StarterLimits.MaxIntervalHours);
        RuleFor(x => x.RiseHours).GreaterThan(0).LessThan(1000);
        RuleFor(x => x.FlourId).NotEqual(Guid.Empty);
        RuleFor(x => x.Notes).MaximumLength(StarterLimits.NotesMaxLength);
    }
}

/// <summary>Validates <see cref="FeedingRequest"/>.</summary>
public sealed class FeedingRequestValidator : AbstractValidator<FeedingRequest>
{
    /// <summary>Creates the rules.</summary>
    public FeedingRequestValidator()
    {
        RuleFor(x => x.FedAt).NotEqual(default(DateTimeOffset));
        RuleFor(x => x.FlourG).GreaterThanOrEqualTo(0).LessThan(StarterLimits.MaxGrams);
        RuleFor(x => x.WaterG).GreaterThanOrEqualTo(0).LessThan(StarterLimits.MaxGrams);
        RuleFor(x => x.StarterKeptG).GreaterThanOrEqualTo(0).LessThan(StarterLimits.MaxGrams);
        RuleFor(x => x.TemperatureC).InclusiveBetween(-30, 100);
        RuleFor(x => x.Note).MaximumLength(StarterLimits.FeedingNoteMaxLength);
    }
}

/// <summary>Validates <see cref="PageQuery"/>.</summary>
public sealed class PageQueryValidator : AbstractValidator<PageQuery>
{
    /// <summary>Creates the rules.</summary>
    public PageQueryValidator() => this.AddPagingRules(x => x.Page, x => x.PageSize);
}

/// <summary>Sourdough starters and feedings of the current user (US-27, US-28, US-30).</summary>
public sealed partial class StarterService(
    IStarterRepository starters,
    IFlourRepository flours,
    IEventPublisher events,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    ILogger<StarterService> logger)
{
    /// <summary>Message for a missing starter.</summary>
    public const string NotFoundMessage = "The starter was not found.";

    /// <summary>Message for a duplicate name.</summary>
    public const string NameTakenMessage = "A starter with this name already exists.";

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
        if (normalized.FlourId is { } flourId && await flours.GetAsync(currentUser.Id, flourId, cancellationToken) is null)
        {
            throw ValidationFailures.For("flourId", FlourService.NotFoundMessage);
        }

        if (await starters.NameExistsAsync(currentUser.Id, normalized.Name, id, cancellationToken))
        {
            throw new ConflictException(NameTakenMessage);
        }

        return normalized;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starter {StarterId} fed by {UserId}")]
    private static partial void LogFed(ILogger logger, Guid starterId, Guid userId);
}
