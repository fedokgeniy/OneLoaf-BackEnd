using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRecipeRepository"/>. The effective content of a recipe is read through
/// <c>fn_recipe_effective_header/ingredients/stages</c>; writes go through the entities in one SaveChanges.
/// </summary>
public sealed class RecipeRepository(OneLoafDbContext db) : IRecipeRepository
{
    private static readonly Dictionary<string, ProofMode> ProofModes = Enum.GetValues<ProofMode>()
        .ToDictionary(m => ModelConventions.ToSnakeCase(m.ToString()), StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<PagedResult<RecipeSummaryDto>> ListAsync(Guid ownerId, RecipeListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var recipes = db.Recipes.AsNoTracking().Where(r => r.OwnerId == ownerId);
        if (!query.IncludeArchived)
        {
            recipes = recipes.Where(r => !r.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            recipes = recipes.Where(r => EF.Functions.ILike(r.Title, FlourRepository.Like(query.Search)));
        }

        if (query.MinProteinPercent is { } minProtein)
        {
            var matching = db.Database.SqlQuery<Guid>($"""
                SELECT r.id AS "Value"
                FROM recipes r
                WHERE r.owner_id = {ownerId}
                  AND EXISTS (SELECT 1
                              FROM fn_recipe_effective_ingredients(r.id) e
                              JOIN ingredients i ON i.id = e.ingredient_id
                              JOIN flours f ON f.id = i.flour_id
                              WHERE f.protein_percent >= {minProtein})
                """);
            recipes = recipes.Where(r => matching.Contains(r.Id));
        }

        var total = await recipes.CountAsync(cancellationToken);
        var items = await recipes.OrderBy(r => r.Title).ThenBy(r => r.Id)
            .Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize)
            .Select(r => new RecipeSummaryDto(r.Id, r.Title, r.ParentRecipeId, r.YieldLoaves, r.IsArchived,
                new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero)))
            .ToListAsync(cancellationToken);
        return new PagedResult<RecipeSummaryDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<RecipeDto?> GetEffectiveAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.AsNoTracking().Where(r => r.OwnerId == ownerId && r.Id == id)
            .Select(r => new { r.ParentRecipeId, r.IsArchived, r.CreatedAt, r.UpdatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (recipe is null)
        {
            return null;
        }

        var header = await db.Database.SqlQuery<HeaderRow>($"""
            SELECT title AS "Title", yield_loaves AS "YieldLoaves", description AS "Description",
                   loaf_weight_g AS "LoafWeightG", notes AS "Notes"
            FROM fn_recipe_effective_header({id})
            """).SingleAsync(cancellationToken);

        var ingredients = await db.Database.SqlQuery<IngredientRow>($"""
            SELECT e.line_id AS "LineId", e.origin_recipe_id AS "OriginRecipeId", e.ingredient_id AS "IngredientId",
                   i.name AS "IngredientName", i.category_code AS "CategoryCode", i.unit_code AS "UnitCode",
                   e.quantity AS "Quantity", e.is_optional AS "IsOptional", e.sort_order AS "SortOrder", e.note AS "Note",
                   f.protein_percent AS "ProteinPercent"
            FROM fn_recipe_effective_ingredients({id}) e
            JOIN ingredients i ON i.id = e.ingredient_id
            LEFT JOIN flours f ON f.id = i.flour_id
            ORDER BY e.sort_order, e.line_id
            """).ToListAsync(cancellationToken);

        var stages = await db.Database.SqlQuery<StageRow>($"""
            SELECT e.line_id AS "LineId", e.origin_recipe_id AS "OriginRecipeId", e.sort_order AS "SortOrder",
                   e.name AS "Name", e.stage_type_code AS "StageTypeCode",
                   e.min_duration_minutes AS "MinDurationMinutes", e.max_duration_minutes AS "MaxDurationMinutes",
                   e.active_minutes AS "ActiveMinutes", e.proof_mode AS "ProofMode", e.temperature_c AS "TemperatureC",
                   e.uses_oven AS "UsesOven", e.uses_mixer AS "UsesMixer", e.is_optional AS "IsOptional",
                   rs.instructions AS "Instructions"
            FROM fn_recipe_effective_stages({id}) e
            JOIN recipe_stages rs ON rs.id = e.line_id
            ORDER BY e.sort_order, e.line_id
            """).ToListAsync(cancellationToken);

        return new RecipeDto(
            id, recipe.ParentRecipeId, header.Title, header.Description, header.YieldLoaves, header.LoafWeightG,
            header.Notes, recipe.IsArchived,
            ingredients.Select(l => new RecipeIngredientDto(l.LineId, l.IngredientId, l.IngredientName, l.CategoryCode,
                l.UnitCode, l.Quantity, l.IsOptional, l.SortOrder, l.Note, l.ProteinPercent, l.OriginRecipeId != id)).ToList(),
            stages.Select(s => new RecipeStageDto(s.LineId, s.SortOrder, s.Name, s.StageTypeCode, s.MinDurationMinutes,
                s.MaxDurationMinutes, s.ActiveMinutes, s.ProofMode is null ? null : ProofModes[s.ProofMode], s.TemperatureC,
                s.UsesOven, s.UsesMixer, s.IsOptional, s.Instructions, s.OriginRecipeId != id)).ToList(),
            new DateTimeOffset(recipe.CreatedAt, TimeSpan.Zero), new DateTimeOffset(recipe.UpdatedAt, TimeSpan.Zero));
    }

    /// <inheritdoc />
    public async Task<RecipeState?> GetStateAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.AsNoTracking().Where(r => r.OwnerId == ownerId && r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.ParentRecipeId,
                r.YieldLoaves,
                Ingredients = r.Ingredients.Select(l => new OwnIngredientRow(l.Id, l.IngredientId, l.Quantity, l.IsOptional,
                    l.SortOrder, l.Note, l.ReplacesItemId, l.IsRemoved)).ToList(),
                Stages = r.Stages.Select(s => new OwnStageRow(s.Id, s.SortOrder, s.Name, s.StageTypeCode,
                    s.MinDurationMinutes, s.MaxDurationMinutes, s.ActiveMinutes, s.ProofMode, s.TemperatureC, s.UsesOven,
                    s.UsesMixer, s.IsOptional, s.Instructions, s.ReplacesStageId, s.IsRemoved)).ToList(),
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        return recipe is null
            ? null
            : new RecipeState(recipe.Id, recipe.ParentRecipeId, recipe.YieldLoaves, recipe.Ingredients, recipe.Stages);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        db.Recipes.AsNoTracking().AnyAsync(r => r.OwnerId == ownerId && r.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(Guid ownerId, Guid? parentRecipeId, RecipeChangeSet recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var entity = new Recipe { OwnerId = ownerId, ParentRecipeId = parentRecipeId };
        ApplyHeader(entity, recipe.Header);
        db.Recipes.Add(entity);
        Sync(entity, recipe);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(Guid ownerId, Guid id, RecipeChangeSet changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var entity = await db.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Stages)
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.OwnerId == ownerId && r.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        ApplyHeader(entity, changes.Header);
        Sync(entity, changes);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetArchivedAsync(Guid ownerId, Guid id, bool archived, CancellationToken cancellationToken) =>
        await db.Recipes.Where(r => r.OwnerId == ownerId && r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsArchived, archived), cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<bool> IsReferencedAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.Recipes.AsNoTracking().AnyAsync(r => r.OwnerId == ownerId && r.ParentRecipeId == id, cancellationToken)
        || await db.BatchItems.AsNoTracking().AnyAsync(b => b.RecipeId == id, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.Recipes.Where(r => r.OwnerId == ownerId && r.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    private static void ApplyHeader(Recipe entity, RecipeHeaderChange header)
    {
        entity.Title = header.Title;
        entity.YieldLoaves = header.YieldLoaves;
        entity.Description = header.Description;
        entity.LoafWeightG = header.LoafWeightG;
        entity.Notes = header.Notes;
        entity.VariantOverrides = header.VariantOverridesJson;
    }

    /// <summary>Rows with an id are updated, rows without an id inserted, own rows not in the set deleted.</summary>
    private static void Sync(Recipe entity, RecipeChangeSet changes)
    {
        var keepIngredients = changes.Ingredients.Select(r => r.Id).OfType<Guid>().ToHashSet();
        foreach (var stale in entity.Ingredients.Where(l => !keepIngredients.Contains(l.Id)).ToList())
        {
            entity.Ingredients.Remove(stale);
        }

        foreach (var row in changes.Ingredients)
        {
            var line = row.Id is { } rowId ? entity.Ingredients.Single(l => l.Id == rowId) : new RecipeIngredient();
            line.IngredientId = row.IngredientId;
            line.Quantity = row.Quantity;
            line.IsOptional = row.IsOptional;
            line.SortOrder = row.SortOrder;
            line.Note = row.Note;
            line.ReplacesItemId = row.ReplacesItemId;
            line.IsRemoved = row.IsRemoved;
            if (row.Id is null)
            {
                entity.Ingredients.Add(line);
            }
        }

        var keepStages = changes.Stages.Select(r => r.Id).OfType<Guid>().ToHashSet();
        foreach (var stale in entity.Stages.Where(s => !keepStages.Contains(s.Id)).ToList())
        {
            entity.Stages.Remove(stale);
        }

        foreach (var row in changes.Stages)
        {
            var stage = row.Id is { } rowId ? entity.Stages.Single(s => s.Id == rowId) : new RecipeStage();
            stage.SortOrder = row.SortOrder;
            stage.Name = row.Name;
            stage.StageTypeCode = row.StageTypeCode;
            stage.MinDurationMinutes = row.MinDurationMinutes;
            stage.MaxDurationMinutes = row.MaxDurationMinutes;
            stage.ActiveMinutes = row.ActiveMinutes;
            stage.ProofMode = row.ProofMode;
            stage.TemperatureC = row.TemperatureC;
            stage.UsesOven = row.UsesOven;
            stage.UsesMixer = row.UsesMixer;
            stage.IsOptional = row.IsOptional;
            stage.Instructions = row.Instructions;
            stage.ReplacesStageId = row.ReplacesStageId;
            stage.IsRemoved = row.IsRemoved;
            if (row.Id is null)
            {
                entity.Stages.Add(stage);
            }
        }
    }

    private sealed class HeaderRow
    {
        public string Title { get; set; } = string.Empty;

        public int YieldLoaves { get; set; }

        public string? Description { get; set; }

        public decimal? LoafWeightG { get; set; }

        public string? Notes { get; set; }
    }

    private sealed class IngredientRow
    {
        public Guid LineId { get; set; }

        public Guid OriginRecipeId { get; set; }

        public Guid IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public string CategoryCode { get; set; } = string.Empty;

        public string UnitCode { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public bool IsOptional { get; set; }

        public int SortOrder { get; set; }

        public string? Note { get; set; }

        public decimal? ProteinPercent { get; set; }
    }

    private sealed class StageRow
    {
        public Guid LineId { get; set; }

        public Guid OriginRecipeId { get; set; }

        public int SortOrder { get; set; }

        public string Name { get; set; } = string.Empty;

        public string StageTypeCode { get; set; } = string.Empty;

        public int MinDurationMinutes { get; set; }

        public int MaxDurationMinutes { get; set; }

        public int ActiveMinutes { get; set; }

        public string? ProofMode { get; set; }

        public decimal? TemperatureC { get; set; }

        public bool UsesOven { get; set; }

        public bool UsesMixer { get; set; }

        public bool IsOptional { get; set; }

        public string? Instructions { get; set; }
    }
}
