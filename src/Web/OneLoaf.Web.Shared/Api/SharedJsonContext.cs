using System.Text.Json;
using System.Text.Json.Serialization;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Shared.Api;

/// <summary>Source-generated JSON metadata of the contracts in the shared layer (trimming-safe).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ProblemDetailsDto))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisterResponse))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(RecipeSummaryDto))]
[JsonSerializable(typeof(PagedResult<RecipeSummaryDto>))]
[JsonSerializable(typeof(RecipeDto))]
[JsonSerializable(typeof(ScaledRecipeDto))]
[JsonSerializable(typeof(CostBreakdownDto))]
[JsonSerializable(typeof(ProductionFiguresDto))]
[JsonSerializable(typeof(TimelineDto))]
[JsonSerializable(typeof(RecipeRequest))]
[JsonSerializable(typeof(CreateVariantRequest))]
[JsonSerializable(typeof(RecipeComparisonDto))]
[JsonSerializable(typeof(IReadOnlyList<UnitDto>))]
[JsonSerializable(typeof(IReadOnlyList<StageTypeDto>))]
[JsonSerializable(typeof(IReadOnlyList<CurrencyDto>))]
[JsonSerializable(typeof(IReadOnlyList<IngredientCategoryDto>))]
[JsonSerializable(typeof(IReadOnlyList<FlourTypeDto>))]
[JsonSerializable(typeof(IngredientDto))]
[JsonSerializable(typeof(PagedResult<IngredientDto>))]
[JsonSerializable(typeof(IngredientRequest))]
[JsonSerializable(typeof(IReadOnlyList<PriceDto>))]
[JsonSerializable(typeof(PriceDto))]
[JsonSerializable(typeof(PriceRequest))]
[JsonSerializable(typeof(PagedResult<FlourDto>))]
[JsonSerializable(typeof(IReadOnlyList<WarningDto>))]
[JsonSerializable(typeof(KitchenSettingsDto))]
[JsonSerializable(typeof(KitchenSettingsRequest))]
internal sealed partial class SharedJsonContext : JsonSerializerContext;
