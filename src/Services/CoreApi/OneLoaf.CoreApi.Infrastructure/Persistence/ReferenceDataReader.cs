using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IReferenceDataReader"/>.</summary>
public sealed class ReferenceDataReader(OneLoafDbContext db) : IReferenceDataReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UnitDto>> GetUnitsAsync(CancellationToken cancellationToken) =>
        await db.Units.AsNoTracking().OrderBy(u => u.SortOrder)
            .Select(u => new UnitDto(u.Code, u.Name, u.Dimension, u.FactorToBase)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken) =>
        await db.Currencies.AsNoTracking().OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Code, c.Name, c.MinorUnits)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StageTypeDto>> GetStageTypesAsync(CancellationToken cancellationToken) =>
        await db.StageTypes.AsNoTracking().OrderBy(s => s.SortOrder)
            .Select(s => new StageTypeDto(s.Code, s.Name, s.IsFermentation, s.DefaultUsesOven, s.DefaultUsesMixer))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IngredientCategoryDto>> GetIngredientCategoriesAsync(CancellationToken cancellationToken) =>
        await db.IngredientCategories.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new IngredientCategoryDto(c.Code, c.Name, c.IsFlour, c.IsLiquid)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FlourTypeDto>> GetFlourTypesAsync(CancellationToken cancellationToken) =>
        await db.FlourTypes.AsNoTracking().OrderBy(f => f.SortOrder)
            .Select(f => new FlourTypeDto(f.Code, f.Name)).ToListAsync(cancellationToken);
}
