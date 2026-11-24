using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> b)
    {
        b.ToTable("batches", t =>
        {
            t.HasComment("E5, US-20, US-26. Production batches with plan, status and cost snapshot.");
            t.HasCheckConstraint("ck_batches_ready_window", "planned_ready_to >= planned_ready_from");
            t.HasCheckConstraint("ck_batches_total_cost", "total_cost >= 0");
            t.HasCheckConstraint("ck_batches_ready_after_start", "planned_ready_from >= planned_start_at");
            t.HasCheckConstraint("ck_batches_completed_after_start", "completed_at >= actual_start_at");
            t.HasCheckConstraint("ck_batches_done_has_completed_at", "status <> 'done' OR completed_at IS NOT NULL");
            t.HasCheckConstraint("ck_batches_name_not_blank", "length(btrim(name)) > 0");
            t.HasCheckConstraint("ck_batches_ingredient_scale", "ingredient_scale > 0 AND ingredient_scale <= 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TotalCost).HasPrecision(12, 2);
        b.Property(x => x.IngredientScale).HasPrecision(5, 4).HasDefaultValue(1m);
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        b.Property(x => x.Notes).HasMaxLength(4000);
        b.HasIndex(x => new { x.OwnerId, x.PlannedStartAt }); // weekly calendar
        b.HasIndex(x => new { x.OwnerId, x.Status });
        b.HasIndex(x => x.Currency);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.Currency).OnDelete(DeleteBehavior.NoAction);
    }
}

public class BatchItemConfiguration : IEntityTypeConfiguration<BatchItem>
{
    public void Configure(EntityTypeBuilder<BatchItem> b)
    {
        b.ToTable("batch_items", t =>
        {
            t.HasComment("E5, US-17, US-18. Recipe and number of loaves inside a batch.");
            t.HasCheckConstraint("ck_batch_items_loaves", "loaves > 0");
            t.HasCheckConstraint("ck_batch_items_starter_amount_g", "starter_amount_g >= 0");
            t.HasCheckConstraint("ck_batch_items_kneading_cycles", "kneading_cycles > 0");
            t.HasCheckConstraint("ck_batch_items_starter_reminder_minutes", "starter_reminder_minutes >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.StarterAmountG).HasPrecision(10, 1);
        b.HasIndex(x => new { x.BatchId, x.SortOrder });
        b.HasIndex(x => x.RecipeId);
        b.HasIndex(x => x.StarterId);
        b.HasOne(x => x.Batch).WithMany(x => x.Items).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Starter).WithMany().HasForeignKey(x => x.StarterId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BatchStageConfiguration : IEntityTypeConfiguration<BatchStage>
{
    public void Configure(EntityTypeBuilder<BatchStage> b)
    {
        b.ToTable("batch_stages", t =>
        {
            t.HasComment("E5, US-21, US-23, US-24. Stage log with planned and actual times (KPI-1).");
            t.HasCheckConstraint("ck_batch_stages_planned_order",
                "planned_end_min_at >= planned_start_at AND planned_end_max_at >= planned_end_min_at");
            t.HasCheckConstraint("ck_batch_stages_actual_order", "actual_end_at >= actual_start_at");
            t.HasCheckConstraint("ck_batch_stages_end_needs_start", "actual_end_at IS NULL OR actual_start_at IS NOT NULL");
            t.HasCheckConstraint("ck_batch_stages_done_has_times", "status <> 'done' OR (actual_start_at IS NOT NULL AND actual_end_at IS NOT NULL)");
            t.HasCheckConstraint("ck_batch_stages_temperature", "actual_temperature_c BETWEEN -30 AND 350");
            t.HasCheckConstraint("ck_batch_stages_extended_minutes", "extended_minutes >= 0");
            t.HasCheckConstraint("ck_batch_stages_planned_minutes", "planned_min_minutes >= 0 AND planned_max_minutes >= planned_min_minutes");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.ActualTemperatureC).HasPrecision(4, 1);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.HasIndex(x => new { x.BatchItemId, x.SortOrder });
        b.HasIndex(x => x.RecipeStageId);
        b.HasIndex(x => x.StageTypeCode);
        b.HasOne<StageType>().WithMany().HasForeignKey(x => x.StageTypeCode).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.BatchItem).WithMany(x => x.Stages).HasForeignKey(x => x.BatchItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.RecipeStage).WithMany().HasForeignKey(x => x.RecipeStageId).OnDelete(DeleteBehavior.SetNull);
    }
}
