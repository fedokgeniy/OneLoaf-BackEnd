using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class FlourConfiguration : IEntityTypeConfiguration<Flour>
{
    public void Configure(EntityTypeBuilder<Flour> b)
    {
        b.ToTable("flours", t =>
        {
            t.HasComment("E3, US-11. Flour catalog of a user.");
            t.HasCheckConstraint("ck_flours_protein_percent", "protein_percent BETWEEN 0 AND 100");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ProteinPercent).HasPrecision(4, 1);
        b.Property(x => x.Grind).HasMaxLength(50);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique();
        b.HasIndex(x => x.FlourTypeCode);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<FlourType>().WithMany().HasForeignKey(x => x.FlourTypeCode).OnDelete(DeleteBehavior.NoAction);
    }
}

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> b)
    {
        b.ToTable("ingredients", t =>
        {
            t.HasComment("E3, US-10. Ingredient catalog of a user, with a unit of measure.");
            t.HasCheckConstraint("ck_ingredients_reorder_level", "reorder_level >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TrackStock).HasDefaultValue(false);
        b.Property(x => x.ReorderLevel).HasPrecision(12, 3);
        b.Property(x => x.PhotoKey).HasMaxLength(200);
        b.Property(x => x.PhotoContentType).HasMaxLength(50);
        b.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique();
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.FlourId);
        b.HasIndex(x => x.CategoryCode);
        b.HasIndex(x => x.UnitCode);
        b.HasOne(x => x.Flour).WithMany().HasForeignKey(x => x.FlourId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<IngredientCategory>().WithMany().HasForeignKey(x => x.CategoryCode).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitCode).OnDelete(DeleteBehavior.NoAction);
    }
}

public class IngredientPriceConfiguration : IEntityTypeConfiguration<IngredientPrice>
{
    public void Configure(EntityTypeBuilder<IngredientPrice> b)
    {
        b.ToTable("ingredient_prices", t =>
        {
            t.HasComment("E3, US-12, US-15. Price history; the price on date D is the row with the latest effective_from <= D.");
            t.HasCheckConstraint("ck_ingredient_prices_price", "price >= 0");
            t.HasCheckConstraint("ck_ingredient_prices_per_quantity", "per_quantity > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Price).HasPrecision(12, 4);
        b.Property(x => x.PerQuantity).HasPrecision(12, 3);
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        // Unique (ingredient, date) also serves the "price as of date" lookup.
        b.HasIndex(x => new { x.IngredientId, x.EffectiveFrom }).IsUnique();
        b.HasIndex(x => x.Currency);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.Currency).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Ingredient).WithMany(x => x.Prices).HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movements", t =>
        {
            t.HasComment("E3. Stock ledger of ingredients: purchases, batch consumption and returns, starter feedings, corrections. The stock is the sum of quantity.");
            t.HasCheckConstraint("ck_stock_movements_quantity_not_zero", "quantity <> 0");
            t.HasCheckConstraint("ck_stock_movements_quantity_sign",
                "(movement_type IN ('purchase', 'batch_return', 'starter_ripened') AND quantity > 0) OR (movement_type IN ('batch_consumption', 'starter_feeding', 'starter_refed') AND quantity < 0) OR movement_type = 'adjustment'");
            t.HasCheckConstraint("ck_stock_movements_purchase_fields",
                "movement_type <> 'purchase' OR (pack_count > 0 AND pack_size > 0 AND total_cost >= 0 AND currency IS NOT NULL)");
            t.HasCheckConstraint("ck_stock_movements_adjustment_note", "movement_type <> 'adjustment' OR length(btrim(note)) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasPrecision(12, 3);
        b.Property(x => x.PackSize).HasPrecision(12, 3);
        b.Property(x => x.TotalCost).HasPrecision(12, 4);
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.IngredientId, x.OccurredOn }); // ledger of an ingredient and its balance
        b.HasIndex(x => x.BatchId);
        b.HasIndex(x => x.FeedingId);
        b.HasIndex(x => x.Currency);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.Currency).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Feeding).WithMany().HasForeignKey(x => x.FeedingId).OnDelete(DeleteBehavior.SetNull);
    }
}
