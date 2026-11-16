namespace OneLoaf.Domain.Entities;

/// <summary>
/// E9, US-39, US-40. Published snapshot of a recipe in the shared library.
/// The snapshot (ingredients and stages) is stored as JSON and does not change
/// when the author edits the private working copy.
/// </summary>
public class PublishedRecipe
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? SourceRecipeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>JSON: {"yieldLoaves":1,"ingredients":[...],"stages":[...]}.</summary>
    public string Snapshot { get; set; } = "{}";
    public PublicationStatus Status { get; set; } = PublicationStatus.Published;
    public DateTime PublishedAt { get; set; }
    public DateTime? UnpublishedAt { get; set; }
    public string? RemovedReason { get; set; }

    public User Author { get; set; } = null!;
    public Recipe? SourceRecipe { get; set; }
    public ICollection<RecipeComment> Comments { get; set; } = new List<RecipeComment>();
    public ICollection<RecipeReaction> Reactions { get; set; } = new List<RecipeReaction>();
}

/// <summary>E9, US-41.</summary>
public class RecipeComment
{
    public Guid Id { get; set; }
    public Guid PublishedRecipeId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsRemoved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }

    public PublishedRecipe PublishedRecipe { get; set; } = null!;
    public User Author { get; set; } = null!;
}

/// <summary>E9, US-41. One reaction per user per published recipe.</summary>
public class RecipeReaction
{
    public Guid PublishedRecipeId { get; set; }
    public Guid UserId { get; set; }
    public ReactionType Reaction { get; set; }
    public DateTime CreatedAt { get; set; }

    public PublishedRecipe PublishedRecipe { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>LR-11. User report on a published recipe or a comment; handled by an administrator.</summary>
public class ContentReport
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public Guid? PublishedRecipeId { get; set; }
    public Guid? CommentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public Guid? ResolvedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public User Reporter { get; set; } = null!;
    public PublishedRecipe? PublishedRecipe { get; set; }
    public RecipeComment? Comment { get; set; }
    public User? ResolvedBy { get; set; }
}
