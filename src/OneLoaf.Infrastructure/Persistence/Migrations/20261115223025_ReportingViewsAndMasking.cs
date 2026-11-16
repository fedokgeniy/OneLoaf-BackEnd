using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Masking of personal data, views for recurring queries and the materialized view with baking statistics.
    /// SQL: Sql/ReportingViewsAndMasking/*.sql.
    /// </summary>
    public partial class ReportingViewsAndMasking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("ReportingViewsAndMasking",
                "01_masking.sql",
                "02_views.sql",
                "03_statistics.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS fn_refresh_bake_statistics();
DROP MATERIALIZED VIEW IF EXISTS mv_bake_statistics;
DROP VIEW IF EXISTS v_data_quality_issues;
DROP VIEW IF EXISTS v_public_catalog;
DROP VIEW IF EXISTS v_batch_log_completeness;
DROP VIEW IF EXISTS v_batch_stage_log;
DROP VIEW IF EXISTS v_recipe_effective_ingredients;
DROP VIEW IF EXISTS v_ingredient_current_prices;
DROP VIEW IF EXISTS v_users_masked;
DROP FUNCTION IF EXISTS fn_mask_name(text);
DROP FUNCTION IF EXISTS fn_mask_email(text);
");
        }
    }
}
