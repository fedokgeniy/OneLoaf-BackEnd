using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class BakeRecordConfiguration : IEntityTypeConfiguration<BakeRecord>
{
    public void Configure(EntityTypeBuilder<BakeRecord> b)
    {
        b.ToTable("bake_records", t =>
        {
            t.HasComment("E7, US-31, US-32, US-36. Baking history: one record per finished batch.");
            t.HasCheckConstraint("ck_bake_records_rating", "rating BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_bake_records_total_loaves", "total_loaves >= 0");
            t.HasCheckConstraint("ck_bake_records_total_cost", "total_cost >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.TotalCost).HasPrecision(12, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        b.Property(x => x.Comment).HasMaxLength(4000);
        b.HasIndex(x => x.BatchId).IsUnique();
        b.HasIndex(x => new { x.OwnerId, x.BakedOn });
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Currency);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.Currency).OnDelete(DeleteBehavior.NoAction);
    }
}

public class BakeRecordItemConfiguration : IEntityTypeConfiguration<BakeRecordItem>
{
    public void Configure(EntityTypeBuilder<BakeRecordItem> b)
    {
        b.ToTable("bake_record_items", t =>
        {
            t.HasComment("E7, US-31. Snapshot of what was baked (recipe title, loaves, cost).");
            t.HasCheckConstraint("ck_bake_record_items_loaves", "loaves > 0");
            t.HasCheckConstraint("ck_bake_record_items_cost", "cost >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.RecipeTitle).HasMaxLength(200).IsRequired();
        b.Property(x => x.Cost).HasPrecision(12, 2);
        b.HasIndex(x => x.BakeRecordId);
        b.HasIndex(x => x.RecipeId);
        b.HasOne(x => x.BakeRecord).WithMany(x => x.Items).HasForeignKey(x => x.BakeRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BakeRecordPhotoConfiguration : IEntityTypeConfiguration<BakeRecordPhoto>
{
    public void Configure(EntityTypeBuilder<BakeRecordPhoto> b)
    {
        b.ToTable("bake_record_photos", t => t.HasComment("E7, US-51. Crumb photos; files live in object storage, the row keeps the key."));
        b.HasKey(x => x.Id);
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.BakeRecordId);
        b.HasOne(x => x.BakeRecord).WithMany(x => x.Photos).HasForeignKey(x => x.BakeRecordId).OnDelete(DeleteBehavior.Cascade);
    }
}
