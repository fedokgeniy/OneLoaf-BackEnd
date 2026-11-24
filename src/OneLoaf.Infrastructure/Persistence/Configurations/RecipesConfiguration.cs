using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> b)
    {
        b.ToTable("recipes", t =>
        {
            t.HasComment("E2, US-05-09. Recipes and variants; a variant stores only its differences from the parent.");
            t.HasCheckConstraint("ck_recipes_yield_loaves", "yield_loaves > 0");
            t.HasCheckConstraint("ck_recipes_loaf_weight_g", "loaf_weight_g > 0");
            t.HasCheckConstraint("ck_recipes_not_own_parent", "parent_recipe_id IS NULL OR parent_recipe_id <> id");
            t.HasCheckConstraint("ck_recipes_overrides_only_for_variants", "variant_overrides IS NULL OR parent_recipe_id IS NOT NULL");
            // A variant takes description, loaf weight and notes from its parent; changes go to variant_overrides.
            t.HasCheckConstraint("ck_recipes_variant_inherits_scalars",
                "parent_recipe_id IS NULL OR (description IS NULL AND loaf_weight_g IS NULL AND notes IS NULL)");
            t.HasCheckConstraint("ck_recipes_title_not_blank", "length(btrim(title)) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.LoafWeightG).HasPrecision(8, 1);
        b.Property(x => x.VariantOverrides).HasColumnType("jsonb");
        b.Property(x => x.Notes).HasMaxLength(4000);
        b.Property(x => x.PhotoKey).HasMaxLength(200);
        b.Property(x => x.PhotoContentType).HasMaxLength(50);
        b.HasIndex(x => new { x.OwnerId, x.Title });
        b.HasIndex(x => x.ParentRecipeId);
        b.HasIndex(x => x.CopiedFromPublishedRecipeId);

        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        // NO ACTION (checked at the end of the statement): a parent with variants cannot be deleted on its own,
        // but deleting the whole account removes both.
        b.HasOne(x => x.ParentRecipe).WithMany(x => x.Variants).HasForeignKey(x => x.ParentRecipeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<PublishedRecipe>().WithMany().HasForeignKey(x => x.CopiedFromPublishedRecipeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> b)
    {
        b.ToTable("recipe_ingredients", t =>
        {
            t.HasComment("E2. Ingredient lines of a recipe; in a variant, lines replace (replaces_item_id), remove (is_removed) or add parent lines.");
            t.HasCheckConstraint("ck_recipe_ingredients_quantity", "quantity >= 0");
            t.HasCheckConstraint("ck_recipe_ingredients_removed_needs_target", "NOT is_removed OR replaces_item_id IS NOT NULL");
            t.HasCheckConstraint("ck_recipe_ingredients_sort_order", "sort_order >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasPrecision(12, 3);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.RecipeId, x.SortOrder });
        b.HasIndex(x => x.IngredientId);
        b.HasIndex(x => x.ReplacesItemId);
        // A variant changes a parent line at most once.
        b.HasIndex(x => new { x.RecipeId, x.ReplacesItemId }, "ux_recipe_ingredients_recipe_id_replaces_item_id")
            .IsUnique().HasFilter("replaces_item_id IS NOT NULL");

        b.HasOne(x => x.Recipe).WithMany(x => x.Ingredients).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ReplacesItem).WithMany().HasForeignKey(x => x.ReplacesItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeStageConfiguration : IEntityTypeConfiguration<RecipeStage>
{
    public void Configure(EntityTypeBuilder<RecipeStage> b)
    {
        b.ToTable("recipe_stages", t =>
        {
            t.HasComment("E2, US-05, US-09, US-16. Ordered stages with a min-max duration.");
            t.HasCheckConstraint("ck_recipe_stages_min_duration", "min_duration_minutes >= 0");
            t.HasCheckConstraint("ck_recipe_stages_max_duration", "max_duration_minutes >= min_duration_minutes");
            t.HasCheckConstraint("ck_recipe_stages_active_minutes", "active_minutes >= 0");
            t.HasCheckConstraint("ck_recipe_stages_temperature_c", "temperature_c BETWEEN -30 AND 350");
            t.HasCheckConstraint("ck_recipe_stages_removed_needs_target", "NOT is_removed OR replaces_stage_id IS NOT NULL");
            t.HasCheckConstraint("ck_recipe_stages_active_within_max", "active_minutes <= max_duration_minutes");
            t.HasCheckConstraint("ck_recipe_stages_sort_order", "sort_order >= 0");
            t.HasCheckConstraint("ck_recipe_stages_name_not_blank", "length(btrim(name)) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.TemperatureC).HasPrecision(4, 1);
        b.Property(x => x.Instructions).HasMaxLength(4000);
        b.HasIndex(x => new { x.RecipeId, x.SortOrder });
        b.HasIndex(x => x.ReplacesStageId);
        b.HasIndex(x => new { x.RecipeId, x.ReplacesStageId }, "ux_recipe_stages_recipe_id_replaces_stage_id")
            .IsUnique().HasFilter("replaces_stage_id IS NOT NULL");

        b.HasIndex(x => x.StageTypeCode);
        b.HasOne<StageType>().WithMany().HasForeignKey(x => x.StageTypeCode).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Recipe).WithMany(x => x.Stages).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ReplacesStage).WithMany().HasForeignKey(x => x.ReplacesStageId).OnDelete(DeleteBehavior.Cascade);
    }
}
