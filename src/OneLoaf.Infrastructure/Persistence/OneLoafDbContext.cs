using Microsoft.EntityFrameworkCore;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence;

public class OneLoafDbContext : DbContext
{
    public OneLoafDbContext(DbContextOptions<OneLoafDbContext> options) : base(options) { }

    // Reference data
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<StageType> StageTypes => Set<StageType>();
    public DbSet<IngredientCategory> IngredientCategories => Set<IngredientCategory>();
    public DbSet<FlourType> FlourTypes => Set<FlourType>();

    // E1 Accounts
    public DbSet<User> Users => Set<User>();
    public DbSet<KitchenSettings> KitchenSettings => Set<KitchenSettings>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // E3 Catalog and prices
    public DbSet<Flour> Flours => Set<Flour>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientPrice> IngredientPrices => Set<IngredientPrice>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    // E2 Recipes
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStage> RecipeStages => Set<RecipeStage>();

    // E5 Production
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchItem> BatchItems => Set<BatchItem>();
    public DbSet<BatchStage> BatchStages => Set<BatchStage>();

    // E6 Starters and reminders
    public DbSet<Starter> Starters => Set<Starter>();
    public DbSet<StarterFeeding> StarterFeedings => Set<StarterFeeding>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    // E7 History
    public DbSet<BakeRecord> BakeRecords => Set<BakeRecord>();
    public DbSet<BakeRecordItem> BakeRecordItems => Set<BakeRecordItem>();
    public DbSet<BakeRecordPhoto> BakeRecordPhotos => Set<BakeRecordPhoto>();

    // E8 Assistant
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // E9 Shared library
    public DbSet<PublishedRecipe> PublishedRecipes => Set<PublishedRecipe>();
    public DbSet<RecipeComment> RecipeComments => Set<RecipeComment>();
    public DbSet<RecipeReaction> RecipeReactions => Set<RecipeReaction>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OneLoafDbContext).Assembly);

        // Must run after the configurations: it names columns, keys and indexes
        // and adds CHECK constraints for enum columns.
        modelBuilder.ApplyOneLoafConventions();
    }
}
