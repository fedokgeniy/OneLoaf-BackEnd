using FluentValidation;
using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.History;

/// <summary>Limits of history requests.</summary>
public static class BakeRecordLimits
{
    /// <summary><c>bake_records.comment</c>.</summary>
    public const int CommentMaxLength = 4000;

    /// <summary>Longest analysis text.</summary>
    public const int AnalysisMaxLength = 20_000;
}

/// <summary>Validates <see cref="UpdateBakeRecordRequest"/>.</summary>
public sealed class UpdateBakeRecordRequestValidator : AbstractValidator<UpdateBakeRecordRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdateBakeRecordRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.Comment).MaximumLength(BakeRecordLimits.CommentMaxLength);
    }
}

/// <summary>Validates <see cref="SaveAnalysisRequest"/>.</summary>
public sealed class SaveAnalysisRequestValidator : AbstractValidator<SaveAnalysisRequest>
{
    /// <summary>Creates the rules.</summary>
    public SaveAnalysisRequestValidator() =>
        RuleFor(x => x.Analysis).NotEmpty().Must(a => !string.IsNullOrWhiteSpace(a)).MaximumLength(BakeRecordLimits.AnalysisMaxLength);
}

/// <summary>Validates <see cref="BakeRecordListQuery"/>.</summary>
public sealed class BakeRecordListQueryValidator : AbstractValidator<BakeRecordListQuery>
{
    /// <summary>Creates the rules.</summary>
    public BakeRecordListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.MinRating).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From.HasValue && x.To.HasValue);
    }
}
