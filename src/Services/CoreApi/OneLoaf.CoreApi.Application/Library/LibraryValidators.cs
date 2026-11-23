using FluentValidation;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Text;

namespace OneLoaf.CoreApi.Application.Library;

/// <summary>Limits of library requests (database column sizes).</summary>
public static class LibraryLimits
{
    /// <summary>Longest comment.</summary>
    public const int MaxCommentLength = 2000;

    /// <summary>Longest report reason.</summary>
    public const int MaxReportReasonLength = 1000;

    /// <summary>Longest removal reason.</summary>
    public const int MaxRemovedReasonLength = 500;

    /// <summary>Shortest reason.</summary>
    public const int MinReasonLength = 3;

    /// <summary>Longest search text.</summary>
    public const int MaxSearchLength = 200;
}

/// <summary>Validates <see cref="LibraryListQuery"/>.</summary>
public sealed class LibraryListQueryValidator : AbstractValidator<LibraryListQuery>
{
    /// <summary>Creates the rules.</summary>
    public LibraryListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(LibraryLimits.MaxSearchLength);
    }
}

/// <summary>Validates <see cref="CommentListQuery"/>.</summary>
public sealed class CommentListQueryValidator : AbstractValidator<CommentListQuery>
{
    /// <summary>Creates the rules.</summary>
    public CommentListQueryValidator() => this.AddPagingRules(x => x.Page, x => x.PageSize);
}

/// <summary>Validates <see cref="ReportListQuery"/>.</summary>
public sealed class ReportListQueryValidator : AbstractValidator<ReportListQuery>
{
    /// <summary>Creates the rules.</summary>
    public ReportListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Status).Must(WireEnumNames.IsNullOrDefined<ReportStatus>)
            .WithMessage($"status must be one of: {WireEnumNames.Values<ReportStatus>()}.");
    }
}

/// <summary>Validates <see cref="CommentRequest"/>.</summary>
public sealed class CommentRequestValidator : AbstractValidator<CommentRequest>
{
    /// <summary>Creates the rules.</summary>
    public CommentRequestValidator() =>
        RuleFor(x => x.Body).NotEmpty().Must(b => !string.IsNullOrWhiteSpace(b)).MaximumLength(LibraryLimits.MaxCommentLength);
}

/// <summary>Validates <see cref="ReactionRequest"/>.</summary>
public sealed class ReactionRequestValidator : AbstractValidator<ReactionRequest>
{
    /// <summary>Creates the rules.</summary>
    public ReactionRequestValidator() => RuleFor(x => x.Reaction).IsInEnum();
}

/// <summary>Validates <see cref="ReportRequest"/>.</summary>
public sealed class ReportRequestValidator : AbstractValidator<ReportRequest>
{
    /// <summary>Creates the rules.</summary>
    public ReportRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().Must(r => r.Trim().Length >= LibraryLimits.MinReasonLength)
            .WithMessage($"reason must have at least {LibraryLimits.MinReasonLength} characters.")
            .MaximumLength(LibraryLimits.MaxReportReasonLength);
}

/// <summary>Validates <see cref="ResolveReportRequest"/>.</summary>
public sealed class ResolveReportRequestValidator : AbstractValidator<ResolveReportRequest>
{
    /// <summary>Creates the rules.</summary>
    public ResolveReportRequestValidator()
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().Must(r => r.Trim().Length >= LibraryLimits.MinReasonLength)
            .WithMessage($"reason must have at least {LibraryLimits.MinReasonLength} characters.")
            .MaximumLength(LibraryLimits.MaxRemovedReasonLength);
    }
}

/// <summary>Validates <see cref="RemovePublicationRequest"/>.</summary>
public sealed class RemovePublicationRequestValidator : AbstractValidator<RemovePublicationRequest>
{
    /// <summary>Creates the rules.</summary>
    public RemovePublicationRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().Must(r => r.Trim().Length >= LibraryLimits.MinReasonLength)
            .WithMessage($"reason must have at least {LibraryLimits.MinReasonLength} characters.")
            .MaximumLength(LibraryLimits.MaxRemovedReasonLength);
}
