using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Warnings;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>Reads <c>v_data_quality_issues</c> for one owner.</summary>
public sealed class WarningsReader(OneLoafDbContext db) : IWarningsReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<WarningDto>> GetAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<WarningDto>($"""
            SELECT issue_code AS "IssueCode", entity_type AS "EntityType", entity_id AS "EntityId", details AS "Details"
            FROM v_data_quality_issues
            WHERE owner_id = {ownerId}
            ORDER BY issue_code, details
            """).ToListAsync(cancellationToken);
}
