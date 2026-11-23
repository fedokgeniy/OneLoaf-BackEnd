using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Library;

/// <summary>Moderation of the library by administrators (LR-11).</summary>
public sealed class ModerationService(ILibraryRepository library, ICurrentUser currentUser)
{
    private const string ReportNotFoundMessage = "Report not found.";

    /// <summary>Reports by status.</summary>
    public Task<PagedResult<ReportDto>> ListReportsAsync(ReportListQuery query, CancellationToken cancellationToken) =>
        library.ListReportsAsync(query, cancellationToken);

    /// <summary>Removes the reported content or rejects the report; 409 when it was already decided.</summary>
    public async Task ResolveAsync(Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var report = await library.GetReportStateAsync(reportId, cancellationToken) ?? throw new NotFoundException(ReportNotFoundMessage);
        if (report.Status != ReportStatus.Open)
        {
            throw new ConflictException("The report was already decided.");
        }

        if (request.Action == ModerationAction.Remove)
        {
            if (report.CommentId is { } commentId)
            {
                await library.RemoveCommentAsync(commentId, cancellationToken);
            }
            else if (report.PublishedRecipeId is { } publicationId)
            {
                await library.SetStatusAsync(publicationId, PublicationStatus.Removed, request.Reason.Trim(), cancellationToken);
            }
        }

        var status = request.Action == ModerationAction.Remove ? ReportStatus.Resolved : ReportStatus.Rejected;
        if (!await library.CloseReportAsync(reportId, currentUser.Id, status, cancellationToken))
        {
            throw new ConflictException("The report was already decided.");
        }
    }

    /// <summary>Removes a publication without a report.</summary>
    public async Task RemovePublicationAsync(Guid id, RemovePublicationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = await library.GetStateAsync(id, cancellationToken) ?? throw new NotFoundException(LibraryService.NotFoundMessage);
        await library.SetStatusAsync(id, PublicationStatus.Removed, request.Reason.Trim(), cancellationToken);
    }
}
