using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Integrity rules that need more than a column constraint: CHECKs with validation functions,
    /// updated_at maintenance, workspace isolation between users, variant consistency, batch and stage
    /// life cycles, starter feeding schedule, automatic baking history. SQL: Sql/IntegrityTriggers/*.sql.
    /// </summary>
    public partial class IntegrityTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.SqlScripts("IntegrityTriggers",
                "01_function_checks.sql",
                "02_updated_at.sql",
                "03_owner_consistency.sql",
                "04_workflow_rules.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_users_updated_at ON users;
DROP TRIGGER IF EXISTS trg_kitchen_settings_updated_at ON kitchen_settings;
DROP TRIGGER IF EXISTS trg_flours_updated_at ON flours;
DROP TRIGGER IF EXISTS trg_ingredients_updated_at ON ingredients;
DROP TRIGGER IF EXISTS trg_recipes_updated_at ON recipes;
DROP TRIGGER IF EXISTS trg_batches_updated_at ON batches;
DROP TRIGGER IF EXISTS trg_starters_updated_at ON starters;
DROP TRIGGER IF EXISTS trg_bake_records_updated_at ON bake_records;
DROP TRIGGER IF EXISTS trg_chat_conversations_updated_at ON chat_conversations;
DROP TRIGGER IF EXISTS trg_recipes_validate ON recipes;
DROP TRIGGER IF EXISTS trg_ingredients_validate ON ingredients;
DROP TRIGGER IF EXISTS trg_starters_validate ON starters;
DROP TRIGGER IF EXISTS trg_recipe_ingredients_validate ON recipe_ingredients;
DROP TRIGGER IF EXISTS trg_recipe_stages_validate ON recipe_stages;
DROP TRIGGER IF EXISTS trg_batch_items_validate ON batch_items;
DROP TRIGGER IF EXISTS trg_batch_stages_validate ON batch_stages;
DROP TRIGGER IF EXISTS trg_reminders_validate ON reminders;
DROP TRIGGER IF EXISTS trg_bake_records_validate ON bake_records;
DROP TRIGGER IF EXISTS trg_bake_record_items_validate ON bake_record_items;
DROP TRIGGER IF EXISTS trg_published_recipes_validate ON published_recipes;
DROP TRIGGER IF EXISTS trg_content_reports_validate ON content_reports;
DROP TRIGGER IF EXISTS trg_batches_status_before ON batches;
DROP TRIGGER IF EXISTS trg_batches_status_after ON batches;
DROP TRIGGER IF EXISTS trg_batch_stages_progress_before ON batch_stages;
DROP TRIGGER IF EXISTS trg_batch_stages_progress_after ON batch_stages;
DROP TRIGGER IF EXISTS trg_starter_feedings_schedule_before ON starter_feedings;
DROP TRIGGER IF EXISTS trg_starter_feedings_schedule_after ON starter_feedings;
DROP TRIGGER IF EXISTS trg_published_recipes_status ON published_recipes;
DROP TRIGGER IF EXISTS trg_recipe_comments_edited ON recipe_comments;
DROP FUNCTION IF EXISTS trg_set_updated_at();
DROP FUNCTION IF EXISTS fn_assert_same_owner(uuid, uuid, text);
DROP FUNCTION IF EXISTS trg_recipes_validate();
DROP FUNCTION IF EXISTS trg_flour_link_validate();
DROP FUNCTION IF EXISTS trg_recipe_ingredients_validate();
DROP FUNCTION IF EXISTS trg_recipe_stages_validate();
DROP FUNCTION IF EXISTS trg_batch_items_validate();
DROP FUNCTION IF EXISTS trg_batch_stages_validate();
DROP FUNCTION IF EXISTS trg_reminders_validate();
DROP FUNCTION IF EXISTS trg_bake_records_validate();
DROP FUNCTION IF EXISTS trg_bake_record_items_validate();
DROP FUNCTION IF EXISTS trg_published_recipes_validate();
DROP FUNCTION IF EXISTS trg_content_reports_validate();
DROP FUNCTION IF EXISTS trg_batches_status_before();
DROP FUNCTION IF EXISTS fn_create_bake_record(uuid);
DROP FUNCTION IF EXISTS trg_batches_status_after();
DROP FUNCTION IF EXISTS trg_batch_stages_progress_before();
DROP FUNCTION IF EXISTS trg_batch_stages_progress_after();
DROP FUNCTION IF EXISTS trg_starter_feedings_schedule_before();
DROP FUNCTION IF EXISTS trg_starter_feedings_schedule_after();
DROP FUNCTION IF EXISTS trg_published_recipes_status();
DROP FUNCTION IF EXISTS trg_recipe_comments_edited();
ALTER TABLE published_recipes DROP CONSTRAINT IF EXISTS ck_published_recipes_snapshot_valid;
ALTER TABLE recipes DROP CONSTRAINT IF EXISTS ck_recipes_variant_overrides_valid;
ALTER TABLE users DROP CONSTRAINT IF EXISTS ck_users_password_hash_strong;
");
        }
    }
}
