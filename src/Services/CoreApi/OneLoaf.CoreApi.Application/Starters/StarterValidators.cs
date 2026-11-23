using FluentValidation;
using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.Starters;

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
