using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes the three "protective" foreign keys (ON DELETE NO ACTION) deferrable and checked at commit.
    /// When a user account is deleted (US-04), PostgreSQL runs the ON DELETE CASCADE actions one by one; a NO ACTION
    /// key such as recipe_ingredients.ingredient_id could be checked while the ingredient is already deleted but the
    /// recipe lines are not yet. Checked at commit, the key sees the final state: the whole account is deleted, while
    /// deleting a single ingredient, recipe or parent recipe that is still in use is still rejected.
    /// EF Core has no fluent API for DEFERRABLE, so the change is written as SQL.
    /// </summary>
    public partial class DeferrableProtectiveForeignKeys : Migration
    {
        private static readonly (string Table, string Constraint)[] Keys =
        {
            ("recipes", "fk_recipes_recipes_parent_recipe_id"),
            ("recipe_ingredients", "fk_recipe_ingredients_ingredients_ingredient_id"),
            ("batch_items", "fk_batch_items_recipes_recipe_id"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, constraint) in Keys)
                migrationBuilder.Sql($"ALTER TABLE {table} ALTER CONSTRAINT {constraint} DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, constraint) in Keys)
                migrationBuilder.Sql($"ALTER TABLE {table} ALTER CONSTRAINT {constraint} NOT DEFERRABLE;");
        }
    }
}
