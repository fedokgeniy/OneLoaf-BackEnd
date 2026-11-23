namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Recipe in a list (<c>GET /api/v1/recipes</c>).</summary>
public sealed record RecipeSummaryDto(
    Guid Id,
    string Title,
    Guid? ParentRecipeId,
    int YieldLoaves,
    bool IsArchived,
    DateTimeOffset UpdatedAt);
