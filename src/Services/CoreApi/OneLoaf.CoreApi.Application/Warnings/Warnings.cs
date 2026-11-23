using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Warnings;

/// <summary>A data quality issue of the user's workspace (missing price, empty recipe, overdue batch, ...).</summary>
/// <param name="IssueCode">Kind of issue, for example <c>missing_price</c>.</param>
/// <param name="EntityType">Kind of entity: ingredient, recipe, batch, starter.</param>
/// <param name="EntityId">Id of the entity.</param>
/// <param name="Details">Human-readable explanation.</param>
public sealed record WarningDto(string IssueCode, string EntityType, Guid EntityId, string Details);

/// <summary>Reads <c>v_data_quality_issues</c>.</summary>
public interface IWarningsReader
{
    /// <summary>Issues of the owner.</summary>
    Task<IReadOnlyList<WarningDto>> GetAsync(Guid ownerId, CancellationToken cancellationToken);
}

/// <summary>Warnings of the current user (for example: incomplete cost because of a missing price).</summary>
public sealed class WarningsService(IWarningsReader reader, ICurrentUser currentUser)
{
    /// <summary>Issues of the current user.</summary>
    public Task<IReadOnlyList<WarningDto>> GetAsync(CancellationToken cancellationToken) =>
        reader.GetAsync(currentUser.Id, cancellationToken);
}
