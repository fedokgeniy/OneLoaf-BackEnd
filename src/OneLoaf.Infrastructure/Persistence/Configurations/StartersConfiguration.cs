using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class StarterConfiguration : IEntityTypeConfiguration<Starter>
{
    public void Configure(EntityTypeBuilder<Starter> b)
    {
        b.ToTable("starters", t =>
        {
            t.HasComment("E6, US-27, US-30. Sourdough starters.");
            t.HasCheckConstraint("ck_starters_hydration_percent", "hydration_percent > 0");
            t.HasCheckConstraint("ck_starters_feeding_interval_hours", "feeding_interval_hours > 0");
            t.HasCheckConstraint("ck_starters_rise_hours", "rise_hours > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.HydrationPercent).HasPrecision(5, 1);
        b.Property(x => x.RiseHours).HasPrecision(4, 1);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique();
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.FlourId);
        b.HasOne(x => x.Flour).WithMany().HasForeignKey(x => x.FlourId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.FlourIngredientId);
        b.HasIndex(x => x.WaterIngredientId);
        b.HasOne(x => x.FlourIngredient).WithMany().HasForeignKey(x => x.FlourIngredientId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.WaterIngredient).WithMany().HasForeignKey(x => x.WaterIngredientId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class StarterFeedingConfiguration : IEntityTypeConfiguration<StarterFeeding>
{
    public void Configure(EntityTypeBuilder<StarterFeeding> b)
    {
        b.ToTable("starter_feedings", t =>
        {
            t.HasComment("E6, US-28. Feeding log of a starter.");
            t.HasCheckConstraint("ck_starter_feedings_flour_g", "flour_g >= 0");
            t.HasCheckConstraint("ck_starter_feedings_water_g", "water_g >= 0");
            t.HasCheckConstraint("ck_starter_feedings_starter_kept_g", "starter_kept_g >= 0");
            t.HasCheckConstraint("ck_starter_feedings_next_after_fed", "next_feeding_at > fed_at");
            t.HasCheckConstraint("ck_starter_feedings_peak_after_fed", "expected_peak_at > fed_at");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.FlourG).HasPrecision(8, 1);
        b.Property(x => x.WaterG).HasPrecision(8, 1);
        b.Property(x => x.StarterKeptG).HasPrecision(8, 1);
        b.Property(x => x.TemperatureC).HasPrecision(4, 1);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasIndex(x => new { x.StarterId, x.FedAt });
        b.HasOne(x => x.Starter).WithMany(x => x.Feedings).HasForeignKey(x => x.StarterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> b)
    {
        b.ToTable("reminders", t =>
        {
            t.HasComment("E6, US-29. Scheduled notifications processed by the Notification service.");
            t.HasCheckConstraint("ck_reminders_has_target", "num_nonnulls(starter_id, batch_item_id, batch_stage_id) >= 1");
            t.HasCheckConstraint("ck_reminders_attempts", "attempts >= 0");
            t.HasCheckConstraint("ck_reminders_sent_has_time", "status <> 'sent' OR sent_at IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Message).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.Status, x.DueAt }); // queue polling
        b.HasIndex(x => x.OwnerId);
        b.HasIndex(x => x.StarterId);
        // At most one scheduled feeding reminder per starter.
        b.HasIndex(x => x.StarterId, "ux_reminders_starter_id_scheduled_feeding")
            .IsUnique().HasFilter("kind = 'starter_feeding' AND status = 'scheduled'");
        b.HasIndex(x => x.BatchItemId);
        b.HasIndex(x => x.BatchStageId);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Starter).WithMany().HasForeignKey(x => x.StarterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BatchItem).WithMany().HasForeignKey(x => x.BatchItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BatchStage).WithMany().HasForeignKey(x => x.BatchStageId).OnDelete(DeleteBehavior.Cascade);
    }
}
