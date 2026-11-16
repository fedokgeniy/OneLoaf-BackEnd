using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Validation functions (password hash, JSON structures) and deterministic calculations of the baking
    /// calculator: price on a date, effective content of recipe variants, timeline, kneading cycles,
    /// starter amount and cost. SQL: Sql/CoreFunctions/*.sql.
    /// </summary>
    public partial class CoreFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("CoreFunctions",
                "01_validation_functions.sql",
                "02_recipe_functions.sql",
                "03_calculation_functions.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS fn_recipe_cost(uuid, integer, date);
DROP FUNCTION IF EXISTS fn_starter_amount_g(uuid, integer);
DROP FUNCTION IF EXISTS fn_kneading_cycles(uuid, integer);
DROP FUNCTION IF EXISTS fn_dough_weight_g(uuid, integer);
DROP FUNCTION IF EXISTS fn_recipe_start_window(uuid, timestamptz);
DROP FUNCTION IF EXISTS fn_recipe_ready_window(uuid, timestamptz);
DROP FUNCTION IF EXISTS fn_recipe_timeline(uuid, timestamptz);
DROP FUNCTION IF EXISTS fn_price_on_date(uuid, date);
DROP FUNCTION IF EXISTS fn_recipe_effective_header(uuid);
DROP FUNCTION IF EXISTS fn_recipe_effective_stages(uuid);
DROP FUNCTION IF EXISTS fn_recipe_effective_ingredients(uuid);
DROP FUNCTION IF EXISTS fn_is_valid_recipe_snapshot(jsonb);
DROP FUNCTION IF EXISTS fn_is_valid_variant_overrides(jsonb);
DROP FUNCTION IF EXISTS fn_is_strong_password_hash(text);
");
        }
    }
}
