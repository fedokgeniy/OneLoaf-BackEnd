using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Warnings;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>Reads <c>v_data_quality_issues</c> and the low stock of <c>v_low_stock</c> for one owner.</summary>
public sealed class WarningsReader(OneLoafDbContext db) : IWarningsReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<WarningDto>> GetAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<WarningDto>($"""
            SELECT w.issue_code AS "IssueCode", w.entity_type AS "EntityType", w.entity_id AS "EntityId", w.details AS "Details"
            FROM (SELECT owner_id, issue_code, entity_type, entity_id, details
                  FROM v_data_quality_issues
                  UNION ALL
                  SELECT owner_id, 'low_stock', 'ingredient', ingredient_id,
                         format('Low stock of "%s": %s %s left', name, trim_scale(stock), unit_code)
                  FROM v_low_stock) w
            WHERE w.owner_id = {ownerId}
            ORDER BY w.issue_code, w.details
            """).ToListAsync(cancellationToken);
}
