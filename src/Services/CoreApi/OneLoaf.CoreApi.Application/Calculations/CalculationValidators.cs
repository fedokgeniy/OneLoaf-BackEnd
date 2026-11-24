using FluentValidation;
using OneLoaf.CoreApi.Application.Recipes;

namespace OneLoaf.CoreApi.Application.Calculations;

/// <summary>Validates <see cref="CostQuery"/>.</summary>
public sealed class CostQueryValidator : AbstractValidator<CostQuery>
{
    /// <summary>Creates the rules.</summary>
    public CostQueryValidator() => RuleFor(x => x.Loaves).InclusiveBetween(1, RecipeLimits.MaxLoaves);
}

/// <summary>Validates <see cref="TimelineQuery"/>.</summary>
public sealed class TimelineQueryValidator : AbstractValidator<TimelineQuery>
{
    /// <summary>Creates the rules.</summary>
    public TimelineQueryValidator() => RuleFor(x => x.Start).NotEqual(default(DateTimeOffset));
}

/// <summary>Validates <see cref="StartWindowQuery"/>.</summary>
public sealed class StartWindowQueryValidator : AbstractValidator<StartWindowQuery>
{
    /// <summary>Creates the rules.</summary>
    public StartWindowQueryValidator() => RuleFor(x => x.ReadyAt).NotEqual(default(DateTimeOffset));
}
