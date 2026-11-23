using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Export;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IExportReader"/>.</summary>
public sealed class ExportReader(OneLoafDbContext db) : IExportReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RecipeExportRow>> GetRecipesAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await db.Recipes.AsNoTracking()
            .Where(r => r.OwnerId == ownerId)
            .OrderBy(r => r.Title)
            .Select(r => new
            {
                r.Title, IsVariant = r.ParentRecipeId != null, r.YieldLoaves, r.LoafWeightG,
                Ingredients = db.RecipeIngredients.Count(i => i.RecipeId == r.Id),
                Stages = db.RecipeStages.Count(s => s.RecipeId == r.Id),
                r.IsArchived, r.CreatedAt, r.UpdatedAt,
            })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new RecipeExportRow(r.Title, r.IsVariant, r.YieldLoaves, r.LoafWeightG, r.Ingredients, r.Stages, r.IsArchived,
            new DateTimeOffset(r.CreatedAt, TimeSpan.Zero), new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero))).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BakeRecordExportRow>> GetBakeRecordsAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await db.BakeRecords.AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.BakedOn).ThenByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.BakedOn, b.TotalLoaves, b.TotalCost, b.Currency, b.Rating, b.Comment,
                Items = b.Items.Select(i => new { Title = i.RecipeTitle, i.Loaves }).ToList(),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(b => new BakeRecordExportRow(b.BakedOn, string.Join("; ", b.Items.Select(i => $"{i.Title} x{i.Loaves}")),
            b.TotalLoaves, b.TotalCost, b.Currency, b.Rating, b.Comment)).ToList();
    }
}
