-- Access rights of the database roles (see docs/db/security.md).
--   app_admin  owner of all objects; runs migrations. Created by db/security/bootstrap.sql.
--   app_write  application services (Core API, AI Assistant, Notification): read and write workspace data,
--              read reference data, call functions and procedures. No DDL.
--   app_read   reporting and support: read-only, no personal data in clear text.
-- The roles are cluster-level objects and are created once by db/security/bootstrap.sql (superuser);
-- this migration only grants privileges, so it can run under app_admin.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_write')
       OR NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_read') THEN
        RAISE EXCEPTION 'Roles app_write and app_read do not exist'
            USING HINT = 'Run db/security/bootstrap.sql as a superuser once before applying migrations.';
    END IF;
END;
$$;

-- Nothing is granted to PUBLIC
REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC;
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
REVOKE EXECUTE ON ALL PROCEDURES IN SCHEMA public FROM PUBLIC;

GRANT USAGE ON SCHEMA public TO app_write, app_read;

-- Reference data: read-only for everybody, changed only by migrations (app_admin)
GRANT SELECT ON units, currencies, stage_types, ingredient_categories, flour_types TO app_write, app_read;

-- Workspace and library data: full DML for the application
GRANT SELECT, INSERT, UPDATE, DELETE ON
    users, kitchen_settings, push_subscriptions,
    flours, ingredients, ingredient_prices,
    recipes, recipe_ingredients, recipe_stages,
    batches, batch_items, batch_stages,
    starters, starter_feedings, reminders,
    bake_records, bake_record_items, bake_record_photos,
    chat_conversations, chat_messages,
    published_recipes, recipe_comments, recipe_reactions, content_reports
TO app_write;

-- Reporting role: tables without personal data or free text written by users
GRANT SELECT ON
    kitchen_settings,
    flours, ingredients, ingredient_prices,
    recipes, recipe_ingredients, recipe_stages,
    batches, batch_items, batch_stages,
    starters, starter_feedings,
    bake_record_items,
    published_recipes, recipe_reactions
TO app_read;

-- bake_records: app_read sees the numbers but not the free-text comment and assistant analysis (column privileges)
GRANT SELECT (id, owner_id, batch_id, baked_on, total_loaves, total_cost, currency, rating, analysis_at, created_at, updated_at)
    ON bake_records TO app_read;

-- Views and the statistics materialized view
GRANT SELECT ON
    v_ingredient_current_prices, v_recipe_effective_ingredients, v_batch_stage_log,
    v_batch_log_completeness, v_public_catalog, v_data_quality_issues, mv_bake_statistics
TO app_write, app_read;
GRANT SELECT ON v_users_masked TO app_write, app_read;

-- Functions and procedures
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO app_write;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO app_write;

GRANT EXECUTE ON FUNCTION
    fn_price_on_date(uuid, date),
    fn_recipe_effective_ingredients(uuid),
    fn_recipe_effective_stages(uuid),
    fn_recipe_effective_header(uuid),
    fn_recipe_timeline(uuid, timestamptz),
    fn_recipe_ready_window(uuid, timestamptz),
    fn_recipe_start_window(uuid, timestamptz),
    fn_dough_weight_g(uuid, integer),
    fn_kneading_cycles(uuid, integer),
    fn_starter_amount_g(uuid, integer),
    fn_recipe_cost(uuid, integer, date),
    fn_mask_email(text),
    fn_mask_name(text)
TO app_read;

-- The migrations history is visible to app_admin only
REVOKE ALL ON __ef_migrations_history FROM app_write, app_read;

-- Default privileges for objects that later migrations create (as app_admin):
-- tables and functions are available to the application automatically; nothing goes to PUBLIC.
-- app_read gets no default privileges on purpose: every new object is reviewed for personal data
-- and granted explicitly; db/tests/security_audit.sql fails if a new table is left without a decision.
ALTER DEFAULT PRIVILEGES REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_write;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO app_write;
