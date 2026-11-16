using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.ToTable("units", t =>
        {
            t.HasComment("Reference. Units of measure and conversion to the base unit of their dimension.");
            t.HasCheckConstraint("ck_units_code_format", "code ~ '^[a-z]{1,16}$'");
            t.HasCheckConstraint("ck_units_dimension", "dimension IN ('mass', 'volume', 'count')");
            t.HasCheckConstraint("ck_units_factor_to_base", "factor_to_base > 0");
        });
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(16);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.Property(x => x.Dimension).HasMaxLength(16).IsRequired();
        b.Property(x => x.FactorToBase).HasPrecision(12, 6);
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(ReferenceData.Units);
    }
}

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> b)
    {
        b.ToTable("currencies", t =>
        {
            t.HasComment("Reference. ISO 4217 currencies allowed for prices and costs.");
            t.HasCheckConstraint("ck_currencies_code_format", "code ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("ck_currencies_minor_units", "minor_units BETWEEN 0 AND 4");
        });
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(3).IsFixedLength();
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(ReferenceData.Currencies);
    }
}

public class StageTypeConfiguration : IEntityTypeConfiguration<StageType>
{
    public void Configure(EntityTypeBuilder<StageType> b)
    {
        b.ToTable("stage_types", t =>
        {
            t.HasComment("Reference. Types of production stages with default equipment use.");
            t.HasCheckConstraint("ck_stage_types_code_format", "code ~ '^[a-z_]{1,32}$'");
        });
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(32);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(ReferenceData.StageTypes);
    }
}

public class IngredientCategoryConfiguration : IEntityTypeConfiguration<IngredientCategory>
{
    public void Configure(EntityTypeBuilder<IngredientCategory> b)
    {
        b.ToTable("ingredient_categories", t =>
        {
            t.HasComment("Reference. Ingredient categories; is_flour / is_liquid drive baker's percentages and hydration.");
            t.HasCheckConstraint("ck_ingredient_categories_code_format", "code ~ '^[a-z_]{1,32}$'");
            t.HasCheckConstraint("ck_ingredient_categories_flour_not_liquid", "NOT (is_flour AND is_liquid)");
        });
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(32);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(ReferenceData.IngredientCategories);
    }
}

public class FlourTypeConfiguration : IEntityTypeConfiguration<FlourType>
{
    public void Configure(EntityTypeBuilder<FlourType> b)
    {
        b.ToTable("flour_types", t =>
        {
            t.HasComment("Reference. Grain types of flour.");
            t.HasCheckConstraint("ck_flour_types_code_format", "code ~ '^[a-z_]{1,32}$'");
        });
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(32);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(ReferenceData.FlourTypes);
    }
}
