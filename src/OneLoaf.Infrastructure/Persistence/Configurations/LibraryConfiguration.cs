using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class PublishedRecipeConfiguration : IEntityTypeConfiguration<PublishedRecipe>
{
    public void Configure(EntityTypeBuilder<PublishedRecipe> b)
    {
        b.ToTable("published_recipes", t =>
        {
            t.HasComment("E9, US-39, US-40. Immutable published snapshot of a recipe (ingredients and stages in JSON).");
            t.HasCheckConstraint("ck_published_recipes_removed_has_reason", "status <> 'removed' OR removed_reason IS NOT NULL");
            t.HasCheckConstraint("ck_published_recipes_unpublished_at", "status <> 'unpublished' OR unpublished_at IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Snapshot).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.RemovedReason).HasMaxLength(500);
        b.HasIndex(x => new { x.Status, x.PublishedAt }); // public catalog
        b.HasIndex(x => x.Title);
        b.HasIndex(x => x.AuthorId);
        b.HasIndex(x => x.SourceRecipeId);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.SourceRecipe).WithMany().HasForeignKey(x => x.SourceRecipeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RecipeCommentConfiguration : IEntityTypeConfiguration<RecipeComment>
{
    public void Configure(EntityTypeBuilder<RecipeComment> b)
    {
        b.ToTable("recipe_comments", t =>
        {
            t.HasComment("E9, US-41. Comments on published recipes.");
            t.HasCheckConstraint("ck_recipe_comments_body_not_empty", "length(body) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.PublishedRecipeId, x.CreatedAt });
        b.HasIndex(x => x.AuthorId);
        b.HasOne(x => x.PublishedRecipe).WithMany(x => x.Comments).HasForeignKey(x => x.PublishedRecipeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeReactionConfiguration : IEntityTypeConfiguration<RecipeReaction>
{
    public void Configure(EntityTypeBuilder<RecipeReaction> b)
    {
        b.ToTable("recipe_reactions", t => t.HasComment("E9, US-41. One reaction per user per published recipe."));
        b.HasKey(x => new { x.PublishedRecipeId, x.UserId });
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.PublishedRecipe).WithMany(x => x.Reactions).HasForeignKey(x => x.PublishedRecipeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> b)
    {
        b.ToTable("content_reports", t =>
        {
            t.HasComment("LR-11. Reports on published recipes or comments, handled by an administrator.");
            t.HasCheckConstraint("ck_content_reports_one_target", "num_nonnulls(published_recipe_id, comment_id) = 1");
            t.HasCheckConstraint("ck_content_reports_resolution",
                "(status = 'open' AND resolved_at IS NULL AND resolved_by_id IS NULL) OR (status <> 'open' AND resolved_at IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ReporterId);
        b.HasIndex(x => x.PublishedRecipeId);
        b.HasIndex(x => x.CommentId);
        b.HasIndex(x => x.ResolvedById);
        b.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.PublishedRecipe).WithMany().HasForeignKey(x => x.PublishedRecipeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Comment).WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.SetNull);
    }
}
