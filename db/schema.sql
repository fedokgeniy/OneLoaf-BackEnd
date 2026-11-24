CREATE TABLE IF NOT EXISTS __ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE currencies (
        code character(3) NOT NULL,
        name character varying(50) NOT NULL,
        minor_units smallint NOT NULL,
        CONSTRAINT pk_currencies PRIMARY KEY (code),
        CONSTRAINT ck_currencies_code_format CHECK (code ~ '^[A-Z]{3}$'),
        CONSTRAINT ck_currencies_minor_units CHECK (minor_units BETWEEN 0 AND 4)
    );
    COMMENT ON TABLE currencies IS 'Reference. ISO 4217 currencies allowed for prices and costs.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE flour_types (
        code character varying(32) NOT NULL,
        name character varying(50) NOT NULL,
        sort_order integer NOT NULL,
        CONSTRAINT pk_flour_types PRIMARY KEY (code),
        CONSTRAINT ck_flour_types_code_format CHECK (code ~ '^[a-z_]{1,32}$')
    );
    COMMENT ON TABLE flour_types IS 'Reference. Grain types of flour.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE ingredient_categories (
        code character varying(32) NOT NULL,
        name character varying(50) NOT NULL,
        is_flour boolean NOT NULL,
        is_liquid boolean NOT NULL,
        sort_order integer NOT NULL,
        CONSTRAINT pk_ingredient_categories PRIMARY KEY (code),
        CONSTRAINT ck_ingredient_categories_code_format CHECK (code ~ '^[a-z_]{1,32}$'),
        CONSTRAINT ck_ingredient_categories_flour_not_liquid CHECK (NOT (is_flour AND is_liquid))
    );
    COMMENT ON TABLE ingredient_categories IS 'Reference. Ingredient categories; is_flour / is_liquid drive baker''s percentages and hydration.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE stage_types (
        code character varying(32) NOT NULL,
        name character varying(50) NOT NULL,
        is_fermentation boolean NOT NULL,
        default_uses_oven boolean NOT NULL,
        default_uses_mixer boolean NOT NULL,
        sort_order integer NOT NULL,
        CONSTRAINT pk_stage_types PRIMARY KEY (code),
        CONSTRAINT ck_stage_types_code_format CHECK (code ~ '^[a-z_]{1,32}$')
    );
    COMMENT ON TABLE stage_types IS 'Reference. Types of production stages with default equipment use.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE units (
        code character varying(16) NOT NULL,
        name character varying(50) NOT NULL,
        dimension character varying(16) NOT NULL,
        factor_to_base numeric(12,6) NOT NULL,
        sort_order integer NOT NULL,
        CONSTRAINT pk_units PRIMARY KEY (code),
        CONSTRAINT ck_units_code_format CHECK (code ~ '^[a-z]{1,16}$'),
        CONSTRAINT ck_units_dimension CHECK (dimension IN ('mass', 'volume', 'count')),
        CONSTRAINT ck_units_factor_to_base CHECK (factor_to_base > 0)
    );
    COMMENT ON TABLE units IS 'Reference. Units of measure and conversion to the base unit of their dimension.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE users (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        email character varying(256) NOT NULL,
        normalized_email character varying(256) NOT NULL,
        password_hash character varying(512) NOT NULL,
        display_name character varying(100) NOT NULL,
        role character varying(32) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        last_login_at timestamp with time zone,
        CONSTRAINT pk_users PRIMARY KEY (id),
        CONSTRAINT ck_users_display_name_not_blank CHECK (length(btrim(display_name)) > 0),
        CONSTRAINT ck_users_email_format CHECK (email ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
        CONSTRAINT ck_users_normalized_email CHECK (normalized_email = upper(email)),
        CONSTRAINT ck_users_role CHECK (role IN ('user', 'admin'))
    );
    COMMENT ON TABLE users IS 'E1. Registered bakers and administrators; each user owns a private workspace.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE batches (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        status character varying(32) NOT NULL,
        source character varying(32) NOT NULL,
        planned_start_at timestamp with time zone NOT NULL,
        planned_ready_from timestamp with time zone,
        planned_ready_to timestamp with time zone,
        actual_start_at timestamp with time zone,
        completed_at timestamp with time zone,
        price_date date NOT NULL,
        total_cost numeric(12,2),
        currency character(3) NOT NULL,
        notes character varying(4000),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_batches PRIMARY KEY (id),
        CONSTRAINT ck_batches_completed_after_start CHECK (completed_at >= actual_start_at),
        CONSTRAINT ck_batches_done_has_completed_at CHECK (status <> 'done' OR completed_at IS NOT NULL),
        CONSTRAINT ck_batches_name_not_blank CHECK (length(btrim(name)) > 0),
        CONSTRAINT ck_batches_ready_after_start CHECK (planned_ready_from >= planned_start_at),
        CONSTRAINT ck_batches_ready_window CHECK (planned_ready_to >= planned_ready_from),
        CONSTRAINT ck_batches_source CHECK (source IN ('manual', 'assistant')),
        CONSTRAINT ck_batches_status CHECK (status IN ('planned', 'in_progress', 'done', 'cancelled')),
        CONSTRAINT ck_batches_total_cost CHECK (total_cost >= 0),
        CONSTRAINT fk_batches_currencies_currency FOREIGN KEY (currency) REFERENCES currencies (code),
        CONSTRAINT fk_batches_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE batches IS 'E5, US-20, US-26. Production batches with plan, status and cost snapshot.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE chat_conversations (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        title character varying(200),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_chat_conversations PRIMARY KEY (id),
        CONSTRAINT fk_chat_conversations_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE chat_conversations IS 'E8. Conversations with the AI assistant.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE flours (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        flour_type_code character varying(32) NOT NULL,
        protein_percent numeric(4,1),
        grind character varying(50),
        notes character varying(2000),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_flours PRIMARY KEY (id),
        CONSTRAINT ck_flours_protein_percent CHECK (protein_percent BETWEEN 0 AND 100),
        CONSTRAINT fk_flours_flour_types_flour_type_code FOREIGN KEY (flour_type_code) REFERENCES flour_types (code),
        CONSTRAINT fk_flours_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE flours IS 'E3, US-11. Flour catalog of a user.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE kitchen_settings (
        user_id uuid NOT NULL,
        oven_power_kw numeric(6,2),
        oven_capacity_loaves integer,
        mixer_bowl_capacity_kg numeric(6,2),
        electricity_tariff_per_kwh numeric(8,4),
        packaging_cost_per_loaf numeric(10,4) NOT NULL,
        loss_rate_percent numeric(5,2) NOT NULL,
        labor_rate_per_hour numeric(10,2),
        currency character(3) NOT NULL,
        time_zone character varying(64) NOT NULL,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_kitchen_settings PRIMARY KEY (user_id),
        CONSTRAINT ck_kitchen_settings_electricity_tariff_per_kwh CHECK (electricity_tariff_per_kwh >= 0),
        CONSTRAINT ck_kitchen_settings_labor_rate_per_hour CHECK (labor_rate_per_hour >= 0),
        CONSTRAINT ck_kitchen_settings_loss_rate_percent CHECK (loss_rate_percent BETWEEN 0 AND 100),
        CONSTRAINT ck_kitchen_settings_mixer_bowl_capacity_kg CHECK (mixer_bowl_capacity_kg > 0),
        CONSTRAINT ck_kitchen_settings_oven_capacity_loaves CHECK (oven_capacity_loaves > 0),
        CONSTRAINT ck_kitchen_settings_oven_power_kw CHECK (oven_power_kw > 0),
        CONSTRAINT ck_kitchen_settings_packaging_cost_per_loaf CHECK (packaging_cost_per_loaf >= 0),
        CONSTRAINT fk_kitchen_settings_currencies_currency FOREIGN KEY (currency) REFERENCES currencies (code),
        CONSTRAINT fk_kitchen_settings_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE kitchen_settings IS 'E3, US-13, US-47-49. Equipment and cost parameters, one row per user.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE push_subscriptions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        endpoint character varying(1024) NOT NULL,
        p256dh character varying(256) NOT NULL,
        auth character varying(256) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_push_subscriptions PRIMARY KEY (id),
        CONSTRAINT fk_push_subscriptions_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE push_subscriptions IS 'Web Push subscriptions of users'' devices (Notification service).';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE bake_records (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        batch_id uuid,
        baked_on date NOT NULL,
        total_loaves integer NOT NULL,
        total_cost numeric(12,2),
        currency character(3) NOT NULL,
        rating smallint,
        comment character varying(4000),
        assistant_analysis text,
        analysis_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_bake_records PRIMARY KEY (id),
        CONSTRAINT ck_bake_records_rating CHECK (rating BETWEEN 1 AND 5),
        CONSTRAINT ck_bake_records_total_cost CHECK (total_cost >= 0),
        CONSTRAINT ck_bake_records_total_loaves CHECK (total_loaves >= 0),
        CONSTRAINT fk_bake_records_batches_batch_id FOREIGN KEY (batch_id) REFERENCES batches (id) ON DELETE SET NULL,
        CONSTRAINT fk_bake_records_currencies_currency FOREIGN KEY (currency) REFERENCES currencies (code),
        CONSTRAINT fk_bake_records_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE bake_records IS 'E7, US-31, US-32, US-36. Baking history: one record per finished batch.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE chat_messages (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        conversation_id uuid NOT NULL,
        role character varying(32) NOT NULL,
        content text NOT NULL,
        tool_name character varying(100),
        tool_payload jsonb,
        input_tokens integer,
        output_tokens integer,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_chat_messages PRIMARY KEY (id),
        CONSTRAINT ck_chat_messages_role CHECK (role IN ('user', 'assistant', 'tool')),
        CONSTRAINT ck_chat_messages_tokens CHECK (input_tokens >= 0 AND output_tokens >= 0),
        CONSTRAINT ck_chat_messages_tool_name CHECK (role <> 'tool' OR tool_name IS NOT NULL),
        CONSTRAINT ck_chat_messages_tool_payload CHECK (tool_payload IS NULL OR (tool_name IS NOT NULL AND jsonb_typeof(tool_payload) = 'object')),
        CONSTRAINT fk_chat_messages_chat_conversations_conversation_id FOREIGN KEY (conversation_id) REFERENCES chat_conversations (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE chat_messages IS 'E8, US-34-36. Messages of the user, the assistant and tool results.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE ingredients (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        category_code character varying(32) NOT NULL,
        unit_code character varying(16) NOT NULL,
        flour_id uuid,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_ingredients PRIMARY KEY (id),
        CONSTRAINT fk_ingredients_flours_flour_id FOREIGN KEY (flour_id) REFERENCES flours (id) ON DELETE SET NULL,
        CONSTRAINT fk_ingredients_ingredient_categories_category_code FOREIGN KEY (category_code) REFERENCES ingredient_categories (code),
        CONSTRAINT fk_ingredients_units_unit_code FOREIGN KEY (unit_code) REFERENCES units (code),
        CONSTRAINT fk_ingredients_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE ingredients IS 'E3, US-10. Ingredient catalog of a user, with a unit of measure.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE starters (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        name character varying(100) NOT NULL,
        flour_id uuid,
        hydration_percent numeric(5,1) NOT NULL,
        feeding_mode character varying(32) NOT NULL,
        feeding_interval_hours integer NOT NULL,
        rise_hours numeric(4,1),
        is_active boolean NOT NULL,
        notes character varying(2000),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_starters PRIMARY KEY (id),
        CONSTRAINT ck_starters_feeding_interval_hours CHECK (feeding_interval_hours > 0),
        CONSTRAINT ck_starters_feeding_mode CHECK (feeding_mode IN ('regular', 'weekly', 'fridge')),
        CONSTRAINT ck_starters_hydration_percent CHECK (hydration_percent > 0),
        CONSTRAINT ck_starters_rise_hours CHECK (rise_hours > 0),
        CONSTRAINT fk_starters_flours_flour_id FOREIGN KEY (flour_id) REFERENCES flours (id) ON DELETE SET NULL,
        CONSTRAINT fk_starters_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE starters IS 'E6, US-27, US-30. Sourdough starters.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE bake_record_photos (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        bake_record_id uuid NOT NULL,
        storage_key character varying(500) NOT NULL,
        content_type character varying(100) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_bake_record_photos PRIMARY KEY (id),
        CONSTRAINT fk_bake_record_photos_bake_records_bake_record_id FOREIGN KEY (bake_record_id) REFERENCES bake_records (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE bake_record_photos IS 'E7, US-51. Crumb photos; files live in object storage, the row keeps the key.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE ingredient_prices (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        ingredient_id uuid NOT NULL,
        price numeric(12,4) NOT NULL,
        per_quantity numeric(12,3) NOT NULL,
        currency character(3) NOT NULL,
        effective_from date NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_ingredient_prices PRIMARY KEY (id),
        CONSTRAINT ck_ingredient_prices_per_quantity CHECK (per_quantity > 0),
        CONSTRAINT ck_ingredient_prices_price CHECK (price >= 0),
        CONSTRAINT fk_ingredient_prices_currencies_currency FOREIGN KEY (currency) REFERENCES currencies (code),
        CONSTRAINT fk_ingredient_prices_ingredients_ingredient_id FOREIGN KEY (ingredient_id) REFERENCES ingredients (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE ingredient_prices IS 'E3, US-12, US-15. Price history; the price on date D is the row with the latest effective_from <= D.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE starter_feedings (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        starter_id uuid NOT NULL,
        fed_at timestamp with time zone NOT NULL,
        flour_g numeric(8,1) NOT NULL,
        water_g numeric(8,1) NOT NULL,
        starter_kept_g numeric(8,1),
        temperature_c numeric(4,1),
        next_feeding_at timestamp with time zone,
        expected_peak_at timestamp with time zone,
        note character varying(1000),
        CONSTRAINT pk_starter_feedings PRIMARY KEY (id),
        CONSTRAINT ck_starter_feedings_flour_g CHECK (flour_g >= 0),
        CONSTRAINT ck_starter_feedings_next_after_fed CHECK (next_feeding_at > fed_at),
        CONSTRAINT ck_starter_feedings_peak_after_fed CHECK (expected_peak_at > fed_at),
        CONSTRAINT ck_starter_feedings_starter_kept_g CHECK (starter_kept_g >= 0),
        CONSTRAINT ck_starter_feedings_water_g CHECK (water_g >= 0),
        CONSTRAINT fk_starter_feedings_starters_starter_id FOREIGN KEY (starter_id) REFERENCES starters (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE starter_feedings IS 'E6, US-28. Feeding log of a starter.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE bake_record_items (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        bake_record_id uuid NOT NULL,
        recipe_id uuid,
        recipe_title character varying(200) NOT NULL,
        loaves integer NOT NULL,
        cost numeric(12,2),
        CONSTRAINT pk_bake_record_items PRIMARY KEY (id),
        CONSTRAINT ck_bake_record_items_cost CHECK (cost >= 0),
        CONSTRAINT ck_bake_record_items_loaves CHECK (loaves > 0),
        CONSTRAINT fk_bake_record_items_bake_records_bake_record_id FOREIGN KEY (bake_record_id) REFERENCES bake_records (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE bake_record_items IS 'E7, US-31. Snapshot of what was baked (recipe title, loaves, cost).';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE batch_items (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        batch_id uuid NOT NULL,
        recipe_id uuid NOT NULL,
        starter_id uuid,
        loaves integer NOT NULL,
        starter_amount_g numeric(10,1),
        kneading_cycles integer,
        starter_reminder_minutes integer,
        sort_order integer NOT NULL,
        CONSTRAINT pk_batch_items PRIMARY KEY (id),
        CONSTRAINT ck_batch_items_kneading_cycles CHECK (kneading_cycles > 0),
        CONSTRAINT ck_batch_items_loaves CHECK (loaves > 0),
        CONSTRAINT ck_batch_items_starter_amount_g CHECK (starter_amount_g >= 0),
        CONSTRAINT ck_batch_items_starter_reminder_minutes CHECK (starter_reminder_minutes >= 0),
        CONSTRAINT fk_batch_items_batches_batch_id FOREIGN KEY (batch_id) REFERENCES batches (id) ON DELETE CASCADE,
        CONSTRAINT fk_batch_items_starters_starter_id FOREIGN KEY (starter_id) REFERENCES starters (id) ON DELETE SET NULL
    );
    COMMENT ON TABLE batch_items IS 'E5, US-17, US-18. Recipe and number of loaves inside a batch.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE batch_stages (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        batch_item_id uuid NOT NULL,
        recipe_stage_id uuid,
        sort_order integer NOT NULL,
        name character varying(100) NOT NULL,
        stage_type_code character varying(32) NOT NULL,
        planned_min_minutes integer NOT NULL,
        planned_max_minutes integer NOT NULL,
        planned_start_at timestamp with time zone NOT NULL,
        planned_end_min_at timestamp with time zone NOT NULL,
        planned_end_max_at timestamp with time zone NOT NULL,
        actual_start_at timestamp with time zone,
        actual_end_at timestamp with time zone,
        status character varying(32) NOT NULL,
        extended_minutes integer NOT NULL,
        is_overridden boolean NOT NULL,
        actual_temperature_c numeric(4,1),
        note character varying(2000),
        CONSTRAINT pk_batch_stages PRIMARY KEY (id),
        CONSTRAINT ck_batch_stages_actual_order CHECK (actual_end_at >= actual_start_at),
        CONSTRAINT ck_batch_stages_done_has_times CHECK (status <> 'done' OR (actual_start_at IS NOT NULL AND actual_end_at IS NOT NULL)),
        CONSTRAINT ck_batch_stages_end_needs_start CHECK (actual_end_at IS NULL OR actual_start_at IS NOT NULL),
        CONSTRAINT ck_batch_stages_extended_minutes CHECK (extended_minutes >= 0),
        CONSTRAINT ck_batch_stages_planned_minutes CHECK (planned_min_minutes >= 0 AND planned_max_minutes >= planned_min_minutes),
        CONSTRAINT ck_batch_stages_planned_order CHECK (planned_end_min_at >= planned_start_at AND planned_end_max_at >= planned_end_min_at),
        CONSTRAINT ck_batch_stages_status CHECK (status IN ('pending', 'running', 'done', 'skipped')),
        CONSTRAINT ck_batch_stages_temperature CHECK (actual_temperature_c BETWEEN -30 AND 350),
        CONSTRAINT fk_batch_stages_batch_items_batch_item_id FOREIGN KEY (batch_item_id) REFERENCES batch_items (id) ON DELETE CASCADE,
        CONSTRAINT fk_batch_stages_stage_types_stage_type_code FOREIGN KEY (stage_type_code) REFERENCES stage_types (code)
    );
    COMMENT ON TABLE batch_stages IS 'E5, US-21, US-23, US-24. Stage log with planned and actual times (KPI-1).';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE reminders (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        kind character varying(32) NOT NULL,
        starter_id uuid,
        batch_item_id uuid,
        batch_stage_id uuid,
        due_at timestamp with time zone NOT NULL,
        channel character varying(32) NOT NULL,
        status character varying(32) NOT NULL,
        message character varying(500) NOT NULL,
        sent_at timestamp with time zone,
        attempts integer NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_reminders PRIMARY KEY (id),
        CONSTRAINT ck_reminders_attempts CHECK (attempts >= 0),
        CONSTRAINT ck_reminders_channel CHECK (channel IN ('push', 'email')),
        CONSTRAINT ck_reminders_has_target CHECK (num_nonnulls(starter_id, batch_item_id, batch_stage_id) >= 1),
        CONSTRAINT ck_reminders_kind CHECK (kind IN ('starter_feeding', 'before_kneading', 'stage_start')),
        CONSTRAINT ck_reminders_sent_has_time CHECK (status <> 'sent' OR sent_at IS NOT NULL),
        CONSTRAINT ck_reminders_status CHECK (status IN ('scheduled', 'sent', 'cancelled', 'failed')),
        CONSTRAINT fk_reminders_batch_items_batch_item_id FOREIGN KEY (batch_item_id) REFERENCES batch_items (id) ON DELETE CASCADE,
        CONSTRAINT fk_reminders_batch_stages_batch_stage_id FOREIGN KEY (batch_stage_id) REFERENCES batch_stages (id) ON DELETE CASCADE,
        CONSTRAINT fk_reminders_starters_starter_id FOREIGN KEY (starter_id) REFERENCES starters (id) ON DELETE CASCADE,
        CONSTRAINT fk_reminders_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE reminders IS 'E6, US-29. Scheduled notifications processed by the Notification service.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE content_reports (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        reporter_id uuid NOT NULL,
        published_recipe_id uuid,
        comment_id uuid,
        reason character varying(1000) NOT NULL,
        status character varying(32) NOT NULL,
        resolved_by_id uuid,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        resolved_at timestamp with time zone,
        CONSTRAINT pk_content_reports PRIMARY KEY (id),
        CONSTRAINT ck_content_reports_one_target CHECK (num_nonnulls(published_recipe_id, comment_id) = 1),
        CONSTRAINT ck_content_reports_resolution CHECK ((status = 'open' AND resolved_at IS NULL AND resolved_by_id IS NULL) OR (status <> 'open' AND resolved_at IS NOT NULL)),
        CONSTRAINT ck_content_reports_status CHECK (status IN ('open', 'resolved', 'rejected')),
        CONSTRAINT fk_content_reports_users_reporter_id FOREIGN KEY (reporter_id) REFERENCES users (id) ON DELETE CASCADE,
        CONSTRAINT fk_content_reports_users_resolved_by_id FOREIGN KEY (resolved_by_id) REFERENCES users (id) ON DELETE SET NULL
    );
    COMMENT ON TABLE content_reports IS 'LR-11. Reports on published recipes or comments, handled by an administrator.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE published_recipes (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        author_id uuid NOT NULL,
        source_recipe_id uuid,
        title character varying(200) NOT NULL,
        description character varying(4000),
        snapshot jsonb NOT NULL,
        status character varying(32) NOT NULL,
        published_at timestamp with time zone NOT NULL,
        unpublished_at timestamp with time zone,
        removed_reason character varying(500),
        CONSTRAINT pk_published_recipes PRIMARY KEY (id),
        CONSTRAINT ck_published_recipes_removed_has_reason CHECK (status <> 'removed' OR removed_reason IS NOT NULL),
        CONSTRAINT ck_published_recipes_status CHECK (status IN ('published', 'unpublished', 'removed')),
        CONSTRAINT ck_published_recipes_unpublished_at CHECK (status <> 'unpublished' OR unpublished_at IS NOT NULL),
        CONSTRAINT fk_published_recipes_users_author_id FOREIGN KEY (author_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE published_recipes IS 'E9, US-39, US-40. Immutable published snapshot of a recipe (ingredients and stages in JSON).';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE recipe_comments (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        published_recipe_id uuid NOT NULL,
        author_id uuid NOT NULL,
        body character varying(2000) NOT NULL,
        is_removed boolean NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        edited_at timestamp with time zone,
        CONSTRAINT pk_recipe_comments PRIMARY KEY (id),
        CONSTRAINT ck_recipe_comments_body_not_empty CHECK (length(body) > 0),
        CONSTRAINT fk_recipe_comments_published_recipes_published_recipe_id FOREIGN KEY (published_recipe_id) REFERENCES published_recipes (id) ON DELETE CASCADE,
        CONSTRAINT fk_recipe_comments_users_author_id FOREIGN KEY (author_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE recipe_comments IS 'E9, US-41. Comments on published recipes.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE recipe_reactions (
        published_recipe_id uuid NOT NULL,
        user_id uuid NOT NULL,
        reaction character varying(32) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_recipe_reactions PRIMARY KEY (published_recipe_id, user_id),
        CONSTRAINT ck_recipe_reactions_reaction CHECK (reaction IN ('like', 'love', 'want_to_bake')),
        CONSTRAINT fk_recipe_reactions_published_recipes_published_recipe_id FOREIGN KEY (published_recipe_id) REFERENCES published_recipes (id) ON DELETE CASCADE,
        CONSTRAINT fk_recipe_reactions_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE recipe_reactions IS 'E9, US-41. One reaction per user per published recipe.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE recipes (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_id uuid NOT NULL,
        parent_recipe_id uuid,
        copied_from_published_recipe_id uuid,
        title character varying(200) NOT NULL,
        description character varying(4000),
        yield_loaves integer NOT NULL,
        loaf_weight_g numeric(8,1),
        variant_overrides jsonb,
        notes character varying(4000),
        is_archived boolean NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_recipes PRIMARY KEY (id),
        CONSTRAINT ck_recipes_loaf_weight_g CHECK (loaf_weight_g > 0),
        CONSTRAINT ck_recipes_not_own_parent CHECK (parent_recipe_id IS NULL OR parent_recipe_id <> id),
        CONSTRAINT ck_recipes_overrides_only_for_variants CHECK (variant_overrides IS NULL OR parent_recipe_id IS NOT NULL),
        CONSTRAINT ck_recipes_title_not_blank CHECK (length(btrim(title)) > 0),
        CONSTRAINT ck_recipes_variant_inherits_scalars CHECK (parent_recipe_id IS NULL OR (description IS NULL AND loaf_weight_g IS NULL AND notes IS NULL)),
        CONSTRAINT ck_recipes_yield_loaves CHECK (yield_loaves > 0),
        CONSTRAINT fk_recipes_published_recipes_copied_from_published_recipe_id FOREIGN KEY (copied_from_published_recipe_id) REFERENCES published_recipes (id) ON DELETE SET NULL,
        CONSTRAINT fk_recipes_recipes_parent_recipe_id FOREIGN KEY (parent_recipe_id) REFERENCES recipes (id),
        CONSTRAINT fk_recipes_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE recipes IS 'E2, US-05-09. Recipes and variants; a variant stores only its differences from the parent.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE recipe_ingredients (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        recipe_id uuid NOT NULL,
        ingredient_id uuid NOT NULL,
        quantity numeric(12,3) NOT NULL,
        is_optional boolean NOT NULL,
        sort_order integer NOT NULL,
        note character varying(500),
        replaces_item_id uuid,
        is_removed boolean NOT NULL,
        CONSTRAINT pk_recipe_ingredients PRIMARY KEY (id),
        CONSTRAINT ck_recipe_ingredients_quantity CHECK (quantity >= 0),
        CONSTRAINT ck_recipe_ingredients_removed_needs_target CHECK (NOT is_removed OR replaces_item_id IS NOT NULL),
        CONSTRAINT ck_recipe_ingredients_sort_order CHECK (sort_order >= 0),
        CONSTRAINT fk_recipe_ingredients_ingredients_ingredient_id FOREIGN KEY (ingredient_id) REFERENCES ingredients (id),
        CONSTRAINT fk_recipe_ingredients_recipe_ingredients_replaces_item_id FOREIGN KEY (replaces_item_id) REFERENCES recipe_ingredients (id) ON DELETE CASCADE,
        CONSTRAINT fk_recipe_ingredients_recipes_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE recipe_ingredients IS 'E2. Ingredient lines of a recipe; in a variant, lines replace (replaces_item_id), remove (is_removed) or add parent lines.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE TABLE recipe_stages (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        recipe_id uuid NOT NULL,
        sort_order integer NOT NULL,
        name character varying(100) NOT NULL,
        stage_type_code character varying(32) NOT NULL,
        min_duration_minutes integer NOT NULL,
        max_duration_minutes integer NOT NULL,
        active_minutes integer NOT NULL,
        proof_mode character varying(32),
        temperature_c numeric(4,1),
        uses_oven boolean NOT NULL,
        uses_mixer boolean NOT NULL,
        is_optional boolean NOT NULL,
        instructions character varying(4000),
        replaces_stage_id uuid,
        is_removed boolean NOT NULL,
        CONSTRAINT pk_recipe_stages PRIMARY KEY (id),
        CONSTRAINT ck_recipe_stages_active_minutes CHECK (active_minutes >= 0),
        CONSTRAINT ck_recipe_stages_active_within_max CHECK (active_minutes <= max_duration_minutes),
        CONSTRAINT ck_recipe_stages_max_duration CHECK (max_duration_minutes >= min_duration_minutes),
        CONSTRAINT ck_recipe_stages_min_duration CHECK (min_duration_minutes >= 0),
        CONSTRAINT ck_recipe_stages_name_not_blank CHECK (length(btrim(name)) > 0),
        CONSTRAINT ck_recipe_stages_proof_mode CHECK (proof_mode IN ('warm', 'cold')),
        CONSTRAINT ck_recipe_stages_removed_needs_target CHECK (NOT is_removed OR replaces_stage_id IS NOT NULL),
        CONSTRAINT ck_recipe_stages_sort_order CHECK (sort_order >= 0),
        CONSTRAINT ck_recipe_stages_temperature_c CHECK (temperature_c BETWEEN -30 AND 350),
        CONSTRAINT fk_recipe_stages_recipe_stages_replaces_stage_id FOREIGN KEY (replaces_stage_id) REFERENCES recipe_stages (id) ON DELETE CASCADE,
        CONSTRAINT fk_recipe_stages_recipes_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id) ON DELETE CASCADE,
        CONSTRAINT fk_recipe_stages_stage_types_stage_type_code FOREIGN KEY (stage_type_code) REFERENCES stage_types (code)
    );
    COMMENT ON TABLE recipe_stages IS 'E2, US-05, US-09, US-16. Ordered stages with a min-max duration.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    INSERT INTO currencies (code, minor_units, name)
    VALUES ('EUR', 2, 'Euro');
    INSERT INTO currencies (code, minor_units, name)
    VALUES ('GBP', 2, 'Pound sterling');
    INSERT INTO currencies (code, minor_units, name)
    VALUES ('PLN', 2, 'Polish zloty');
    INSERT INTO currencies (code, minor_units, name)
    VALUES ('USD', 2, 'US dollar');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('barley', 'Barley', 5);
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('corn', 'Corn', 7);
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('oat', 'Oat', 6);
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('other', 'Other', 8);
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('rye', 'Rye', 2);
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('spelt', 'Spelt', 3);
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('wheat', 'Wheat', 1);
    INSERT INTO flour_types (code, name, sort_order)
    VALUES ('whole', 'Whole grain (mixed)', 4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('add_in', FALSE, FALSE, 'Add-in (seeds, nuts, fruit)', 9);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('dairy', FALSE, TRUE, 'Dairy', 8);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('fat', FALSE, FALSE, 'Fat', 7);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('flour', TRUE, FALSE, 'Flour', 1);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('other', FALSE, FALSE, 'Other', 11);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('packaging', FALSE, FALSE, 'Packaging', 10);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('salt', FALSE, FALSE, 'Salt', 3);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('starter', FALSE, FALSE, 'Starter', 4);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('sweetener', FALSE, FALSE, 'Sweetener', 6);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('water', FALSE, TRUE, 'Water', 2);
    INSERT INTO ingredient_categories (code, is_flour, is_liquid, name, sort_order)
    VALUES ('yeast', FALSE, FALSE, 'Yeast', 5);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('autolyse', FALSE, FALSE, FALSE, 'Autolyse', 1);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('bake', FALSE, TRUE, FALSE, 'Baking', 8);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('bulk_fermentation', FALSE, FALSE, TRUE, 'Bulk fermentation', 3);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('cool', FALSE, FALSE, FALSE, 'Cooling', 9);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('fold', FALSE, FALSE, TRUE, 'Stretch and fold', 4);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('mix', TRUE, FALSE, FALSE, 'Mixing', 2);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('other', FALSE, FALSE, FALSE, 'Other', 10);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('preshape', FALSE, FALSE, FALSE, 'Pre-shaping', 5);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('proof', FALSE, FALSE, TRUE, 'Proofing', 7);
    INSERT INTO stage_types (code, default_uses_mixer, default_uses_oven, is_fermentation, name, sort_order)
    VALUES ('shape', FALSE, FALSE, FALSE, 'Shaping', 6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    INSERT INTO units (code, dimension, factor_to_base, name, sort_order)
    VALUES ('g', 'mass', 1.0, 'gram', 1);
    INSERT INTO units (code, dimension, factor_to_base, name, sort_order)
    VALUES ('kg', 'mass', 1000.0, 'kilogram', 2);
    INSERT INTO units (code, dimension, factor_to_base, name, sort_order)
    VALUES ('l', 'volume', 1000.0, 'litre', 4);
    INSERT INTO units (code, dimension, factor_to_base, name, sort_order)
    VALUES ('ml', 'volume', 1.0, 'millilitre', 3);
    INSERT INTO units (code, dimension, factor_to_base, name, sort_order)
    VALUES ('pcs', 'count', 1.0, 'piece', 5);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_bake_record_items_bake_record_id ON bake_record_items (bake_record_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_bake_record_items_recipe_id ON bake_record_items (recipe_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_bake_record_photos_bake_record_id ON bake_record_photos (bake_record_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_bake_records_currency ON bake_records (currency);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_bake_records_owner_id_baked_on ON bake_records (owner_id, baked_on);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_bake_records_batch_id ON bake_records (batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batch_items_batch_id_sort_order ON batch_items (batch_id, sort_order);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batch_items_recipe_id ON batch_items (recipe_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batch_items_starter_id ON batch_items (starter_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batch_stages_batch_item_id_sort_order ON batch_stages (batch_item_id, sort_order);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batch_stages_recipe_stage_id ON batch_stages (recipe_stage_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batch_stages_stage_type_code ON batch_stages (stage_type_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batches_currency ON batches (currency);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batches_owner_id_planned_start_at ON batches (owner_id, planned_start_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_batches_owner_id_status ON batches (owner_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_chat_conversations_owner_id_updated_at ON chat_conversations (owner_id, updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_chat_messages_conversation_id_created_at ON chat_messages (conversation_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_content_reports_comment_id ON content_reports (comment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_content_reports_published_recipe_id ON content_reports (published_recipe_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_content_reports_reporter_id ON content_reports (reporter_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_content_reports_resolved_by_id ON content_reports (resolved_by_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_content_reports_status ON content_reports (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_currencies_name ON currencies (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_flour_types_name ON flour_types (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_flours_flour_type_code ON flours (flour_type_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_flours_owner_id_name ON flours (owner_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_ingredient_categories_name ON ingredient_categories (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_ingredient_prices_currency ON ingredient_prices (currency);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_ingredient_prices_ingredient_id_effective_from ON ingredient_prices (ingredient_id, effective_from);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_ingredients_category_code ON ingredients (category_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_ingredients_flour_id ON ingredients (flour_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_ingredients_unit_code ON ingredients (unit_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_ingredients_owner_id_name ON ingredients (owner_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_kitchen_settings_currency ON kitchen_settings (currency);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_published_recipes_author_id ON published_recipes (author_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_published_recipes_source_recipe_id ON published_recipes (source_recipe_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_published_recipes_status_published_at ON published_recipes (status, published_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_published_recipes_title ON published_recipes (title);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_push_subscriptions_user_id ON push_subscriptions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_push_subscriptions_endpoint ON push_subscriptions (endpoint);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_comments_author_id ON recipe_comments (author_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_comments_published_recipe_id_created_at ON recipe_comments (published_recipe_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_ingredients_ingredient_id ON recipe_ingredients (ingredient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_ingredients_recipe_id_sort_order ON recipe_ingredients (recipe_id, sort_order);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_ingredients_replaces_item_id ON recipe_ingredients (replaces_item_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_recipe_ingredients_recipe_id_replaces_item_id ON recipe_ingredients (recipe_id, replaces_item_id) WHERE replaces_item_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_reactions_user_id ON recipe_reactions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_stages_recipe_id_sort_order ON recipe_stages (recipe_id, sort_order);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_stages_replaces_stage_id ON recipe_stages (replaces_stage_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipe_stages_stage_type_code ON recipe_stages (stage_type_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_recipe_stages_recipe_id_replaces_stage_id ON recipe_stages (recipe_id, replaces_stage_id) WHERE replaces_stage_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipes_copied_from_published_recipe_id ON recipes (copied_from_published_recipe_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipes_owner_id_title ON recipes (owner_id, title);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_recipes_parent_recipe_id ON recipes (parent_recipe_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_reminders_batch_item_id ON reminders (batch_item_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_reminders_batch_stage_id ON reminders (batch_stage_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_reminders_owner_id ON reminders (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_reminders_starter_id ON reminders (starter_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_reminders_status_due_at ON reminders (status, due_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_reminders_starter_id_scheduled_feeding ON reminders (starter_id) WHERE kind = 'starter_feeding' AND status = 'scheduled';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_stage_types_name ON stage_types (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_starter_feedings_starter_id_fed_at ON starter_feedings (starter_id, fed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE INDEX ix_starters_flour_id ON starters (flour_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_starters_owner_id_name ON starters (owner_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_units_name ON units (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    CREATE UNIQUE INDEX ux_users_normalized_email ON users (normalized_email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    ALTER TABLE bake_record_items ADD CONSTRAINT fk_bake_record_items_recipes_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    ALTER TABLE batch_items ADD CONSTRAINT fk_batch_items_recipes_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    ALTER TABLE batch_stages ADD CONSTRAINT fk_batch_stages_recipe_stages_recipe_stage_id FOREIGN KEY (recipe_stage_id) REFERENCES recipe_stages (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    ALTER TABLE content_reports ADD CONSTRAINT fk_content_reports_published_recipes_published_recipe_id FOREIGN KEY (published_recipe_id) REFERENCES published_recipes (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    ALTER TABLE content_reports ADD CONSTRAINT fk_content_reports_recipe_comments_comment_id FOREIGN KEY (comment_id) REFERENCES recipe_comments (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    ALTER TABLE published_recipes ADD CONSTRAINT fk_published_recipes_recipes_source_recipe_id FOREIGN KEY (source_recipe_id) REFERENCES recipes (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222953_InitialSchema') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261115222953_InitialSchema', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222959_DeferrableProtectiveForeignKeys') THEN
    ALTER TABLE recipes ALTER CONSTRAINT fk_recipes_recipes_parent_recipe_id DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222959_DeferrableProtectiveForeignKeys') THEN
    ALTER TABLE recipe_ingredients ALTER CONSTRAINT fk_recipe_ingredients_ingredients_ingredient_id DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222959_DeferrableProtectiveForeignKeys') THEN
    ALTER TABLE batch_items ALTER CONSTRAINT fk_batch_items_recipes_recipe_id DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115222959_DeferrableProtectiveForeignKeys') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261115222959_DeferrableProtectiveForeignKeys', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223005_CoreFunctions') THEN
    -- Validation functions used by CHECK constraints.
    -- IMMUTABLE: the result depends only on the argument, so PostgreSQL allows them in CHECK constraints.

    -- Password hash format (NFR-04). Accepts only ASP.NET Core Identity v3 hashes:
    --   byte 0      = 0x01 (format v3)
    --   bytes 1-4   = PRF: 1 = HMAC-SHA256, 2 = HMAC-SHA512 (0 = HMAC-SHA1 is rejected as outdated)
    --   bytes 5-8   = PBKDF2 iteration count, at least 100 000
    --   bytes 9-12  = salt length, at least 16 bytes
    --   then salt and a derived key of at least 32 bytes.
    -- Plain text, MD5, SHA-1 or unsalted values do not pass.
    CREATE OR REPLACE FUNCTION fn_is_strong_password_hash(p_hash text)
    RETURNS boolean
    LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE
    AS $$
    DECLARE
        b          bytea;
        prf        bigint;
        iterations bigint;
        salt_len   bigint;
    BEGIN
        IF p_hash !~ '^[A-Za-z0-9+/]+={0,2}$' OR length(p_hash) % 4 <> 0 THEN
            RETURN false;
        END IF;
        b := decode(p_hash, 'base64');
        IF length(b) < 13 OR get_byte(b, 0) <> 1 THEN
            RETURN false;
        END IF;
        prf        := (get_byte(b, 1)::bigint << 24) | (get_byte(b, 2) << 16) | (get_byte(b, 3) << 8) | get_byte(b, 4);
        iterations := (get_byte(b, 5)::bigint << 24) | (get_byte(b, 6) << 16) | (get_byte(b, 7) << 8) | get_byte(b, 8);
        salt_len   := (get_byte(b, 9)::bigint << 24) | (get_byte(b, 10) << 16) | (get_byte(b, 11) << 8) | get_byte(b, 12);
        RETURN prf IN (1, 2)
           AND iterations >= 100000
           AND salt_len >= 16
           AND length(b) >= 13 + salt_len + 32;
    END;
    $$;

    COMMENT ON FUNCTION fn_is_strong_password_hash(text) IS
        'True for an ASP.NET Core Identity v3 PBKDF2 hash (HMAC-SHA256/512, >= 100000 iterations, salt >= 16 bytes).';

    -- Structure of recipes.variant_overrides.
    -- A JSON object whose keys are a subset of: description (string|null), loafWeightG (number > 0|null), notes (string|null).
    -- Key present = the variant changes the value (null clears it); key absent = the value is inherited from the parent.
    CREATE OR REPLACE FUNCTION fn_is_valid_variant_overrides(p jsonb)
    RETURNS boolean
    LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE
    AS $$
    DECLARE
        k text;
    BEGIN
        IF jsonb_typeof(p) <> 'object' THEN
            RETURN false;
        END IF;
        FOR k IN SELECT jsonb_object_keys(p) LOOP
            CASE k
                WHEN 'description' THEN
                    IF jsonb_typeof(p -> k) NOT IN ('string', 'null') OR length(coalesce(p ->> k, '')) > 4000 THEN RETURN false; END IF;
                WHEN 'notes' THEN
                    IF jsonb_typeof(p -> k) NOT IN ('string', 'null') OR length(coalesce(p ->> k, '')) > 4000 THEN RETURN false; END IF;
                WHEN 'loafWeightG' THEN
                    IF jsonb_typeof(p -> k) = 'null' THEN
                        NULL;
                    ELSIF jsonb_typeof(p -> k) <> 'number' OR (p ->> k)::numeric <= 0 THEN
                        RETURN false;
                    END IF;
                ELSE
                    RETURN false;
            END CASE;
        END LOOP;
        RETURN true;
    END;
    $$;

    COMMENT ON FUNCTION fn_is_valid_variant_overrides(jsonb) IS
        'Validates recipes.variant_overrides: object with optional keys description, loafWeightG, notes.';

    -- Structure of published_recipes.snapshot (schemaVersion 1):
    -- {
    --   "schemaVersion": 1,
    --   "yieldLoaves": <integer > 0>,
    --   "loafWeightG": <number > 0 | null>,
    --   "ingredients": [ { "name": <non-empty string>, "category": <string>, "unit": <string>,
    --                      "quantity": <number >= 0>, "isOptional": <boolean> }, ... ]   (at least one),
    --   "stages":      [ { "name": <non-empty string>, "stageType": <string>, "minMinutes": <integer >= 0>,
    --                      "maxMinutes": <integer >= minMinutes>, "temperatureC": <number | null>,
    --                      "proofMode": "warm" | "cold" | null, "isOptional": <boolean> }, ... ]
    -- }
    CREATE OR REPLACE FUNCTION fn_is_valid_recipe_snapshot(p jsonb)
    RETURNS boolean
    LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE
    AS $$
    DECLARE
        k    text;
        item jsonb;
    BEGIN
        IF jsonb_typeof(p) <> 'object'
           OR NOT (p ?& ARRAY['schemaVersion', 'yieldLoaves', 'ingredients', 'stages']) THEN
            RETURN false;
        END IF;
        FOR k IN SELECT jsonb_object_keys(p) LOOP
            IF k NOT IN ('schemaVersion', 'yieldLoaves', 'loafWeightG', 'ingredients', 'stages') THEN
                RETURN false;
            END IF;
        END LOOP;

        IF p -> 'schemaVersion' <> '1'::jsonb THEN RETURN false; END IF;
        IF jsonb_typeof(p -> 'yieldLoaves') <> 'number'
           OR (p ->> 'yieldLoaves')::numeric <= 0
           OR (p ->> 'yieldLoaves')::numeric <> trunc((p ->> 'yieldLoaves')::numeric) THEN
            RETURN false;
        END IF;
        IF p ? 'loafWeightG' AND jsonb_typeof(p -> 'loafWeightG') <> 'null'
           AND (jsonb_typeof(p -> 'loafWeightG') <> 'number' OR (p ->> 'loafWeightG')::numeric <= 0) THEN
            RETURN false;
        END IF;

        IF jsonb_typeof(p -> 'ingredients') <> 'array' OR jsonb_array_length(p -> 'ingredients') = 0 THEN
            RETURN false;
        END IF;
        FOR item IN SELECT jsonb_array_elements(p -> 'ingredients') LOOP
            IF jsonb_typeof(item) <> 'object'
               OR jsonb_typeof(item -> 'name') IS DISTINCT FROM 'string' OR length(btrim(item ->> 'name')) = 0
               OR jsonb_typeof(item -> 'category') IS DISTINCT FROM 'string'
               OR jsonb_typeof(item -> 'unit') IS DISTINCT FROM 'string'
               OR jsonb_typeof(item -> 'quantity') IS DISTINCT FROM 'number' OR (item ->> 'quantity')::numeric < 0
               OR jsonb_typeof(item -> 'isOptional') IS DISTINCT FROM 'boolean' THEN
                RETURN false;
            END IF;
        END LOOP;

        IF jsonb_typeof(p -> 'stages') <> 'array' THEN
            RETURN false;
        END IF;
        FOR item IN SELECT jsonb_array_elements(p -> 'stages') LOOP
            IF jsonb_typeof(item) <> 'object'
               OR jsonb_typeof(item -> 'name') IS DISTINCT FROM 'string' OR length(btrim(item ->> 'name')) = 0
               OR jsonb_typeof(item -> 'stageType') IS DISTINCT FROM 'string'
               OR jsonb_typeof(item -> 'minMinutes') IS DISTINCT FROM 'number'
               OR jsonb_typeof(item -> 'maxMinutes') IS DISTINCT FROM 'number'
               OR (item ->> 'minMinutes')::numeric < 0
               OR (item ->> 'maxMinutes')::numeric < (item ->> 'minMinutes')::numeric
               OR jsonb_typeof(item -> 'isOptional') IS DISTINCT FROM 'boolean'
               OR (item ? 'temperatureC' AND jsonb_typeof(item -> 'temperatureC') NOT IN ('number', 'null'))
               OR (item ? 'proofMode' AND jsonb_typeof(item -> 'proofMode') <> 'null'
                   AND item ->> 'proofMode' NOT IN ('warm', 'cold')) THEN
                RETURN false;
            END IF;
        END LOOP;
        RETURN true;
    END;
    $$;

    COMMENT ON FUNCTION fn_is_valid_recipe_snapshot(jsonb) IS
        'Validates published_recipes.snapshot against the documented structure (schemaVersion 1).';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223005_CoreFunctions') THEN
    -- Recipe knowledge base: effective content of variants (US-07, US-08).
    -- A variant stores only its differences; these functions assemble the full recipe
    -- by walking up the parent chain. Multi-level variants (variant of a variant) are supported.

    -- Effective ingredient lines of a recipe.
    -- line_id is the id of the row that finally defines the line (the parent's row or the variant's replacement),
    -- origin_recipe_id tells in which recipe of the chain that row lives.
    CREATE OR REPLACE FUNCTION fn_recipe_effective_ingredients(p_recipe_id uuid)
    RETURNS TABLE (
        line_id          uuid,
        origin_recipe_id uuid,
        ingredient_id    uuid,
        quantity         numeric,
        is_optional      boolean,
        sort_order       integer,
        note             text
    )
    LANGUAGE plpgsql STABLE STRICT
    AS $$
    #variable_conflict use_column
    DECLARE
        v_parent_id uuid;
    BEGIN
        SELECT r.parent_recipe_id INTO v_parent_id FROM recipes r WHERE r.id = p_recipe_id;
        IF NOT FOUND THEN
            RETURN;
        END IF;

        IF v_parent_id IS NOT NULL THEN
            RETURN QUERY
            SELECT e.line_id, e.origin_recipe_id, e.ingredient_id, e.quantity, e.is_optional, e.sort_order, e.note
            FROM fn_recipe_effective_ingredients(v_parent_id) e
            WHERE NOT EXISTS (SELECT 1 FROM recipe_ingredients own
                              WHERE own.recipe_id = p_recipe_id AND own.replaces_item_id = e.line_id);
        END IF;

        RETURN QUERY
        SELECT ri.id, ri.recipe_id, ri.ingredient_id, ri.quantity, ri.is_optional, ri.sort_order, ri.note::text
        FROM recipe_ingredients ri
        WHERE ri.recipe_id = p_recipe_id AND NOT ri.is_removed;
    END;
    $$;

    COMMENT ON FUNCTION fn_recipe_effective_ingredients(uuid) IS
        'Full ingredient list of a recipe or variant: inherited lines not replaced/removed by the variant plus its own lines (US-07).';

    -- Effective stages of a recipe, same rules as for ingredients.
    CREATE OR REPLACE FUNCTION fn_recipe_effective_stages(p_recipe_id uuid)
    RETURNS TABLE (
        line_id              uuid,
        origin_recipe_id     uuid,
        sort_order           integer,
        name                 text,
        stage_type_code      text,
        min_duration_minutes integer,
        max_duration_minutes integer,
        active_minutes       integer,
        proof_mode           text,
        temperature_c        numeric,
        uses_oven            boolean,
        uses_mixer           boolean,
        is_optional          boolean
    )
    LANGUAGE plpgsql STABLE STRICT
    AS $$
    #variable_conflict use_column
    DECLARE
        v_parent_id uuid;
    BEGIN
        SELECT r.parent_recipe_id INTO v_parent_id FROM recipes r WHERE r.id = p_recipe_id;
        IF NOT FOUND THEN
            RETURN;
        END IF;

        IF v_parent_id IS NOT NULL THEN
            RETURN QUERY
            SELECT e.*
            FROM fn_recipe_effective_stages(v_parent_id) e
            WHERE NOT EXISTS (SELECT 1 FROM recipe_stages own
                              WHERE own.recipe_id = p_recipe_id AND own.replaces_stage_id = e.line_id);
        END IF;

        RETURN QUERY
        SELECT rs.id, rs.recipe_id, rs.sort_order, rs.name::text, rs.stage_type_code::text,
               rs.min_duration_minutes, rs.max_duration_minutes, rs.active_minutes, rs.proof_mode::text,
               rs.temperature_c, rs.uses_oven, rs.uses_mixer, rs.is_optional
        FROM recipe_stages rs
        WHERE rs.recipe_id = p_recipe_id AND NOT rs.is_removed;
    END;
    $$;

    COMMENT ON FUNCTION fn_recipe_effective_stages(uuid) IS
        'Full ordered stage list of a recipe or variant (US-07).';

    -- Effective scalar fields of a recipe: own values for a base recipe;
    -- parent values with variant_overrides applied for a variant.
    CREATE OR REPLACE FUNCTION fn_recipe_effective_header(p_recipe_id uuid)
    RETURNS TABLE (
        recipe_id     uuid,
        owner_id      uuid,
        title         text,
        yield_loaves  integer,
        description   text,
        loaf_weight_g numeric,
        notes         text
    )
    LANGUAGE plpgsql STABLE STRICT
    AS $$
    #variable_conflict use_column
    DECLARE
        r recipes%ROWTYPE;
        p record;
    BEGIN
        SELECT * INTO r FROM recipes WHERE id = p_recipe_id;
        IF NOT FOUND THEN
            RETURN;
        END IF;
        IF r.parent_recipe_id IS NULL THEN
            RETURN QUERY SELECT r.id, r.owner_id, r.title::text, r.yield_loaves, r.description::text, r.loaf_weight_g, r.notes::text;
            RETURN;
        END IF;

        SELECT * INTO p FROM fn_recipe_effective_header(r.parent_recipe_id);
        RETURN QUERY SELECT
            r.id, r.owner_id, r.title::text, r.yield_loaves,
            CASE WHEN r.variant_overrides ? 'description' THEN r.variant_overrides ->> 'description' ELSE p.description END,
            CASE WHEN r.variant_overrides ? 'loafWeightG' THEN (r.variant_overrides ->> 'loafWeightG')::numeric ELSE p.loaf_weight_g END,
            CASE WHEN r.variant_overrides ? 'notes' THEN r.variant_overrides ->> 'notes' ELSE p.notes END;
    END;
    $$;

    COMMENT ON FUNCTION fn_recipe_effective_header(uuid) IS
        'Title, yield, description, loaf weight and notes of a recipe with variant overrides applied.';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223005_CoreFunctions') THEN
    -- Deterministic calculations of the baking calculator (E4).
    -- The Core API and the AI assistant tools call these functions; the language model never computes numbers itself (US-35).

    -- Price of an ingredient valid on a date (US-15): the latest price with effective_from <= date.
    CREATE OR REPLACE FUNCTION fn_price_on_date(p_ingredient_id uuid, p_as_of date)
    RETURNS TABLE (price numeric, per_quantity numeric, currency character(3), effective_from date)
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT ip.price, ip.per_quantity, ip.currency, ip.effective_from
        FROM ingredient_prices ip
        WHERE ip.ingredient_id = p_ingredient_id
          AND ip.effective_from <= p_as_of
        ORDER BY ip.effective_from DESC
        LIMIT 1;
    $$;

    COMMENT ON FUNCTION fn_price_on_date(uuid, date) IS
        'Price row valid on the given date: latest effective_from <= date (US-15). Uses ux_ingredient_prices_ingredient_id_effective_from.';

    -- Timeline of a recipe for a start time (US-16).
    -- Each stage starts when the previous stages have taken their minimum time and may end
    -- between start + min and start + max of the cumulative durations.
    CREATE OR REPLACE FUNCTION fn_recipe_timeline(p_recipe_id uuid, p_start timestamptz)
    RETURNS TABLE (
        recipe_stage_id    uuid,
        sort_order         integer,
        name               text,
        stage_type_code    text,
        min_minutes        integer,
        max_minutes        integer,
        planned_start_at   timestamptz,
        planned_end_min_at timestamptz,
        planned_end_max_at timestamptz
    )
    LANGUAGE sql STABLE STRICT
    AS $$
        WITH s AS (
            SELECT e.*,
                   sum(e.min_duration_minutes) OVER w AS cum_min,
                   sum(e.max_duration_minutes) OVER w AS cum_max
            FROM fn_recipe_effective_stages(p_recipe_id) e
            WINDOW w AS (ORDER BY e.sort_order, e.line_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
        )
        SELECT s.line_id,
               s.sort_order,
               s.name,
               s.stage_type_code,
               s.min_duration_minutes,
               s.max_duration_minutes,
               p_start + make_interval(mins => (s.cum_min - s.min_duration_minutes)::int),
               p_start + make_interval(mins => s.cum_min::int),
               p_start + make_interval(mins => s.cum_max::int)
        FROM s
        ORDER BY s.sort_order, s.line_id;
    $$;

    COMMENT ON FUNCTION fn_recipe_timeline(uuid, timestamptz) IS
        'Planned start and end window of every stage for a given start time (US-16).';

    -- Ready window for a start time (US-16): start + total min .. start + total max.
    CREATE OR REPLACE FUNCTION fn_recipe_ready_window(p_recipe_id uuid, p_start timestamptz)
    RETURNS TABLE (ready_from timestamptz, ready_to timestamptz)
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT p_start + make_interval(mins => coalesce(sum(min_duration_minutes), 0)::int),
               p_start + make_interval(mins => coalesce(sum(max_duration_minutes), 0)::int)
        FROM fn_recipe_effective_stages(p_recipe_id);
    $$;

    -- Start window for a desired ready time, planning backwards (US-52).
    CREATE OR REPLACE FUNCTION fn_recipe_start_window(p_recipe_id uuid, p_ready_at timestamptz)
    RETURNS TABLE (start_from timestamptz, start_to timestamptz)
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT p_ready_at - make_interval(mins => coalesce(sum(max_duration_minutes), 0)::int),
               p_ready_at - make_interval(mins => coalesce(sum(min_duration_minutes), 0)::int)
        FROM fn_recipe_effective_stages(p_recipe_id);
    $$;

    -- Dough weight in grams for N loaves. Mass units are converted with units.factor_to_base;
    -- liquids in ml are counted 1 ml = 1 g (water-based liquids); pieces are not counted; optional lines are excluded.
    CREATE OR REPLACE FUNCTION fn_dough_weight_g(p_recipe_id uuid, p_loaves integer)
    RETURNS numeric
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT coalesce(sum(e.quantity * u.factor_to_base * p_loaves / h.yield_loaves), 0)
        FROM fn_recipe_effective_ingredients(p_recipe_id) e
        JOIN ingredients i ON i.id = e.ingredient_id
        JOIN units u ON u.code = i.unit_code
        CROSS JOIN fn_recipe_effective_header(p_recipe_id) h
        WHERE NOT e.is_optional
          AND u.dimension IN ('mass', 'volume');
    $$;

    -- Number of kneading cycles for the mixer bowl of the recipe owner (US-17).
    -- NULL when the bowl capacity is not configured.
    CREATE OR REPLACE FUNCTION fn_kneading_cycles(p_recipe_id uuid, p_loaves integer)
    RETURNS integer
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT CASE
                   WHEN ks.mixer_bowl_capacity_kg IS NULL THEN NULL
                   ELSE greatest(1, ceil(fn_dough_weight_g(p_recipe_id, p_loaves) / (ks.mixer_bowl_capacity_kg * 1000)))::int
               END
        FROM recipes r
        LEFT JOIN kitchen_settings ks ON ks.user_id = r.owner_id
        WHERE r.id = p_recipe_id;
    $$;

    -- Starter to prepare for N loaves, grams (US-18): starter lines of the recipe scaled to N loaves.
    CREATE OR REPLACE FUNCTION fn_starter_amount_g(p_recipe_id uuid, p_loaves integer)
    RETURNS numeric
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT coalesce(sum(e.quantity * u.factor_to_base * p_loaves / h.yield_loaves), 0)
        FROM fn_recipe_effective_ingredients(p_recipe_id) e
        JOIN ingredients i ON i.id = e.ingredient_id
        JOIN units u ON u.code = i.unit_code
        CROSS JOIN fn_recipe_effective_header(p_recipe_id) h
        WHERE i.category_code = 'starter' AND NOT e.is_optional;
    $$;

    -- Cost of N loaves of a recipe with prices valid on a date (US-14, US-15, US-47, US-48, US-49).
    -- Lines: one per mandatory ingredient, electricity, packaging, labor (if a labor rate is set), loss, total.
    -- Every amount is rounded to the minor units of the currency; the total is the sum of the rounded lines.
    -- A line whose input is missing (no price on the date, price in another currency, no oven settings)
    -- has amount NULL and is_complete = false; the total is then marked incomplete (UC-01, A4).
    CREATE OR REPLACE FUNCTION fn_recipe_cost(p_recipe_id uuid, p_loaves integer, p_as_of date)
    RETURNS TABLE (
        line_no       integer,
        line_type     text,
        ingredient_id uuid,
        description   text,
        quantity      numeric,
        unit_code     text,
        unit_price    numeric,
        amount        numeric,
        currency      character(3),
        is_complete   boolean
    )
    LANGUAGE plpgsql STABLE STRICT
    AS $$
    #variable_conflict use_column
    DECLARE
        h            record;
        ks           kitchen_settings%ROWTYPE;
        v_currency   character(3);
        v_minor      integer;
        v_no         integer := 0;
        v_subtotal   numeric := 0;
        v_complete   boolean := true;
        v_amount     numeric;
        v_oven_min   numeric;
        v_active_min numeric;
        v_loads      integer;
        line         record;
        pr           record;
    BEGIN
        IF p_loaves <= 0 THEN
            RAISE EXCEPTION 'Number of loaves must be positive, got %', p_loaves USING ERRCODE = '22023';
        END IF;

        SELECT * INTO h FROM fn_recipe_effective_header(p_recipe_id);
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Recipe % not found', p_recipe_id USING ERRCODE = 'P0002';
        END IF;

        SELECT * INTO ks FROM kitchen_settings WHERE user_id = h.owner_id;
        v_currency := coalesce(ks.currency, 'EUR');
        SELECT minor_units INTO v_minor FROM currencies WHERE code = v_currency;

        -- Ingredient lines
        FOR line IN
            SELECT e.ingredient_id, i.name, i.unit_code, e.quantity * p_loaves / h.yield_loaves AS qty
            FROM fn_recipe_effective_ingredients(p_recipe_id) e
            JOIN ingredients i ON i.id = e.ingredient_id
            WHERE NOT e.is_optional
            ORDER BY e.sort_order, e.line_id
        LOOP
            v_no := v_no + 1;
            SELECT * INTO pr FROM fn_price_on_date(line.ingredient_id, p_as_of);
            IF FOUND AND pr.currency = v_currency THEN
                v_amount := round(line.qty * pr.price / pr.per_quantity, v_minor);
                v_subtotal := v_subtotal + v_amount;
                RETURN QUERY SELECT v_no, 'ingredient', line.ingredient_id, line.name::text, line.qty, line.unit_code::text,
                                    pr.price / pr.per_quantity, v_amount, v_currency, true;
            ELSE
                v_complete := false;
                RETURN QUERY SELECT v_no, 'ingredient', line.ingredient_id, line.name::text, line.qty, line.unit_code::text,
                                    NULL::numeric, NULL::numeric, v_currency, false;
            END IF;
        END LOOP;

        -- Electricity: oven power x oven time (maximum durations) x tariff x number of oven loads
        SELECT coalesce(sum(max_duration_minutes) FILTER (WHERE uses_oven), 0),
               coalesce(sum(active_minutes), 0)
        INTO v_oven_min, v_active_min
        FROM fn_recipe_effective_stages(p_recipe_id);

        v_no := v_no + 1;
        IF v_oven_min = 0 THEN
            RETURN QUERY SELECT v_no, 'electricity', NULL::uuid, 'Electricity (no oven stages)', 0::numeric, 'kWh',
                                ks.electricity_tariff_per_kwh, 0::numeric, v_currency, true;
        ELSIF ks.oven_power_kw IS NULL OR ks.electricity_tariff_per_kwh IS NULL THEN
            v_complete := false;
            RETURN QUERY SELECT v_no, 'electricity', NULL::uuid, 'Electricity (oven power or tariff not set)', NULL::numeric, 'kWh',
                                NULL::numeric, NULL::numeric, v_currency, false;
        ELSE
            v_loads := ceil(p_loaves::numeric / coalesce(ks.oven_capacity_loaves, p_loaves))::int;
            v_amount := round(ks.oven_power_kw * v_oven_min / 60 * v_loads * ks.electricity_tariff_per_kwh, v_minor);
            v_subtotal := v_subtotal + v_amount;
            RETURN QUERY SELECT v_no, 'electricity', NULL::uuid,
                                format('Electricity: %s oven load(s)', v_loads),
                                round(ks.oven_power_kw * v_oven_min / 60 * v_loads, 3), 'kWh',
                                ks.electricity_tariff_per_kwh, v_amount, v_currency, true;
        END IF;

        -- Packaging per loaf (US-47)
        v_no := v_no + 1;
        v_amount := round(coalesce(ks.packaging_cost_per_loaf, 0) * p_loaves, v_minor);
        v_subtotal := v_subtotal + v_amount;
        RETURN QUERY SELECT v_no, 'packaging', NULL::uuid, 'Packaging', p_loaves::numeric, 'pcs',
                            coalesce(ks.packaging_cost_per_loaf, 0), v_amount, v_currency, true;

        -- Labor: active minutes of all stages x hourly rate (US-49, only when a rate is set)
        IF ks.labor_rate_per_hour IS NOT NULL THEN
            v_no := v_no + 1;
            v_amount := round(v_active_min / 60 * ks.labor_rate_per_hour, v_minor);
            v_subtotal := v_subtotal + v_amount;
            RETURN QUERY SELECT v_no, 'labor', NULL::uuid, 'Labor', round(v_active_min / 60, 3), 'h',
                                ks.labor_rate_per_hour, v_amount, v_currency, true;
        END IF;

        -- Loss rate on the subtotal (US-48)
        v_no := v_no + 1;
        v_amount := round(v_subtotal * coalesce(ks.loss_rate_percent, 0) / 100, v_minor);
        RETURN QUERY SELECT v_no, 'loss', NULL::uuid, format('Loss %s%%', coalesce(ks.loss_rate_percent, 0)),
                            v_subtotal, NULL::text, coalesce(ks.loss_rate_percent, 0) / 100, v_amount, v_currency, true;

        v_no := v_no + 1;
        RETURN QUERY SELECT v_no, 'total', NULL::uuid, 'Total', p_loaves::numeric, 'loaves',
                            NULL::numeric, v_subtotal + v_amount, v_currency, v_complete;
    END;
    $$;

    COMMENT ON FUNCTION fn_recipe_cost(uuid, integer, date) IS
        'Cost breakdown of N loaves with prices valid on a date: ingredients, electricity, packaging, labor, loss, total (US-14, US-15, US-47-49).';

    COMMENT ON FUNCTION fn_recipe_ready_window(uuid, timestamptz) IS 'Ready window for a start time: start + total minimum .. start + total maximum duration (US-16).';
    COMMENT ON FUNCTION fn_recipe_start_window(uuid, timestamptz) IS 'Start window for a desired ready time, planning backwards (US-52).';
    COMMENT ON FUNCTION fn_dough_weight_g(uuid, integer) IS 'Dough weight in grams for N loaves; ml counted as g, pieces and optional lines excluded.';
    COMMENT ON FUNCTION fn_kneading_cycles(uuid, integer) IS 'Number of kneading cycles for N loaves and the mixer bowl of the owner; NULL if the bowl is not set (US-17).';
    COMMENT ON FUNCTION fn_starter_amount_g(uuid, integer) IS 'Starter to prepare for N loaves, grams (US-18).';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223005_CoreFunctions') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261115223005_CoreFunctions', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223011_IntegrityTriggers') THEN
    -- CHECK constraints that call validation functions (created in the CoreFunctions migration).

    -- NFR-04: only strong salted password hashes, never plain text or outdated algorithms.
    ALTER TABLE users
        ADD CONSTRAINT ck_users_password_hash_strong CHECK (fn_is_strong_password_hash(password_hash));

    -- JSON columns follow a documented structure (see docs/db/design.md, section JSON).
    ALTER TABLE recipes
        ADD CONSTRAINT ck_recipes_variant_overrides_valid
            CHECK (variant_overrides IS NULL OR fn_is_valid_variant_overrides(variant_overrides));

    ALTER TABLE published_recipes
        ADD CONSTRAINT ck_published_recipes_snapshot_valid CHECK (fn_is_valid_recipe_snapshot(snapshot));

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223011_IntegrityTriggers') THEN
    -- updated_at is maintained by the database, not by each caller.

    CREATE OR REPLACE FUNCTION trg_set_updated_at()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        NEW.updated_at := now();
        RETURN NEW;
    END;
    $$;

    COMMENT ON FUNCTION trg_set_updated_at() IS 'BEFORE UPDATE trigger: sets updated_at to the current time.';

    CREATE TRIGGER trg_users_updated_at              BEFORE UPDATE ON users              FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_kitchen_settings_updated_at   BEFORE UPDATE ON kitchen_settings   FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_flours_updated_at             BEFORE UPDATE ON flours             FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_ingredients_updated_at        BEFORE UPDATE ON ingredients        FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_recipes_updated_at            BEFORE UPDATE ON recipes            FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_batches_updated_at            BEFORE UPDATE ON batches            FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_starters_updated_at           BEFORE UPDATE ON starters           FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_bake_records_updated_at       BEFORE UPDATE ON bake_records       FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
    CREATE TRIGGER trg_chat_conversations_updated_at BEFORE UPDATE ON chat_conversations FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223011_IntegrityTriggers') THEN
    -- Workspace isolation at the database level (US-03, NFR-03).
    -- A foreign key only proves that the referenced row exists. These triggers also prove that it belongs
    -- to the same user, so a row of one workspace can never point to a row of another workspace,
    -- even if the application has a bug. Violations raise SQLSTATE 23000 (integrity_constraint_violation).

    CREATE OR REPLACE FUNCTION fn_assert_same_owner(p_expected uuid, p_actual uuid, p_what text)
    RETURNS void
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF p_actual IS DISTINCT FROM p_expected THEN
            RAISE EXCEPTION 'Workspace isolation: % belongs to another user', p_what
                USING ERRCODE = '23000';
        END IF;
    END;
    $$;

    -- recipes: parent in the same workspace, no cycles, variants share the scaling base of the parent.
    CREATE OR REPLACE FUNCTION trg_recipes_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        v_parent  recipes%ROWTYPE;
        v_cursor  uuid;
        v_depth   integer := 0;
    BEGIN
        IF NEW.parent_recipe_id IS NOT NULL THEN
            SELECT * INTO v_parent FROM recipes WHERE id = NEW.parent_recipe_id;
            PERFORM fn_assert_same_owner(NEW.owner_id, v_parent.owner_id, 'parent recipe');
            IF NEW.yield_loaves <> v_parent.yield_loaves THEN
                RAISE EXCEPTION 'A variant must have the same yield_loaves as its parent (% <> %)', NEW.yield_loaves, v_parent.yield_loaves
                    USING ERRCODE = '23514';
            END IF;
            -- walk up the chain: the new parent must not be the recipe itself or one of its variants
            v_cursor := NEW.parent_recipe_id;
            WHILE v_cursor IS NOT NULL LOOP
                IF v_cursor = NEW.id THEN
                    RAISE EXCEPTION 'Recipe variants must not form a cycle' USING ERRCODE = '23514';
                END IF;
                v_depth := v_depth + 1;
                IF v_depth > 10 THEN
                    RAISE EXCEPTION 'Variant chain is deeper than 10 levels' USING ERRCODE = '23514';
                END IF;
                SELECT parent_recipe_id INTO v_cursor FROM recipes WHERE id = v_cursor;
            END LOOP;
        END IF;

        IF TG_OP = 'UPDATE' AND NEW.yield_loaves <> OLD.yield_loaves
           AND EXISTS (SELECT 1 FROM recipes WHERE parent_recipe_id = NEW.id) THEN
            RAISE EXCEPTION 'yield_loaves of a recipe with variants cannot be changed' USING ERRCODE = '23514';
        END IF;
        IF TG_OP = 'UPDATE' AND NEW.owner_id <> OLD.owner_id THEN
            RAISE EXCEPTION 'A recipe cannot be moved to another workspace' USING ERRCODE = '23000';
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_recipes_validate
        BEFORE INSERT OR UPDATE OF parent_recipe_id, owner_id, yield_loaves ON recipes
        FOR EACH ROW EXECUTE FUNCTION trg_recipes_validate();

    -- ingredients / starters: the linked flour is from the same catalog.
    CREATE OR REPLACE FUNCTION trg_flour_link_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.flour_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM flours WHERE id = NEW.flour_id), 'flour');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_ingredients_validate
        BEFORE INSERT OR UPDATE OF flour_id, owner_id ON ingredients
        FOR EACH ROW EXECUTE FUNCTION trg_flour_link_validate();

    CREATE TRIGGER trg_starters_validate
        BEFORE INSERT OR UPDATE OF flour_id, owner_id ON starters
        FOR EACH ROW EXECUTE FUNCTION trg_flour_link_validate();

    -- recipe_ingredients: ingredient from the same workspace; a replacing/removing line exists only in a variant
    -- and targets a line that is effective in the parent.
    CREATE OR REPLACE FUNCTION trg_recipe_ingredients_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        v_recipe recipes%ROWTYPE;
    BEGIN
        SELECT * INTO v_recipe FROM recipes WHERE id = NEW.recipe_id;
        PERFORM fn_assert_same_owner(v_recipe.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.ingredient_id), 'ingredient');

        IF NEW.replaces_item_id IS NOT NULL THEN
            IF v_recipe.parent_recipe_id IS NULL THEN
                RAISE EXCEPTION 'Only a variant can replace or remove ingredient lines' USING ERRCODE = '23514';
            END IF;
            IF NOT EXISTS (SELECT 1 FROM fn_recipe_effective_ingredients(v_recipe.parent_recipe_id) e
                           WHERE e.line_id = NEW.replaces_item_id) THEN
                RAISE EXCEPTION 'replaces_item_id must reference an ingredient line of the parent recipe' USING ERRCODE = '23514';
            END IF;
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_recipe_ingredients_validate
        BEFORE INSERT OR UPDATE ON recipe_ingredients
        FOR EACH ROW EXECUTE FUNCTION trg_recipe_ingredients_validate();

    CREATE OR REPLACE FUNCTION trg_recipe_stages_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        v_parent_id uuid;
    BEGIN
        IF NEW.replaces_stage_id IS NOT NULL THEN
            SELECT parent_recipe_id INTO v_parent_id FROM recipes WHERE id = NEW.recipe_id;
            IF v_parent_id IS NULL THEN
                RAISE EXCEPTION 'Only a variant can replace or remove stages' USING ERRCODE = '23514';
            END IF;
            IF NOT EXISTS (SELECT 1 FROM fn_recipe_effective_stages(v_parent_id) e WHERE e.line_id = NEW.replaces_stage_id) THEN
                RAISE EXCEPTION 'replaces_stage_id must reference a stage of the parent recipe' USING ERRCODE = '23514';
            END IF;
        END IF;
        IF NEW.proof_mode IS NOT NULL AND NEW.stage_type_code <> 'proof' THEN
            RAISE EXCEPTION 'proof_mode is allowed only for proof stages' USING ERRCODE = '23514';
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_recipe_stages_validate
        BEFORE INSERT OR UPDATE ON recipe_stages
        FOR EACH ROW EXECUTE FUNCTION trg_recipe_stages_validate();

    -- batch_items: recipe and starter from the batch owner's workspace.
    CREATE OR REPLACE FUNCTION trg_batch_items_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        v_owner uuid;
    BEGIN
        SELECT owner_id INTO v_owner FROM batches WHERE id = NEW.batch_id;
        PERFORM fn_assert_same_owner(v_owner, (SELECT owner_id FROM recipes WHERE id = NEW.recipe_id), 'recipe');
        IF NEW.starter_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(v_owner, (SELECT owner_id FROM starters WHERE id = NEW.starter_id), 'starter');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_batch_items_validate
        BEFORE INSERT OR UPDATE OF batch_id, recipe_id, starter_id ON batch_items
        FOR EACH ROW EXECUTE FUNCTION trg_batch_items_validate();

    -- batch_stages: the source stage belongs to the effective stages of the item's recipe.
    CREATE OR REPLACE FUNCTION trg_batch_stages_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.recipe_stage_id IS NOT NULL AND NOT EXISTS (
            SELECT 1
            FROM batch_items bi
            CROSS JOIN LATERAL fn_recipe_effective_stages(bi.recipe_id) e
            WHERE bi.id = NEW.batch_item_id AND e.line_id = NEW.recipe_stage_id) THEN
            RAISE EXCEPTION 'recipe_stage_id must be a stage of the recipe of this batch item' USING ERRCODE = '23000';
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_batch_stages_validate
        BEFORE INSERT OR UPDATE OF batch_item_id, recipe_stage_id ON batch_stages
        FOR EACH ROW EXECUTE FUNCTION trg_batch_stages_validate();

    -- reminders: target objects belong to the reminder owner.
    CREATE OR REPLACE FUNCTION trg_reminders_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.starter_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM starters WHERE id = NEW.starter_id), 'starter');
        END IF;
        IF NEW.batch_item_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(NEW.owner_id,
                (SELECT b.owner_id FROM batch_items bi JOIN batches b ON b.id = bi.batch_id WHERE bi.id = NEW.batch_item_id), 'batch item');
        END IF;
        IF NEW.batch_stage_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(NEW.owner_id,
                (SELECT b.owner_id FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
                                                        JOIN batches b ON b.id = bi.batch_id
                 WHERE bs.id = NEW.batch_stage_id), 'batch stage');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_reminders_validate
        BEFORE INSERT OR UPDATE OF owner_id, starter_id, batch_item_id, batch_stage_id ON reminders
        FOR EACH ROW EXECUTE FUNCTION trg_reminders_validate();

    -- bake_records / bake_record_items: batch and recipe from the same workspace.
    CREATE OR REPLACE FUNCTION trg_bake_records_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.batch_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM batches WHERE id = NEW.batch_id), 'batch');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_bake_records_validate
        BEFORE INSERT OR UPDATE OF owner_id, batch_id ON bake_records
        FOR EACH ROW EXECUTE FUNCTION trg_bake_records_validate();

    CREATE OR REPLACE FUNCTION trg_bake_record_items_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.recipe_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner((SELECT owner_id FROM bake_records WHERE id = NEW.bake_record_id),
                                         (SELECT owner_id FROM recipes WHERE id = NEW.recipe_id), 'recipe');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_bake_record_items_validate
        BEFORE INSERT OR UPDATE OF bake_record_id, recipe_id ON bake_record_items
        FOR EACH ROW EXECUTE FUNCTION trg_bake_record_items_validate();

    -- published_recipes: the source recipe belongs to the author; the snapshot is immutable (US-39).
    CREATE OR REPLACE FUNCTION trg_published_recipes_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.source_recipe_id IS NOT NULL
           AND (TG_OP = 'INSERT' OR NEW.source_recipe_id IS DISTINCT FROM OLD.source_recipe_id) THEN
            PERFORM fn_assert_same_owner(NEW.author_id, (SELECT owner_id FROM recipes WHERE id = NEW.source_recipe_id), 'source recipe');
        END IF;
        IF TG_OP = 'UPDATE' THEN
            IF NEW.snapshot IS DISTINCT FROM OLD.snapshot THEN
                RAISE EXCEPTION 'A published snapshot cannot be changed; publish a new version instead' USING ERRCODE = '23514';
            END IF;
            IF NEW.author_id <> OLD.author_id THEN
                RAISE EXCEPTION 'The author of a publication cannot be changed' USING ERRCODE = '23000';
            END IF;
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_published_recipes_validate
        BEFORE INSERT OR UPDATE ON published_recipes
        FOR EACH ROW EXECUTE FUNCTION trg_published_recipes_validate();

    -- content_reports: only an administrator resolves reports (LR-11).
    CREATE OR REPLACE FUNCTION trg_content_reports_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.resolved_by_id IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM users WHERE id = NEW.resolved_by_id AND role = 'admin') THEN
            RAISE EXCEPTION 'Reports can be resolved only by an administrator' USING ERRCODE = '23514';
        END IF;
        IF NEW.status <> 'open' AND (TG_OP = 'INSERT' OR NEW.status <> OLD.status) THEN
            NEW.resolved_at := coalesce(NEW.resolved_at, now());
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_content_reports_validate
        BEFORE INSERT OR UPDATE ON content_reports
        FOR EACH ROW EXECUTE FUNCTION trg_content_reports_validate();

    COMMENT ON FUNCTION fn_assert_same_owner(uuid, uuid, text) IS 'Raises 23000 when a referenced row belongs to another workspace (US-03).';
    COMMENT ON FUNCTION trg_recipes_validate() IS 'Trigger: parent recipe in the same workspace, no cycles, depth <= 10, variant yield equals parent yield, no move between workspaces.';
    COMMENT ON FUNCTION trg_flour_link_validate() IS 'Trigger: flour linked to an ingredient or starter belongs to the same workspace.';
    COMMENT ON FUNCTION trg_recipe_ingredients_validate() IS 'Trigger: ingredient from the same workspace; replacing/removing lines only in variants and only for lines of the parent.';
    COMMENT ON FUNCTION trg_recipe_stages_validate() IS 'Trigger: replacing/removing stages only in variants and only for stages of the parent; proof_mode only for proof stages.';
    COMMENT ON FUNCTION trg_batch_items_validate() IS 'Trigger: recipe and starter of a batch item belong to the batch owner.';
    COMMENT ON FUNCTION trg_batch_stages_validate() IS 'Trigger: the source recipe stage is a stage of the item recipe.';
    COMMENT ON FUNCTION trg_reminders_validate() IS 'Trigger: the starter, batch item or stage of a reminder belongs to its owner.';
    COMMENT ON FUNCTION trg_bake_records_validate() IS 'Trigger: the batch of a history record belongs to its owner.';
    COMMENT ON FUNCTION trg_bake_record_items_validate() IS 'Trigger: the recipe of a history item belongs to the owner of the record.';
    COMMENT ON FUNCTION trg_published_recipes_validate() IS 'Trigger: the source recipe belongs to the author; snapshot and author are immutable.';
    COMMENT ON FUNCTION trg_content_reports_validate() IS 'Trigger: only an administrator resolves reports; sets resolved_at.';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223011_IntegrityTriggers') THEN
    -- Business rules of the production workflow (E5, E6, E7, E9) enforced by triggers.

    ------------------------------------------------------------------------------------------------
    -- Batch life cycle (US-20, US-23, US-31)
    --   planned -> in_progress -> done
    --   planned | in_progress -> cancelled
    --   done and cancelled are final. A batch is always created as planned.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_batches_status_before()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF TG_OP = 'INSERT' THEN
            IF NEW.status <> 'planned' THEN
                RAISE EXCEPTION 'A batch is created with status planned, got %', NEW.status USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END IF;

        IF NEW.status = OLD.status THEN
            RETURN NEW;
        END IF;
        IF NOT ((OLD.status = 'planned'     AND NEW.status IN ('in_progress', 'cancelled'))
             OR (OLD.status = 'in_progress' AND NEW.status IN ('done', 'cancelled'))) THEN
            RAISE EXCEPTION 'Batch status cannot change from % to %', OLD.status, NEW.status USING ERRCODE = '23514';
        END IF;
        IF NEW.status = 'in_progress' THEN
            NEW.actual_start_at := coalesce(NEW.actual_start_at, now());
        ELSIF NEW.status = 'done' THEN
            NEW.completed_at := coalesce(NEW.completed_at, now());
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_batches_status_before
        BEFORE INSERT OR UPDATE OF status ON batches
        FOR EACH ROW EXECUTE FUNCTION trg_batches_status_before();

    -- History record for a finished batch (US-31): date, items, loaves and the cost on the bake date.
    CREATE OR REPLACE FUNCTION fn_create_bake_record(p_batch_id uuid)
    RETURNS uuid
    LANGUAGE plpgsql
    AS $$
    DECLARE
        b         batches%ROWTYPE;
        v_id      uuid;
        v_date    date;
    BEGIN
        SELECT * INTO b FROM batches WHERE id = p_batch_id;
        v_date := coalesce(b.completed_at, now())::date;

        INSERT INTO bake_records (owner_id, batch_id, baked_on, total_loaves, total_cost, currency)
        VALUES (b.owner_id, b.id, v_date, 0, NULL, b.currency)
        ON CONFLICT (batch_id) DO NOTHING
        RETURNING id INTO v_id;
        IF v_id IS NULL THEN
            RETURN (SELECT id FROM bake_records WHERE batch_id = p_batch_id);
        END IF;

        INSERT INTO bake_record_items (bake_record_id, recipe_id, recipe_title, loaves, cost)
        SELECT v_id, bi.recipe_id, h.title, bi.loaves,
               (SELECT c.amount FROM fn_recipe_cost(bi.recipe_id, bi.loaves, v_date) c
                WHERE c.line_type = 'total' AND c.is_complete AND c.currency = b.currency)
        FROM batch_items bi
        CROSS JOIN LATERAL fn_recipe_effective_header(bi.recipe_id) h
        WHERE bi.batch_id = p_batch_id
        ORDER BY bi.sort_order;

        -- total_loaves and total_cost are a snapshot of the items (see docs/db/design.md, denormalization)
        UPDATE bake_records r
        SET total_loaves = s.loaves,
            total_cost   = CASE WHEN s.missing = 0 THEN s.cost END
        FROM (SELECT coalesce(sum(loaves), 0) AS loaves, sum(cost) AS cost, count(*) FILTER (WHERE cost IS NULL) AS missing
              FROM bake_record_items WHERE bake_record_id = v_id) s
        WHERE r.id = v_id;
        RETURN v_id;
    END;
    $$;

    COMMENT ON FUNCTION fn_create_bake_record(uuid) IS
        'Creates the baking history record of a finished batch with a cost snapshot on the bake date (US-31). Idempotent.';

    CREATE OR REPLACE FUNCTION trg_batches_status_after()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.status = 'done' THEN
            PERFORM fn_create_bake_record(NEW.id);
        END IF;
        IF NEW.status IN ('done', 'cancelled') THEN
            -- reminders of a finished or cancelled batch are no longer needed
            UPDATE reminders r
            SET status = 'cancelled'
            WHERE r.status = 'scheduled'
              AND (r.batch_item_id IN (SELECT id FROM batch_items WHERE batch_id = NEW.id)
                   OR r.batch_stage_id IN (SELECT bs.id FROM batch_stages bs
                                           JOIN batch_items bi ON bi.id = bs.batch_item_id
                                           WHERE bi.batch_id = NEW.id));
        END IF;
        RETURN NULL;
    END;
    $$;

    CREATE TRIGGER trg_batches_status_after
        AFTER UPDATE OF status ON batches
        FOR EACH ROW WHEN (NEW.status IS DISTINCT FROM OLD.status)
        EXECUTE FUNCTION trg_batches_status_after();

    ------------------------------------------------------------------------------------------------
    -- Stage execution (US-23): status follows the recorded times; the first started stage starts the batch.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_batch_stages_progress_before()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.status = 'skipped' THEN
            RETURN NEW;
        END IF;
        IF NEW.actual_end_at IS NOT NULL THEN
            NEW.status := 'done';
        ELSIF NEW.actual_start_at IS NOT NULL THEN
            NEW.status := 'running';
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_batch_stages_progress_before
        BEFORE INSERT OR UPDATE OF actual_start_at, actual_end_at, status ON batch_stages
        FOR EACH ROW EXECUTE FUNCTION trg_batch_stages_progress_before();

    CREATE OR REPLACE FUNCTION trg_batch_stages_progress_after()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        UPDATE batches b
        SET status = 'in_progress',
            actual_start_at = coalesce(b.actual_start_at, NEW.actual_start_at)
        FROM batch_items bi
        WHERE bi.id = NEW.batch_item_id AND b.id = bi.batch_id AND b.status = 'planned';
        RETURN NULL;
    END;
    $$;

    CREATE TRIGGER trg_batch_stages_progress_after
        AFTER UPDATE OF actual_start_at ON batch_stages
        FOR EACH ROW WHEN (OLD.actual_start_at IS NULL AND NEW.actual_start_at IS NOT NULL)
        EXECUTE FUNCTION trg_batch_stages_progress_after();

    ------------------------------------------------------------------------------------------------
    -- Starter feeding schedule (US-28, US-29, US-30)
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_starter_feedings_schedule_before()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        s starters%ROWTYPE;
    BEGIN
        SELECT * INTO s FROM starters WHERE id = NEW.starter_id;
        NEW.next_feeding_at := coalesce(NEW.next_feeding_at, NEW.fed_at + make_interval(hours => s.feeding_interval_hours));
        IF s.rise_hours IS NOT NULL THEN
            NEW.expected_peak_at := coalesce(NEW.expected_peak_at, NEW.fed_at + make_interval(secs => s.rise_hours * 3600));
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_starter_feedings_schedule_before
        BEFORE INSERT ON starter_feedings
        FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_schedule_before();

    -- After a feeding the previous feeding reminder is cancelled and the next one is scheduled.
    CREATE OR REPLACE FUNCTION trg_starter_feedings_schedule_after()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        s starters%ROWTYPE;
    BEGIN
        SELECT * INTO s FROM starters WHERE id = NEW.starter_id;

        UPDATE reminders
        SET status = 'cancelled'
        WHERE starter_id = NEW.starter_id AND kind = 'starter_feeding' AND status = 'scheduled';

        IF s.is_active AND NEW.next_feeding_at > now()
           AND NEW.fed_at = (SELECT max(fed_at) FROM starter_feedings WHERE starter_id = NEW.starter_id) THEN
            INSERT INTO reminders (owner_id, kind, starter_id, due_at, channel, status, message, attempts)
            VALUES (s.owner_id, 'starter_feeding', s.id, NEW.next_feeding_at, 'push', 'scheduled',
                    format('Time to feed starter "%s"', s.name), 0);
        END IF;
        RETURN NULL;
    END;
    $$;

    CREATE TRIGGER trg_starter_feedings_schedule_after
        AFTER INSERT ON starter_feedings
        FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_schedule_after();

    ------------------------------------------------------------------------------------------------
    -- Shared library (US-39, US-41)
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_published_recipes_status()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.status = 'unpublished' AND OLD.status <> 'unpublished' THEN
            NEW.unpublished_at := coalesce(NEW.unpublished_at, now());
        ELSIF NEW.status = 'published' AND OLD.status <> 'published' THEN
            IF OLD.status = 'removed' THEN
                RAISE EXCEPTION 'A publication removed by a moderator cannot be published again' USING ERRCODE = '23514';
            END IF;
            NEW.unpublished_at := NULL;
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_published_recipes_status
        BEFORE UPDATE OF status ON published_recipes
        FOR EACH ROW EXECUTE FUNCTION trg_published_recipes_status();

    CREATE OR REPLACE FUNCTION trg_recipe_comments_edited()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.body IS DISTINCT FROM OLD.body THEN
            NEW.edited_at := now();
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_recipe_comments_edited
        BEFORE UPDATE OF body ON recipe_comments
        FOR EACH ROW EXECUTE FUNCTION trg_recipe_comments_edited();

    COMMENT ON FUNCTION trg_batches_status_before() IS 'Trigger: batch life cycle planned -> in_progress -> done | cancelled; sets actual_start_at and completed_at.';
    COMMENT ON FUNCTION trg_batches_status_after() IS 'Trigger: on done creates the history record; on done/cancelled cancels scheduled reminders.';
    COMMENT ON FUNCTION trg_batch_stages_progress_before() IS 'Trigger: stage status follows the recorded actual times (US-23).';
    COMMENT ON FUNCTION trg_batch_stages_progress_after() IS 'Trigger: the first started stage moves the batch to in_progress.';
    COMMENT ON FUNCTION trg_starter_feedings_schedule_before() IS 'Trigger: computes the next feeding time and the expected peak (US-28, US-30).';
    COMMENT ON FUNCTION trg_starter_feedings_schedule_after() IS 'Trigger: replaces the scheduled feeding reminder of the starter (US-29).';
    COMMENT ON FUNCTION trg_published_recipes_status() IS 'Trigger: publication status rules; sets unpublished_at; removed publications cannot be republished.';
    COMMENT ON FUNCTION trg_recipe_comments_edited() IS 'Trigger: sets edited_at when the comment text changes.';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223011_IntegrityTriggers') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261115223011_IntegrityTriggers', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223018_BusinessProcedures') THEN
    -- Stored procedures: multi-step business operations that must be applied completely or not at all.
    -- A CALL is executed atomically: if any statement fails, every change made by the procedure is rolled back.

    ------------------------------------------------------------------------------------------------
    -- Build or rebuild the plan of a planned batch (US-16, US-17, US-18, US-20, US-29).
    -- For every item: stage timeline from the recipe, kneading cycles, starter amount and the reminder
    -- before kneading; for the batch: ready window and total cost with prices on batches.price_date.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE PROCEDURE sp_build_batch_plan(p_batch_id uuid)
    LANGUAGE plpgsql
    AS $$
    DECLARE
        b          batches%ROWTYPE;
        item       record;
        v_total    numeric := 0;
        v_complete boolean := true;
        v_cost     record;
        v_first    timestamptz;
    BEGIN
        SELECT * INTO b FROM batches WHERE id = p_batch_id FOR UPDATE;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Batch % not found', p_batch_id USING ERRCODE = 'P0002';
        END IF;
        IF b.status <> 'planned' THEN
            RAISE EXCEPTION 'Only a planned batch can be (re)planned; batch % is %', p_batch_id, b.status USING ERRCODE = '55000';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM batch_items WHERE batch_id = p_batch_id) THEN
            RAISE EXCEPTION 'Batch % has no items', p_batch_id USING ERRCODE = '23514';
        END IF;

        FOR item IN SELECT * FROM batch_items WHERE batch_id = p_batch_id ORDER BY sort_order FOR UPDATE LOOP
            DELETE FROM reminders WHERE batch_item_id = item.id AND status = 'scheduled';
            DELETE FROM batch_stages WHERE batch_item_id = item.id;

            INSERT INTO batch_stages (batch_item_id, recipe_stage_id, sort_order, name, stage_type_code,
                                      planned_min_minutes, planned_max_minutes, planned_start_at, planned_end_min_at, planned_end_max_at, status, extended_minutes, is_overridden)
            SELECT item.id, t.recipe_stage_id, t.sort_order, t.name, t.stage_type_code,
                   t.min_minutes, t.max_minutes, t.planned_start_at, t.planned_end_min_at, t.planned_end_max_at, 'pending', 0, false
            FROM fn_recipe_timeline(item.recipe_id, b.planned_start_at) t;

            UPDATE batch_items
            SET kneading_cycles  = fn_kneading_cycles(item.recipe_id, item.loaves),
                starter_amount_g = nullif(fn_starter_amount_g(item.recipe_id, item.loaves), 0)
            WHERE id = item.id;

            SELECT * INTO v_cost FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) WHERE line_type = 'total';
            IF v_cost.is_complete AND v_cost.currency = b.currency THEN
                v_total := v_total + v_cost.amount;
            ELSE
                v_complete := false;
            END IF;

            IF item.starter_reminder_minutes IS NOT NULL THEN
                SELECT min(planned_start_at) INTO v_first
                FROM batch_stages WHERE batch_item_id = item.id AND stage_type_code = 'mix';
                v_first := coalesce(v_first, b.planned_start_at);
                INSERT INTO reminders (owner_id, kind, starter_id, batch_item_id, due_at, channel, status, message, attempts)
                VALUES (b.owner_id, 'before_kneading', item.starter_id, item.id,
                        v_first - make_interval(mins => item.starter_reminder_minutes), 'push', 'scheduled',
                        format('Prepare the starter: kneading starts in %s min', item.starter_reminder_minutes), 0);
            END IF;
        END LOOP;

        UPDATE batches
        SET planned_ready_from = s.ready_from,
            planned_ready_to   = s.ready_to,
            total_cost         = CASE WHEN v_complete THEN v_total END
        FROM (SELECT max(bs.planned_end_min_at) AS ready_from, max(bs.planned_end_max_at) AS ready_to
              FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
              WHERE bi.batch_id = p_batch_id) s
        WHERE id = p_batch_id;
    END;
    $$;

    COMMENT ON PROCEDURE sp_build_batch_plan(uuid) IS
        'Builds the stage plan, kneading cycles, starter amount, reminders, ready window and total cost of a planned batch (US-16-18, US-20, US-29).';

    ------------------------------------------------------------------------------------------------
    -- Extend a pending or running stage by N minutes and shift the following stages (US-24).
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE PROCEDURE sp_extend_batch_stage(p_stage_id uuid, p_minutes integer DEFAULT 30)
    LANGUAGE plpgsql
    AS $$
    DECLARE
        st       batch_stages%ROWTYPE;
        v_status text;
        v_batch  uuid;
        v_shift  interval;
    BEGIN
        IF p_minutes IS NULL OR p_minutes <= 0 OR p_minutes > 24 * 60 THEN
            RAISE EXCEPTION 'Extension must be between 1 and 1440 minutes, got %', p_minutes USING ERRCODE = '22023';
        END IF;

        SELECT * INTO st FROM batch_stages WHERE id = p_stage_id FOR UPDATE;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Stage % not found', p_stage_id USING ERRCODE = 'P0002';
        END IF;
        SELECT b.status, b.id INTO v_status, v_batch
        FROM batch_items bi JOIN batches b ON b.id = bi.batch_id WHERE bi.id = st.batch_item_id;
        IF v_status NOT IN ('planned', 'in_progress') OR st.status NOT IN ('pending', 'running') THEN
            RAISE EXCEPTION 'Only a pending or running stage of an active batch can be extended' USING ERRCODE = '55000';
        END IF;

        v_shift := make_interval(mins => p_minutes);

        UPDATE batch_stages
        SET planned_end_min_at = planned_end_min_at + v_shift,
            planned_end_max_at = planned_end_max_at + v_shift,
            extended_minutes   = extended_minutes + p_minutes
        WHERE id = p_stage_id;

        UPDATE batch_stages
        SET planned_start_at   = planned_start_at + v_shift,
            planned_end_min_at = planned_end_min_at + v_shift,
            planned_end_max_at = planned_end_max_at + v_shift
        WHERE batch_item_id = st.batch_item_id AND sort_order > st.sort_order AND status = 'pending';

        UPDATE reminders
        SET due_at = due_at + v_shift
        WHERE status = 'scheduled'
          AND batch_stage_id IN (SELECT id FROM batch_stages
                                 WHERE batch_item_id = st.batch_item_id AND sort_order > st.sort_order);

        UPDATE batches
        SET planned_ready_from = s.ready_from,
            planned_ready_to   = s.ready_to
        FROM (SELECT max(bs.planned_end_min_at) AS ready_from, max(bs.planned_end_max_at) AS ready_to
              FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
              WHERE bi.batch_id = v_batch) s
        WHERE id = v_batch;
    END;
    $$;

    COMMENT ON PROCEDURE sp_extend_batch_stage(uuid, integer) IS
        'Extends a stage by N minutes (button "+30 min") and shifts the following stages, reminders and the ready window (US-24).';

    ------------------------------------------------------------------------------------------------
    -- Publish a recipe to the shared library (US-39): an immutable snapshot of the effective recipe.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE PROCEDURE sp_publish_recipe(p_recipe_id uuid, INOUT p_published_recipe_id uuid DEFAULT NULL)
    LANGUAGE plpgsql
    AS $$
    DECLARE
        h          record;
        v_snapshot jsonb;
    BEGIN
        SELECT * INTO h FROM fn_recipe_effective_header(p_recipe_id);
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Recipe % not found', p_recipe_id USING ERRCODE = 'P0002';
        END IF;

        v_snapshot := jsonb_build_object(
            'schemaVersion', 1,
            'yieldLoaves', h.yield_loaves,
            'loafWeightG', h.loaf_weight_g,
            'ingredients', coalesce((
                SELECT jsonb_agg(jsonb_build_object(
                           'name', i.name, 'category', i.category_code, 'unit', i.unit_code,
                           'quantity', e.quantity, 'isOptional', e.is_optional)
                       ORDER BY e.sort_order, e.line_id)
                FROM fn_recipe_effective_ingredients(p_recipe_id) e
                JOIN ingredients i ON i.id = e.ingredient_id), '[]'::jsonb),
            'stages', coalesce((
                SELECT jsonb_agg(jsonb_build_object(
                           'name', s.name, 'stageType', s.stage_type_code,
                           'minMinutes', s.min_duration_minutes, 'maxMinutes', s.max_duration_minutes,
                           'temperatureC', s.temperature_c, 'proofMode', s.proof_mode, 'isOptional', s.is_optional)
                       ORDER BY s.sort_order, s.line_id)
                FROM fn_recipe_effective_stages(p_recipe_id) s), '[]'::jsonb));

        INSERT INTO published_recipes (author_id, source_recipe_id, title, description, snapshot, status, published_at)
        VALUES (h.owner_id, p_recipe_id, h.title, h.description, v_snapshot, 'published', now())
        RETURNING id INTO p_published_recipe_id;
    END;
    $$;

    COMMENT ON PROCEDURE sp_publish_recipe(uuid, uuid) IS
        'Publishes an immutable JSON snapshot of the effective recipe to the shared library and returns its id (US-39).';

    ------------------------------------------------------------------------------------------------
    -- Anonymize a user (GDPR, LR-03): personal data and the private workspace are removed,
    -- public contributions (published recipes, comments, reactions) stay, signed "Anonymous baker".
    -- Full deletion of the account is a plain DELETE FROM users (cascade, US-04).
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION fn_random_disabled_password_hash()
    RETURNS text
    LANGUAGE sql VOLATILE
    AS $$
        -- A valid v3 hash (HMAC-SHA512, 100000 iterations) of random bytes: nobody knows a matching password.
        -- header: 01 | PRF 00000002 (HMAC-SHA512) | iterations 000186a0 (100000) | salt length 00000010 (16)
        SELECT replace(encode('\x0100000002000186a000000010'::bytea
                              || uuid_send(gen_random_uuid())
                              || uuid_send(gen_random_uuid()) || uuid_send(gen_random_uuid()), 'base64'), E'\n', '');
    $$;

    CREATE OR REPLACE PROCEDURE sp_anonymize_user(p_user_id uuid)
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM users WHERE id = p_user_id) THEN
            RAISE EXCEPTION 'User % not found', p_user_id USING ERRCODE = 'P0002';
        END IF;

        DELETE FROM push_subscriptions WHERE user_id = p_user_id;
        DELETE FROM chat_conversations WHERE owner_id = p_user_id;
        DELETE FROM reminders          WHERE owner_id = p_user_id;
        DELETE FROM bake_records       WHERE owner_id = p_user_id;
        DELETE FROM batches            WHERE owner_id = p_user_id;
        DELETE FROM starters           WHERE owner_id = p_user_id;
        DELETE FROM recipes            WHERE owner_id = p_user_id;
        DELETE FROM ingredients        WHERE owner_id = p_user_id;
        DELETE FROM flours             WHERE owner_id = p_user_id;
        DELETE FROM kitchen_settings   WHERE user_id  = p_user_id;

        UPDATE users
        SET email            = 'anonymized-' || id || '@anonymized.invalid',
            normalized_email = upper('anonymized-' || id || '@anonymized.invalid'),
            display_name     = 'Anonymous baker',
            password_hash    = fn_random_disabled_password_hash(),
            role             = 'user',
            last_login_at    = NULL
        WHERE id = p_user_id;
    END;
    $$;

    COMMENT ON PROCEDURE sp_anonymize_user(uuid) IS
        'Removes personal data and the private workspace of a user, keeps public contributions as "Anonymous baker" (GDPR).';

    COMMENT ON FUNCTION fn_random_disabled_password_hash() IS 'Valid PBKDF2 hash of random bytes: disables login of an anonymized account.';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223018_BusinessProcedures') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261115223018_BusinessProcedures', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223025_ReportingViewsAndMasking') THEN
    -- Masking of personal data (GDPR data minimisation).
    -- Roles that do not need personal data (app_read: reporting, support analytics) get masked views only.

    CREATE OR REPLACE FUNCTION fn_mask_email(p_email text)
    RETURNS text
    LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
    AS $$
        -- baker@example.com -> b***@e***.com
        SELECT CASE
                   WHEN position('@' IN p_email) = 0 THEN left(p_email, 1) || '***'
                   ELSE left(split_part(p_email, '@', 1), 1) || '***@'
                        || left(split_part(p_email, '@', 2), 1) || '***'
                        || coalesce('.' || nullif(substring(split_part(p_email, '@', 2) FROM '\.([^.]+)$'), ''), '')
               END;
    $$;

    COMMENT ON FUNCTION fn_mask_email(text) IS 'Masks an e-mail address: baker@example.com -> b***@e***.com.';

    CREATE OR REPLACE FUNCTION fn_mask_name(p_name text)
    RETURNS text
    LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
    AS $$
        -- Demo Baker -> D*** B***
        SELECT string_agg(left(part, 1) || '***', ' ' ORDER BY ord)
        FROM regexp_split_to_table(btrim(p_name), '\s+') WITH ORDINALITY AS t(part, ord);
    $$;

    COMMENT ON FUNCTION fn_mask_name(text) IS 'Masks a person name word by word: Demo Baker -> D*** B***.';

    CREATE VIEW v_users_masked AS
    SELECT u.id,
           fn_mask_email(u.email)       AS email_masked,
           fn_mask_name(u.display_name) AS display_name_masked,
           u.role,
           u.created_at::date           AS registered_on,
           u.last_login_at::date        AS last_login_on
    FROM users u;

    COMMENT ON VIEW v_users_masked IS
        'Users without personal data in clear text: masked e-mail and name, dates without time. For the app_read role.';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223025_ReportingViewsAndMasking') THEN
    -- Views that hide recurring joins and calculations from the services.

    -- Current price of every ingredient (US-12): the price valid today.
    CREATE VIEW v_ingredient_current_prices AS
    SELECT i.owner_id,
           i.id                          AS ingredient_id,
           i.name                        AS ingredient_name,
           i.unit_code,
           p.price,
           p.per_quantity,
           round(p.price / p.per_quantity, 6) AS unit_price,
           p.currency,
           p.effective_from
    FROM ingredients i
    LEFT JOIN LATERAL fn_price_on_date(i.id, current_date) p ON true;

    COMMENT ON VIEW v_ingredient_current_prices IS 'Price of each ingredient valid today; NULL price = no price yet (US-12).';

    -- Full ingredient list of every recipe and variant (US-07, US-08).
    CREATE VIEW v_recipe_effective_ingredients AS
    SELECT r.owner_id,
           r.id                         AS recipe_id,
           r.title                      AS recipe_title,
           r.parent_recipe_id,
           e.line_id,
           e.ingredient_id,
           i.name                       AS ingredient_name,
           i.category_code,
           e.quantity,
           i.unit_code,
           e.is_optional,
           e.sort_order,
           (e.origin_recipe_id <> r.id) AS is_inherited
    FROM recipes r
    CROSS JOIN LATERAL fn_recipe_effective_ingredients(r.id) e
    JOIN ingredients i ON i.id = e.ingredient_id;

    COMMENT ON VIEW v_recipe_effective_ingredients IS
        'Effective ingredient lines of every recipe; is_inherited = line comes from a parent recipe (US-07, US-08).';

    -- Stage log with deviations from the plan (US-23, US-36, KPI-1).
    -- deviation_minutes: how much the actual duration is outside the planned range of the stage
    -- [planned_min_minutes, planned_max_minutes + extended_minutes]
    -- (positive = longer than planned, negative = shorter than planned, 0 = within the range).
    CREATE VIEW v_batch_stage_log AS
    SELECT b.owner_id,
           b.id                                  AS batch_id,
           b.name                                AS batch_name,
           b.status                              AS batch_status,
           bi.id                                 AS batch_item_id,
           h.title                               AS recipe_title,
           bs.id                                 AS batch_stage_id,
           bs.sort_order,
           bs.name                               AS stage_name,
           bs.stage_type_code,
           bs.status                             AS stage_status,
           bs.planned_start_at,
           bs.planned_end_min_at,
           bs.planned_end_max_at,
           bs.actual_start_at,
           bs.actual_end_at,
           bs.extended_minutes,
           bs.actual_temperature_c,
           round(extract(epoch FROM bs.actual_start_at - bs.planned_start_at) / 60)    AS start_delay_minutes,
           bs.planned_min_minutes,
           bs.planned_max_minutes,
           a.actual_minutes,
           CASE
               WHEN a.actual_minutes IS NULL THEN NULL
               WHEN a.actual_minutes > bs.planned_max_minutes + bs.extended_minutes
                   THEN a.actual_minutes - (bs.planned_max_minutes + bs.extended_minutes)
               WHEN a.actual_minutes < bs.planned_min_minutes
                   THEN a.actual_minutes - bs.planned_min_minutes
               ELSE 0
           END                                   AS deviation_minutes
    FROM batches b
    JOIN batch_items bi ON bi.batch_id = b.id
    JOIN batch_stages bs ON bs.batch_item_id = bi.id
    CROSS JOIN LATERAL (SELECT round(extract(epoch FROM bs.actual_end_at - bs.actual_start_at) / 60)::integer AS actual_minutes) a
    CROSS JOIN LATERAL fn_recipe_effective_header(bi.recipe_id) h;

    COMMENT ON VIEW v_batch_stage_log IS 'Planned vs actual stage times with start delay and deviation from the planned window (US-23, US-36).';

    -- KPI-1: share of finished batches with a complete stage log.
    CREATE VIEW v_batch_log_completeness AS
    SELECT b.owner_id,
           b.id                                                                    AS batch_id,
           b.status,
           b.completed_at,
           count(bs.id)                                                            AS stages_total,
           count(bs.id) FILTER (WHERE bs.status = 'skipped'
                                   OR (bs.actual_start_at IS NOT NULL AND bs.actual_end_at IS NOT NULL)) AS stages_logged,
           count(bs.id) > 0
           AND count(bs.id) = count(bs.id) FILTER (WHERE bs.status = 'skipped'
                                   OR (bs.actual_start_at IS NOT NULL AND bs.actual_end_at IS NOT NULL)) AS is_complete
    FROM batches b
    LEFT JOIN batch_items bi ON bi.batch_id = b.id
    LEFT JOIN batch_stages bs ON bs.batch_item_id = bi.id
    GROUP BY b.owner_id, b.id, b.status, b.completed_at;

    COMMENT ON VIEW v_batch_log_completeness IS 'Per batch: number of stages and of stages with full actual times; basis of KPI-1.';

    -- Public catalog (US-40): only published snapshots and public profile data, no private tables exposed.
    CREATE VIEW v_public_catalog AS
    SELECT pr.id                                         AS published_recipe_id,
           pr.title,
           pr.description,
           u.display_name                                AS author_name,
           pr.published_at,
           (pr.snapshot ->> 'yieldLoaves')::integer      AS yield_loaves,
           jsonb_array_length(pr.snapshot -> 'ingredients') AS ingredient_count,
           jsonb_array_length(pr.snapshot -> 'stages')   AS stage_count,
           (SELECT count(*) FROM recipe_comments c
            WHERE c.published_recipe_id = pr.id AND NOT c.is_removed) AS comment_count,
           (SELECT count(*) FROM recipe_reactions r WHERE r.published_recipe_id = pr.id) AS reaction_count
    FROM published_recipes pr
    JOIN users u ON u.id = pr.author_id
    WHERE pr.status = 'published';

    COMMENT ON VIEW v_public_catalog IS 'Published recipes with author name and comment/reaction counts (US-40).';

    -- Data quality issues that constraints cannot express (they depend on time or on several tables).
    -- Used by the application to warn the baker and by monitoring (see docs/db/data-dictionary.md).
    CREATE VIEW v_data_quality_issues AS
    -- an ingredient used in an active recipe has no price today -> cost is incomplete (UC-01, A4)
    SELECT DISTINCT r.owner_id, 'missing_price'::text AS issue_code, 'ingredient'::text AS entity_type,
           i.id AS entity_id, format('No price for "%s" used in "%s"', i.name, r.title) AS details
    FROM recipes r
    CROSS JOIN LATERAL fn_recipe_effective_ingredients(r.id) e
    JOIN ingredients i ON i.id = e.ingredient_id
    WHERE NOT r.is_archived AND NOT e.is_optional
      AND NOT EXISTS (SELECT 1 FROM fn_price_on_date(i.id, current_date))
    UNION ALL
    -- the latest price is in another currency than the kitchen settings
    SELECT i.owner_id, 'price_currency_mismatch', 'ingredient', i.id,
           format('Price of "%s" is in %s, kitchen currency is %s', i.name, p.currency, ks.currency)
    FROM ingredients i
    JOIN kitchen_settings ks ON ks.user_id = i.owner_id
    CROSS JOIN LATERAL fn_price_on_date(i.id, current_date) p
    WHERE p.currency <> ks.currency
    UNION ALL
    SELECT r.owner_id, 'recipe_without_ingredients', 'recipe', r.id, format('Recipe "%s" has no ingredients', r.title)
    FROM recipes r
    WHERE NOT r.is_archived AND NOT EXISTS (SELECT 1 FROM fn_recipe_effective_ingredients(r.id))
    UNION ALL
    SELECT r.owner_id, 'recipe_without_stages', 'recipe', r.id, format('Recipe "%s" has no stages', r.title)
    FROM recipes r
    WHERE NOT r.is_archived AND NOT EXISTS (SELECT 1 FROM fn_recipe_effective_stages(r.id))
    UNION ALL
    -- a planned batch whose start passed more than a day ago and was never started
    SELECT b.owner_id, 'overdue_planned_batch', 'batch', b.id,
           format('Batch "%s" was planned for %s and not started', b.name, b.planned_start_at)
    FROM batches b
    WHERE b.status = 'planned' AND b.planned_start_at < now() - interval '1 day'
    UNION ALL
    -- a finished batch without a complete stage log (KPI-1)
    SELECT c.owner_id, 'incomplete_stage_log', 'batch', c.batch_id,
           format('%s of %s stages have actual times', c.stages_logged, c.stages_total)
    FROM v_batch_log_completeness c
    WHERE c.status = 'done' AND NOT c.is_complete
    UNION ALL
    -- an active starter that has not been fed for two feeding intervals
    SELECT s.owner_id, 'starter_feeding_overdue', 'starter', s.id,
           format('Starter "%s" last fed %s', s.name, coalesce(f.last_fed::text, 'never'))
    FROM starters s
    LEFT JOIN LATERAL (SELECT max(fed_at) AS last_fed FROM starter_feedings WHERE starter_id = s.id) f ON true
    WHERE s.is_active
      AND (f.last_fed IS NULL OR f.last_fed < now() - make_interval(hours => 2 * s.feeding_interval_hours));

    COMMENT ON VIEW v_data_quality_issues IS
        'Data quality problems that depend on time or several tables: missing prices, empty recipes, overdue batches, incomplete stage logs, overdue feedings.';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223025_ReportingViewsAndMasking') THEN
    -- Baking statistics per baker and recipe (E7, US-33, analysis by the assistant).
    -- A materialized view: the aggregation over the whole history is computed once and refreshed
    -- after a batch is finished, instead of on every opening of the history screen.

    CREATE MATERIALIZED VIEW mv_bake_statistics AS
    SELECT br.owner_id,
           coalesce(bri.recipe_id::text, 'title:' || bri.recipe_title)            AS recipe_key,
           bri.recipe_id,
           max(bri.recipe_title)                                                  AS recipe_title,
           count(DISTINCT br.id)                                                  AS bakes_count,
           sum(bri.loaves)                                                        AS loaves_total,
           round(avg(br.rating), 2)                                               AS avg_rating,
           count(br.rating)                                                       AS rated_count,
           round(sum(bri.cost) / nullif(sum(bri.loaves) FILTER (WHERE bri.cost IS NOT NULL), 0), 2) AS avg_cost_per_loaf,
           min(br.baked_on)                                                       AS first_baked_on,
           max(br.baked_on)                                                       AS last_baked_on
    FROM bake_records br
    JOIN bake_record_items bri ON bri.bake_record_id = br.id
    GROUP BY br.owner_id, coalesce(bri.recipe_id::text, 'title:' || bri.recipe_title), bri.recipe_id
    WITH DATA;

    -- Unique index: required for REFRESH ... CONCURRENTLY (readers are not blocked during refresh)
    CREATE UNIQUE INDEX ux_mv_bake_statistics_owner_id_recipe_key ON mv_bake_statistics (owner_id, recipe_key);

    COMMENT ON MATERIALIZED VIEW mv_bake_statistics IS
        'Per baker and recipe: number of bakes, loaves, average rating, average cost per loaf, first and last bake. Refreshed by fn_refresh_bake_statistics().';

    -- Only the owner of a materialized view may refresh it. This SECURITY DEFINER function lets the
    -- application role refresh the statistics without owning the view; search_path is fixed for safety.
    CREATE OR REPLACE FUNCTION fn_refresh_bake_statistics()
    RETURNS void
    LANGUAGE plpgsql
    SECURITY DEFINER
    SET search_path = public, pg_temp
    AS $$
    BEGIN
        REFRESH MATERIALIZED VIEW CONCURRENTLY mv_bake_statistics;
    END;
    $$;

    COMMENT ON FUNCTION fn_refresh_bake_statistics() IS
        'Refreshes mv_bake_statistics concurrently. SECURITY DEFINER so that app_write can call it.';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223025_ReportingViewsAndMasking') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261115223025_ReportingViewsAndMasking', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223031_AccessPrivileges') THEN
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

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261115223031_AccessPrivileges') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261115223031_AccessPrivileges', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE starters ADD flour_ingredient_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE starters ADD water_ingredient_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE ingredients ADD reorder_level numeric(12,3);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE ingredients ADD track_stock boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE batches ADD ingredient_scale numeric(5,4) NOT NULL DEFAULT 1.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    CREATE TABLE stock_movements (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        ingredient_id uuid NOT NULL,
        movement_type character varying(32) NOT NULL,
        quantity numeric(12,3) NOT NULL,
        occurred_on date NOT NULL,
        batch_id uuid,
        feeding_id uuid,
        pack_count integer,
        pack_size numeric(12,3),
        total_cost numeric(12,4),
        currency character(3),
        note character varying(500),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_stock_movements PRIMARY KEY (id),
        CONSTRAINT ck_stock_movements_adjustment_note CHECK (movement_type <> 'adjustment' OR length(btrim(note)) > 0),
        CONSTRAINT ck_stock_movements_movement_type CHECK (movement_type IN ('purchase', 'batch_consumption', 'batch_return', 'starter_feeding', 'adjustment')),
        CONSTRAINT ck_stock_movements_purchase_fields CHECK (movement_type <> 'purchase' OR (pack_count > 0 AND pack_size > 0 AND total_cost >= 0 AND currency IS NOT NULL)),
        CONSTRAINT ck_stock_movements_quantity_not_zero CHECK (quantity <> 0),
        CONSTRAINT ck_stock_movements_quantity_sign CHECK ((movement_type IN ('purchase', 'batch_return') AND quantity > 0) OR (movement_type IN ('batch_consumption', 'starter_feeding') AND quantity < 0) OR movement_type = 'adjustment'),
        CONSTRAINT fk_stock_movements_batches_batch_id FOREIGN KEY (batch_id) REFERENCES batches (id) ON DELETE SET NULL,
        CONSTRAINT fk_stock_movements_currencies_currency FOREIGN KEY (currency) REFERENCES currencies (code),
        CONSTRAINT fk_stock_movements_ingredients_ingredient_id FOREIGN KEY (ingredient_id) REFERENCES ingredients (id) ON DELETE CASCADE,
        CONSTRAINT fk_stock_movements_starter_feedings_feeding_id FOREIGN KEY (feeding_id) REFERENCES starter_feedings (id) ON DELETE SET NULL
    );
    COMMENT ON TABLE stock_movements IS 'E3. Stock ledger of ingredients: purchases, batch consumption and returns, starter feedings, corrections. The stock is the sum of quantity.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    CREATE INDEX ix_starters_flour_ingredient_id ON starters (flour_ingredient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    CREATE INDEX ix_starters_water_ingredient_id ON starters (water_ingredient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE ingredients ADD CONSTRAINT ck_ingredients_reorder_level CHECK (reorder_level >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE batches ADD CONSTRAINT ck_batches_ingredient_scale CHECK (ingredient_scale > 0 AND ingredient_scale <= 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    CREATE INDEX ix_stock_movements_batch_id ON stock_movements (batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    CREATE INDEX ix_stock_movements_currency ON stock_movements (currency);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    CREATE INDEX ix_stock_movements_feeding_id ON stock_movements (feeding_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    CREATE INDEX ix_stock_movements_ingredient_id_occurred_on ON stock_movements (ingredient_id, occurred_on);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE starters ADD CONSTRAINT fk_starters_ingredients_flour_ingredient_id FOREIGN KEY (flour_ingredient_id) REFERENCES ingredients (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    ALTER TABLE starters ADD CONSTRAINT fk_starters_ingredients_water_ingredient_id FOREIGN KEY (water_ingredient_id) REFERENCES ingredients (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    -- Stock of ingredients: purchases, consumption by batches and starter feedings, corrections.
    -- The stock of an ingredient is the sum of stock_movements.quantity (signed, in the ingredient unit).
    -- Only ingredients with track_stock = true are consumed and checked; the application switches the flag on with the
    -- first purchase or correction and the user can switch it off again.

    ------------------------------------------------------------------------------------------------
    -- Workspace isolation: a movement and the batch or feeding it belongs to are in one workspace;
    -- the ingredients a starter is fed with belong to the owner of the starter.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_stock_movements_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        v_owner uuid;
    BEGIN
        SELECT owner_id INTO v_owner FROM ingredients WHERE id = NEW.ingredient_id;
        IF NEW.batch_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(v_owner, (SELECT owner_id FROM batches WHERE id = NEW.batch_id), 'batch');
        END IF;
        IF NEW.feeding_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(v_owner,
                (SELECT s.owner_id FROM starter_feedings f JOIN starters s ON s.id = f.starter_id WHERE f.id = NEW.feeding_id),
                'starter feeding');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_stock_movements_validate
        BEFORE INSERT OR UPDATE OF ingredient_id, batch_id, feeding_id ON stock_movements
        FOR EACH ROW EXECUTE FUNCTION trg_stock_movements_validate();

    CREATE OR REPLACE FUNCTION trg_starters_stock_links_validate()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NEW.flour_ingredient_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.flour_ingredient_id), 'flour ingredient');
        END IF;
        IF NEW.water_ingredient_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.water_ingredient_id), 'water ingredient');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_starters_stock_links_validate
        BEFORE INSERT OR UPDATE OF flour_ingredient_id, water_ingredient_id, owner_id ON starters
        FOR EACH ROW EXECUTE FUNCTION trg_starters_stock_links_validate();

    ------------------------------------------------------------------------------------------------
    -- Stock of an ingredient, in its unit.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION fn_stock_balance(p_ingredient_id uuid)
    RETURNS numeric
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT coalesce(sum(m.quantity), 0) FROM stock_movements m WHERE m.ingredient_id = p_ingredient_id;
    $$;

    -- Ingredients a batch needs for the full recipe quantities: every mandatory line of every item scaled to its loaves.
    CREATE OR REPLACE FUNCTION fn_batch_ingredient_needs(p_batch_id uuid)
    RETURNS TABLE (ingredient_id uuid, quantity numeric)
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT e.ingredient_id, sum(e.quantity * bi.loaves / h.yield_loaves)
        FROM batch_items bi
        CROSS JOIN LATERAL fn_recipe_effective_ingredients(bi.recipe_id) e
        CROSS JOIN LATERAL fn_recipe_effective_header(bi.recipe_id) h
        WHERE bi.batch_id = p_batch_id AND NOT e.is_optional
        GROUP BY e.ingredient_id;
    $$;

    -- What a batch takes from the stock, for the ingredients whose stock is kept:
    -- needed_full for the full recipe, needed for batches.ingredient_scale, available = stock now.
    CREATE OR REPLACE FUNCTION fn_batch_stock_check(p_batch_id uuid)
    RETURNS TABLE (ingredient_id uuid, name text, unit_code text, needed_full numeric, needed numeric, available numeric)
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT n.ingredient_id, i.name::text, i.unit_code::text, round(n.quantity, 3),
               round(n.quantity * b.ingredient_scale, 3), fn_stock_balance(i.id)
        FROM batches b
        CROSS JOIN LATERAL fn_batch_ingredient_needs(b.id) n
        JOIN ingredients i ON i.id = n.ingredient_id
        WHERE b.id = p_batch_id AND i.track_stock
        ORDER BY i.name;
    $$;

    ------------------------------------------------------------------------------------------------
    -- Take the ingredients of a batch from the stock. Called after the plan is built or rebuilt:
    -- the earlier consumption of the batch is replaced, so the stock changes by the difference.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE PROCEDURE sp_apply_batch_stock(p_batch_id uuid)
    LANGUAGE plpgsql
    AS $$
    DECLARE
        v_on date;
    BEGIN
        SELECT (planned_start_at AT TIME ZONE 'UTC')::date INTO v_on FROM batches WHERE id = p_batch_id;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Batch % not found', p_batch_id USING ERRCODE = 'P0002';
        END IF;

        DELETE FROM stock_movements WHERE batch_id = p_batch_id AND movement_type = 'batch_consumption';

        INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, batch_id)
        SELECT c.ingredient_id, 'batch_consumption', -c.needed, v_on, p_batch_id
        FROM fn_batch_stock_check(p_batch_id) c
        WHERE c.needed > 0;
    END;
    $$;

    -- Give back what a batch took and has not returned yet (the batch was cancelled or deleted before the dough was mixed).
    CREATE OR REPLACE PROCEDURE sp_return_batch_stock(p_batch_id uuid)
    LANGUAGE plpgsql
    AS $$
    BEGIN
        INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, batch_id)
        SELECT m.ingredient_id, 'batch_return', -sum(m.quantity), current_date, p_batch_id
        FROM stock_movements m
        WHERE m.batch_id = p_batch_id AND m.movement_type IN ('batch_consumption', 'batch_return')
        GROUP BY m.ingredient_id
        HAVING sum(m.quantity) < 0;
    END;
    $$;

    ------------------------------------------------------------------------------------------------
    -- A starter feeding takes its flour and water from the stock of the ingredients linked to the starter.
    -- Feedings are logged in grams; mass and volume units are converted (1 ml = 1 g), pieces are not consumed.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_starter_feedings_stock()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
        SELECT i.id, 'starter_feeding', -round(x.grams / u.factor_to_base, 3), (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id
        FROM starters s
        CROSS JOIN LATERAL (VALUES (s.flour_ingredient_id, NEW.flour_g), (s.water_ingredient_id, NEW.water_g)) AS x(ingredient_id, grams)
        JOIN ingredients i ON i.id = x.ingredient_id
        JOIN units u ON u.code = i.unit_code
        WHERE s.id = NEW.starter_id
          AND i.track_stock
          AND u.dimension IN ('mass', 'volume')
          AND round(x.grams / u.factor_to_base, 3) > 0;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_starter_feedings_stock
        AFTER INSERT ON starter_feedings
        FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_stock();

    ------------------------------------------------------------------------------------------------
    -- Ingredients below their reorder level (or below zero when no level is set).
    ------------------------------------------------------------------------------------------------
    CREATE VIEW v_low_stock AS
    SELECT i.owner_id, i.id AS ingredient_id, i.name, i.unit_code, fn_stock_balance(i.id) AS stock, i.reorder_level
    FROM ingredients i
    WHERE i.track_stock
      AND fn_stock_balance(i.id) < coalesce(i.reorder_level, 0);

    COMMENT ON VIEW v_low_stock IS 'Ingredients with kept stock that are below their reorder level, or below zero when no level is set.';

    ------------------------------------------------------------------------------------------------
    -- The batch plan takes batches.ingredient_scale into account: kneading cycles, starter amount and the
    -- ingredient lines of the cost shrink with the scale; electricity, packaging and labor do not.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE PROCEDURE sp_build_batch_plan(p_batch_id uuid)
    LANGUAGE plpgsql
    AS $$
    DECLARE
        b          batches%ROWTYPE;
        ks         kitchen_settings%ROWTYPE;
        item       record;
        v_total    numeric := 0;
        v_complete boolean := true;
        v_cost     record;
        v_first    timestamptz;
        v_minor    integer;
        v_subtotal numeric;
        v_ok       boolean;
        v_currency character(3);
    BEGIN
        SELECT * INTO b FROM batches WHERE id = p_batch_id FOR UPDATE;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Batch % not found', p_batch_id USING ERRCODE = 'P0002';
        END IF;
        IF b.status <> 'planned' THEN
            RAISE EXCEPTION 'Only a planned batch can be (re)planned; batch % is %', p_batch_id, b.status USING ERRCODE = '55000';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM batch_items WHERE batch_id = p_batch_id) THEN
            RAISE EXCEPTION 'Batch % has no items', p_batch_id USING ERRCODE = '23514';
        END IF;

        SELECT * INTO ks FROM kitchen_settings WHERE user_id = b.owner_id;
        SELECT minor_units INTO v_minor FROM currencies WHERE code = b.currency;

        FOR item IN SELECT * FROM batch_items WHERE batch_id = p_batch_id ORDER BY sort_order FOR UPDATE LOOP
            DELETE FROM reminders WHERE batch_item_id = item.id AND status = 'scheduled';
            DELETE FROM batch_stages WHERE batch_item_id = item.id;

            INSERT INTO batch_stages (batch_item_id, recipe_stage_id, sort_order, name, stage_type_code,
                                      planned_min_minutes, planned_max_minutes, planned_start_at, planned_end_min_at, planned_end_max_at, status, extended_minutes, is_overridden)
            SELECT item.id, t.recipe_stage_id, t.sort_order, t.name, t.stage_type_code,
                   t.min_minutes, t.max_minutes, t.planned_start_at, t.planned_end_min_at, t.planned_end_max_at, 'pending', 0, false
            FROM fn_recipe_timeline(item.recipe_id, b.planned_start_at) t;

            UPDATE batch_items
            SET kneading_cycles  = CASE
                                       WHEN b.ingredient_scale = 1 THEN fn_kneading_cycles(item.recipe_id, item.loaves)
                                       WHEN ks.mixer_bowl_capacity_kg IS NULL THEN NULL
                                       ELSE greatest(1, ceil(fn_dough_weight_g(item.recipe_id, item.loaves) * b.ingredient_scale
                                                             / (ks.mixer_bowl_capacity_kg * 1000)))::int
                                   END,
                starter_amount_g = nullif(fn_starter_amount_g(item.recipe_id, item.loaves) * b.ingredient_scale, 0)
            WHERE id = item.id;

            IF b.ingredient_scale = 1 THEN
                SELECT * INTO v_cost FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) WHERE line_type = 'total';
                IF v_cost.is_complete AND v_cost.currency = b.currency THEN
                    v_total := v_total + v_cost.amount;
                ELSE
                    v_complete := false;
                END IF;
            ELSE
                SELECT coalesce(sum(CASE WHEN c.line_type = 'ingredient' THEN round(c.amount * b.ingredient_scale, v_minor) ELSE c.amount END)
                                    FILTER (WHERE c.line_type NOT IN ('loss', 'total')), 0),
                       bool_and(c.is_complete),
                       max(c.currency) FILTER (WHERE c.line_type = 'total')
                INTO v_subtotal, v_ok, v_currency
                FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) c;
                IF v_ok AND v_currency = b.currency THEN
                    v_total := v_total + v_subtotal + round(v_subtotal * coalesce(ks.loss_rate_percent, 0) / 100, v_minor);
                ELSE
                    v_complete := false;
                END IF;
            END IF;

            IF item.starter_reminder_minutes IS NOT NULL THEN
                SELECT min(planned_start_at) INTO v_first
                FROM batch_stages WHERE batch_item_id = item.id AND stage_type_code = 'mix';
                v_first := coalesce(v_first, b.planned_start_at);
                INSERT INTO reminders (owner_id, kind, starter_id, batch_item_id, due_at, channel, status, message, attempts)
                VALUES (b.owner_id, 'before_kneading', item.starter_id, item.id,
                        v_first - make_interval(mins => item.starter_reminder_minutes), 'push', 'scheduled',
                        format('Prepare the starter: kneading starts in %s min', item.starter_reminder_minutes), 0);
            END IF;
        END LOOP;

        UPDATE batches
        SET planned_ready_from = s.ready_from,
            planned_ready_to   = s.ready_to,
            total_cost         = CASE WHEN v_complete THEN v_total END
        FROM (SELECT max(bs.planned_end_min_at) AS ready_from, max(bs.planned_end_max_at) AS ready_to
              FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
              WHERE bi.batch_id = p_batch_id) s
        WHERE id = p_batch_id;
    END;
    $$;

    COMMENT ON FUNCTION trg_stock_movements_validate() IS 'Trigger: a stock movement, its batch and its feeding belong to one workspace.';
    COMMENT ON FUNCTION trg_starters_stock_links_validate() IS 'Trigger: the ingredients a starter is fed with belong to the owner of the starter.';
    COMMENT ON FUNCTION trg_starter_feedings_stock() IS 'Trigger: a feeding takes its flour and water from the stock of the linked ingredients.';
    COMMENT ON FUNCTION fn_stock_balance(uuid) IS 'Stock of an ingredient in its unit: sum of its stock movements.';
    COMMENT ON FUNCTION fn_batch_ingredient_needs(uuid) IS 'Ingredient quantities of a batch for the full recipe quantities, summed over its items.';
    COMMENT ON FUNCTION fn_batch_stock_check(uuid) IS 'Needed (full and scaled) and available quantity of every stock-kept ingredient of a batch.';
    COMMENT ON PROCEDURE sp_apply_batch_stock(uuid) IS 'Replaces the stock consumption of a batch with the consumption of its current plan.';
    COMMENT ON PROCEDURE sp_return_batch_stock(uuid) IS 'Returns to the stock what a batch took and has not returned yet.';
    COMMENT ON PROCEDURE sp_build_batch_plan(uuid) IS
        'Builds the stage plan, kneading cycles, starter amount, reminders, ready window and total cost of a planned batch, scaled by ingredient_scale (US-16-18, US-20, US-29).';

    -- Privileges: the application role gets its rights from the default privileges; the reporting role reads the
    -- ledger without the free-text note (column privileges).
    GRANT SELECT (id, ingredient_id, movement_type, quantity, occurred_on, batch_id, feeding_id, pack_count, pack_size,
                  total_cost, currency, created_at)
        ON stock_movements TO app_read;
    -- A view gets the default table privileges of app_write; like the other views it is read-only.
    REVOKE INSERT, UPDATE, DELETE ON v_low_stock FROM app_write;
    GRANT SELECT ON v_low_stock TO app_write, app_read;
    GRANT EXECUTE ON FUNCTION fn_stock_balance(uuid), fn_batch_ingredient_needs(uuid), fn_batch_stock_check(uuid) TO app_read;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124161242_IngredientStock') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261124161242_IngredientStock', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE stock_movements DROP CONSTRAINT ck_stock_movements_movement_type;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE stock_movements DROP CONSTRAINT ck_stock_movements_quantity_sign;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE stock_movements ADD available_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE starters ADD ingredient_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE starters ADD usable_share numeric(4,3) NOT NULL DEFAULT 0.667;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE recipes ADD photo_content_type character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE recipes ADD photo_key character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE ingredients ADD photo_content_type character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE ingredients ADD photo_key character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE stock_movements ADD CONSTRAINT ck_stock_movements_movement_type CHECK (movement_type IN ('purchase', 'batch_consumption', 'batch_return', 'starter_feeding', 'adjustment', 'starter_ripened', 'starter_refed'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE stock_movements ADD CONSTRAINT ck_stock_movements_quantity_sign CHECK ((movement_type IN ('purchase', 'batch_return', 'starter_ripened') AND quantity > 0) OR (movement_type IN ('batch_consumption', 'starter_feeding', 'starter_refed') AND quantity < 0) OR movement_type = 'adjustment');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    -- A starter is a kind of ingredient: every starter has its own row in ingredients (category starter), so recipes use
    -- it and its stock is the ripe starter. The name is the same in both tables and is kept in step by triggers.
    -- A feeding returns what is left of the ripe starter to the jar and, at the expected peak, puts the usable share
    -- of the fed starter into the stock; the rest stays for the next feeding.

    ------------------------------------------------------------------------------------------------
    -- Stock at a moment: movements that are not available yet (ripening starter) do not count.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION fn_stock_balance_at(p_ingredient_id uuid, p_at timestamptz)
    RETURNS numeric
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT coalesce(sum(m.quantity), 0)
        FROM stock_movements m
        WHERE m.ingredient_id = p_ingredient_id
          AND (m.available_at IS NULL OR m.available_at <= p_at);
    $$;

    CREATE OR REPLACE FUNCTION fn_stock_balance(p_ingredient_id uuid)
    RETURNS numeric
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT fn_stock_balance_at(p_ingredient_id, now());
    $$;

    -- A batch is checked against the stock at its planned start: a starter fed today counts for a batch tomorrow.
    CREATE OR REPLACE FUNCTION fn_batch_stock_check(p_batch_id uuid)
    RETURNS TABLE (ingredient_id uuid, name text, unit_code text, needed_full numeric, needed numeric, available numeric)
    LANGUAGE sql STABLE STRICT
    AS $$
        SELECT n.ingredient_id, i.name::text, i.unit_code::text, round(n.quantity, 3),
               round(n.quantity * b.ingredient_scale, 3), fn_stock_balance_at(i.id, greatest(b.planned_start_at, now()))
        FROM batches b
        CROSS JOIN LATERAL fn_batch_ingredient_needs(b.id) n
        JOIN ingredients i ON i.id = n.ingredient_id
        WHERE b.id = p_batch_id AND i.track_stock
        ORDER BY i.name;
    $$;

    CREATE OR REPLACE VIEW v_low_stock AS
    SELECT i.owner_id, i.id AS ingredient_id, i.name, i.unit_code, fn_stock_balance(i.id) AS stock, i.reorder_level
    FROM ingredients i
    WHERE i.track_stock
      AND fn_stock_balance(i.id) < coalesce(i.reorder_level, 0);

    ------------------------------------------------------------------------------------------------
    -- Starter and its ingredient.
    ------------------------------------------------------------------------------------------------
    -- A new starter without an ingredient gets one: an unlinked starter ingredient of the owner with the same name is
    -- adopted, otherwise an ingredient is created (grams, stock kept).
    CREATE OR REPLACE FUNCTION trg_starters_ingredient_before()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        -- the application sends the empty uuid for "not set"
        IF NEW.ingredient_id = '00000000-0000-0000-0000-000000000000' THEN
            NEW.ingredient_id := NULL;
        END IF;
        IF NEW.ingredient_id IS NULL THEN
            SELECT i.id INTO NEW.ingredient_id
            FROM ingredients i
            WHERE i.owner_id = NEW.owner_id AND i.name = NEW.name AND i.category_code = 'starter'
              AND NOT EXISTS (SELECT 1 FROM starters s WHERE s.ingredient_id = i.id);
            IF NEW.ingredient_id IS NULL THEN
                INSERT INTO ingredients (owner_id, name, category_code, unit_code, track_stock)
                VALUES (NEW.owner_id, NEW.name, 'starter', 'g', true)
                RETURNING id INTO NEW.ingredient_id;
            ELSE
                UPDATE ingredients SET track_stock = true WHERE id = NEW.ingredient_id AND NOT track_stock;
            END IF;
        ELSE
            PERFORM fn_assert_same_owner(NEW.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.ingredient_id), 'starter ingredient');
        END IF;
        RETURN NEW;
    END;
    $$;

    -- One name in both tables: a change on either side is copied to the other.
    CREATE OR REPLACE FUNCTION trg_starters_name_sync()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        UPDATE ingredients SET name = NEW.name WHERE id = NEW.ingredient_id AND name <> NEW.name;
        RETURN NULL;
    END;
    $$;

    CREATE OR REPLACE FUNCTION trg_ingredients_starter_name_sync()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        UPDATE starters SET name = NEW.name WHERE ingredient_id = NEW.id AND name <> NEW.name;
        RETURN NULL;
    END;
    $$;

    -- A deleted starter takes its ingredient with it unless a recipe still uses the ingredient.
    CREATE OR REPLACE FUNCTION trg_starters_ingredient_after_delete()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        DELETE FROM ingredients i
        WHERE i.id = OLD.ingredient_id
          AND NOT EXISTS (SELECT 1 FROM recipe_ingredients ri WHERE ri.ingredient_id = i.id);
        RETURN NULL;
    END;
    $$;

    -- Starters that existed before this migration get their ingredients.
    DO $$
    DECLARE
        s record;
        v_ingredient uuid;
    BEGIN
        FOR s IN SELECT * FROM starters WHERE ingredient_id IS NULL LOOP
            SELECT i.id INTO v_ingredient
            FROM ingredients i
            WHERE i.owner_id = s.owner_id AND i.name = s.name
              AND NOT EXISTS (SELECT 1 FROM starters x WHERE x.ingredient_id = i.id);
            IF v_ingredient IS NULL THEN
                INSERT INTO ingredients (owner_id, name, category_code, unit_code, track_stock)
                VALUES (s.owner_id, s.name, 'starter', 'g', true)
                RETURNING id INTO v_ingredient;
            END IF;
            UPDATE starters SET ingredient_id = v_ingredient WHERE id = s.id;
        END LOOP;
    END;
    $$;

    CREATE TRIGGER trg_starters_ingredient_before
        BEFORE INSERT ON starters
        FOR EACH ROW EXECUTE FUNCTION trg_starters_ingredient_before();

    CREATE TRIGGER trg_starters_name_sync
        AFTER UPDATE OF name ON starters
        FOR EACH ROW EXECUTE FUNCTION trg_starters_name_sync();

    CREATE TRIGGER trg_ingredients_starter_name_sync
        AFTER UPDATE OF name ON ingredients
        FOR EACH ROW EXECUTE FUNCTION trg_ingredients_starter_name_sync();

    CREATE TRIGGER trg_starters_ingredient_after_delete
        AFTER DELETE ON starters
        FOR EACH ROW EXECUTE FUNCTION trg_starters_ingredient_after_delete();

    ------------------------------------------------------------------------------------------------
    -- A feeding and the stock: flour and water are taken from the linked ingredients (as before); the ripe starter
    -- that is left goes back into the jar; the usable share of the fed starter becomes stock at the expected peak.
    -- Only the latest feeding moves the starter stock: a feeding logged afterwards for an earlier time does not.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_starter_feedings_stock()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        s        starters%ROWTYPE;
        v_factor numeric;
        v_track  boolean;
        v_left   numeric;
        v_ripe   numeric;
        v_at     timestamptz;
    BEGIN
        SELECT * INTO s FROM starters WHERE id = NEW.starter_id;

        INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
        SELECT i.id, 'starter_feeding', -round(x.grams / u.factor_to_base, 3), (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id
        FROM (VALUES (s.flour_ingredient_id, NEW.flour_g), (s.water_ingredient_id, NEW.water_g)) AS x(ingredient_id, grams)
        JOIN ingredients i ON i.id = x.ingredient_id
        JOIN units u ON u.code = i.unit_code
        WHERE i.track_stock
          AND u.dimension IN ('mass', 'volume')
          AND round(x.grams / u.factor_to_base, 3) > 0;

        SELECT u.factor_to_base, i.track_stock AND u.dimension = 'mass' INTO v_factor, v_track
        FROM ingredients i JOIN units u ON u.code = i.unit_code
        WHERE i.id = s.ingredient_id;

        IF v_track AND NEW.fed_at >= (SELECT max(fed_at) FROM starter_feedings WHERE starter_id = NEW.starter_id) THEN
            -- starter of the previous feeding that has not ripened yet never reaches the stock
            DELETE FROM stock_movements
            WHERE ingredient_id = s.ingredient_id AND movement_type = 'starter_ripened' AND available_at > NEW.fed_at;

            v_left := fn_stock_balance_at(s.ingredient_id, NEW.fed_at);
            IF v_left > 0 THEN
                INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
                VALUES (s.ingredient_id, 'starter_refed', -v_left, (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id);
            END IF;

            v_at := coalesce(NEW.expected_peak_at, NEW.fed_at);
            v_ripe := round((NEW.flour_g + NEW.water_g + coalesce(NEW.starter_kept_g, 0)) * s.usable_share / v_factor, 3);
            IF v_ripe > 0 THEN
                INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id, available_at)
                VALUES (s.ingredient_id, 'starter_ripened', v_ripe, (v_at AT TIME ZONE 'UTC')::date, NEW.id, v_at);
            END IF;
        END IF;
        RETURN NEW;
    END;
    $$;

    COMMENT ON FUNCTION fn_stock_balance_at(uuid, timestamptz) IS 'Stock of an ingredient at a moment: movements that are not available yet do not count.';
    COMMENT ON FUNCTION fn_stock_balance(uuid) IS 'Stock of an ingredient now, in its unit.';
    COMMENT ON FUNCTION fn_batch_stock_check(uuid) IS 'Needed (full and scaled) quantity and the stock at the planned start of every stock-kept ingredient of a batch.';
    COMMENT ON FUNCTION trg_starters_ingredient_before() IS 'Trigger: a new starter gets its ingredient of the category starter (adopted by name or created).';
    COMMENT ON FUNCTION trg_starters_name_sync() IS 'Trigger: renaming a starter renames its ingredient.';
    COMMENT ON FUNCTION trg_ingredients_starter_name_sync() IS 'Trigger: renaming the ingredient of a starter renames the starter.';
    COMMENT ON FUNCTION trg_starters_ingredient_after_delete() IS 'Trigger: a deleted starter takes its ingredient with it unless a recipe uses it.';
    COMMENT ON FUNCTION trg_starter_feedings_stock() IS
        'Trigger: a feeding takes flour and water from the linked ingredients, returns the ripe starter that is left and adds the usable share at the expected peak.';

    GRANT EXECUTE ON FUNCTION fn_stock_balance_at(uuid, timestamptz) TO app_read;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE starters ALTER COLUMN ingredient_id SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    CREATE UNIQUE INDEX ux_starters_ingredient_id ON starters (ingredient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE starters ADD CONSTRAINT ck_starters_usable_share CHECK (usable_share > 0 AND usable_share <= 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    ALTER TABLE starters ADD CONSTRAINT fk_starters_ingredients_ingredient_id FOREIGN KEY (ingredient_id) REFERENCES ingredients (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124190938_StarterIngredients') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261124190938_StarterIngredients', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124193748_RefreshTokens') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        family_id uuid NOT NULL,
        token_hash character(64) NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        rotated_at timestamp with time zone,
        revoked_at timestamp with time zone,
        CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
        CONSTRAINT ck_refresh_tokens_expires_after_created CHECK (expires_at > created_at),
        CONSTRAINT ck_refresh_tokens_token_hash CHECK (token_hash ~ '^[0-9a-f]{64}$'),
        CONSTRAINT fk_refresh_tokens_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    COMMENT ON TABLE refresh_tokens IS 'E1, US-02. Hashes of the refresh tokens of signed-in browsers; a family is the chain of one sign-in.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124193748_RefreshTokens') THEN
    CREATE INDEX ix_refresh_tokens_family_id ON refresh_tokens (family_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124193748_RefreshTokens') THEN
    CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124193748_RefreshTokens') THEN
    CREATE UNIQUE INDEX ux_refresh_tokens_token_hash ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124193748_RefreshTokens') THEN
    -- Refresh tokens are personal session data: anonymizing a user signs the account out everywhere.
    CREATE OR REPLACE PROCEDURE sp_anonymize_user(p_user_id uuid)
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM users WHERE id = p_user_id) THEN
            RAISE EXCEPTION 'User % not found', p_user_id USING ERRCODE = 'P0002';
        END IF;

        DELETE FROM refresh_tokens     WHERE user_id = p_user_id;
        DELETE FROM push_subscriptions WHERE user_id = p_user_id;
        DELETE FROM chat_conversations WHERE owner_id = p_user_id;
        DELETE FROM reminders          WHERE owner_id = p_user_id;
        DELETE FROM bake_records       WHERE owner_id = p_user_id;
        DELETE FROM batches            WHERE owner_id = p_user_id;
        DELETE FROM starters           WHERE owner_id = p_user_id;
        DELETE FROM recipes            WHERE owner_id = p_user_id;
        DELETE FROM ingredients        WHERE owner_id = p_user_id;
        DELETE FROM flours             WHERE owner_id = p_user_id;
        DELETE FROM kitchen_settings   WHERE user_id  = p_user_id;

        UPDATE users
        SET email            = 'anonymized-' || id || '@anonymized.invalid',
            normalized_email = upper('anonymized-' || id || '@anonymized.invalid'),
            display_name     = 'Anonymous baker',
            password_hash    = fn_random_disabled_password_hash(),
            role             = 'user',
            last_login_at    = NULL
        WHERE id = p_user_id;
    END;
    $$;

    COMMENT ON PROCEDURE sp_anonymize_user(uuid) IS
        'Removes personal data, sessions and the private workspace of a user, keeps public contributions as "Anonymous baker" (GDPR).';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124193748_RefreshTokens') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261124193748_RefreshTokens', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    ALTER TABLE starter_feedings ADD flour_ingredient_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    ALTER TABLE starter_feedings ADD water_ingredient_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    CREATE INDEX ix_starter_feedings_flour_ingredient_id ON starter_feedings (flour_ingredient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    CREATE INDEX ix_starter_feedings_water_ingredient_id ON starter_feedings (water_ingredient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    ALTER TABLE starter_feedings ADD CONSTRAINT fk_starter_feedings_ingredients_flour_ingredient_id FOREIGN KEY (flour_ingredient_id) REFERENCES ingredients (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    ALTER TABLE starter_feedings ADD CONSTRAINT fk_starter_feedings_ingredients_water_ingredient_id FOREIGN KEY (water_ingredient_id) REFERENCES ingredients (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    -- A feeding names the flour and the water it used: a starter can be fed with another flour than usual. The usual
    -- flour and water of the starter are the default. The price of the starter is what it was fed: after every feeding
    -- the cost of one unit of starter is stored as the price of its ingredient, so recipes with a starter have a cost.

    -- Feedings recorded before this migration used the flour and water of their starter.
    UPDATE starter_feedings f
    SET flour_ingredient_id = s.flour_ingredient_id, water_ingredient_id = s.water_ingredient_id
    FROM starters s
    WHERE s.id = f.starter_id;

    COMMENT ON COLUMN starter_feedings.flour_ingredient_id IS 'Flour ingredient this feeding used; the usual flour of the starter when not chosen.';
    COMMENT ON COLUMN starter_feedings.water_ingredient_id IS 'Water ingredient this feeding used; the usual water of the starter when not chosen.';

    ------------------------------------------------------------------------------------------------
    -- Default and ownership of the ingredients of a feeding.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_starter_feedings_ingredients_before()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        s starters%ROWTYPE;
    BEGIN
        SELECT * INTO s FROM starters WHERE id = NEW.starter_id;
        NEW.flour_ingredient_id := coalesce(NEW.flour_ingredient_id, s.flour_ingredient_id);
        NEW.water_ingredient_id := coalesce(NEW.water_ingredient_id, s.water_ingredient_id);
        IF NEW.flour_ingredient_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(s.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.flour_ingredient_id), 'flour of the feeding');
        END IF;
        IF NEW.water_ingredient_id IS NOT NULL THEN
            PERFORM fn_assert_same_owner(s.owner_id, (SELECT owner_id FROM ingredients WHERE id = NEW.water_ingredient_id), 'water of the feeding');
        END IF;
        RETURN NEW;
    END;
    $$;

    CREATE TRIGGER trg_starter_feedings_ingredients_before
        BEFORE INSERT ON starter_feedings
        FOR EACH ROW EXECUTE FUNCTION trg_starter_feedings_ingredients_before();

    ------------------------------------------------------------------------------------------------
    -- Cost of one gram of the starter after a feeding, in the currency of the kitchen.
    -- Flour and water are priced on the day of the feeding. The starter that was kept has the price the starter had
    -- before; without one it is worth as much as the fresh mix. NULL when the cost is not known: no flour was named,
    -- a named ingredient has no price on that day, or its price is in another currency.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION fn_starter_feeding_cost(p_feeding_id uuid)
    RETURNS numeric
    LANGUAGE plpgsql STABLE STRICT
    AS $$
    DECLARE
        f          starter_feedings%ROWTYPE;
        s          starters%ROWTYPE;
        v_currency character(3);
        v_date     date;
        v_cost     numeric := 0;
        v_mass     numeric;
        v_factor   numeric;
        x          record;
        pr         record;
    BEGIN
        SELECT * INTO f FROM starter_feedings WHERE id = p_feeding_id;
        IF NOT FOUND OR f.flour_ingredient_id IS NULL OR f.flour_g <= 0 THEN
            RETURN NULL;
        END IF;
        SELECT * INTO s FROM starters WHERE id = f.starter_id;
        v_currency := coalesce((SELECT ks.currency FROM kitchen_settings ks WHERE ks.user_id = s.owner_id), 'EUR');
        v_date := (f.fed_at AT TIME ZONE 'UTC')::date;

        FOR x IN
            SELECT v.ingredient_id, v.grams
            FROM (VALUES (f.flour_ingredient_id, f.flour_g), (f.water_ingredient_id, f.water_g)) AS v(ingredient_id, grams)
            WHERE v.ingredient_id IS NOT NULL AND v.grams > 0
        LOOP
            SELECT u.factor_to_base INTO v_factor
            FROM ingredients i JOIN units u ON u.code = i.unit_code
            WHERE i.id = x.ingredient_id AND u.dimension IN ('mass', 'volume');
            IF NOT FOUND THEN
                RETURN NULL;
            END IF;
            SELECT * INTO pr FROM fn_price_on_date(x.ingredient_id, v_date);
            IF NOT FOUND OR pr.currency <> v_currency THEN
                RETURN NULL;
            END IF;
            v_cost := v_cost + x.grams / v_factor * pr.price / pr.per_quantity;
        END LOOP;

        v_mass := f.flour_g + f.water_g;
        IF coalesce(f.starter_kept_g, 0) > 0 THEN
            SELECT u.factor_to_base INTO v_factor
            FROM ingredients i JOIN units u ON u.code = i.unit_code
            WHERE i.id = s.ingredient_id AND u.dimension = 'mass';
            SELECT * INTO pr FROM fn_price_on_date(s.ingredient_id, v_date);
            IF FOUND AND v_factor IS NOT NULL AND pr.currency = v_currency THEN
                v_cost := v_cost + f.starter_kept_g / v_factor * pr.price / pr.per_quantity;
                v_mass := v_mass + f.starter_kept_g;
            END IF;
        END IF;
        RETURN v_cost / v_mass;
    END;
    $$;

    COMMENT ON FUNCTION fn_starter_feeding_cost(uuid) IS
        'Cost of one gram of a starter after a feeding: flour and water at their prices of that day plus the kept starter at its previous price; NULL when a price is missing.';

    ------------------------------------------------------------------------------------------------
    -- A feeding and the stock (as before, with the flour and water of the feeding) and the price of the starter.
    ------------------------------------------------------------------------------------------------
    CREATE OR REPLACE FUNCTION trg_starter_feedings_stock()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    DECLARE
        s        starters%ROWTYPE;
        v_factor numeric;
        v_track  boolean;
        v_mass   boolean;
        v_latest boolean;
        v_left   numeric;
        v_ripe   numeric;
        v_at     timestamptz;
        v_cost   numeric;
    BEGIN
        SELECT * INTO s FROM starters WHERE id = NEW.starter_id;

        INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
        SELECT i.id, 'starter_feeding', -round(x.grams / u.factor_to_base, 3), (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id
        FROM (VALUES (NEW.flour_ingredient_id, NEW.flour_g), (NEW.water_ingredient_id, NEW.water_g)) AS x(ingredient_id, grams)
        JOIN ingredients i ON i.id = x.ingredient_id
        JOIN units u ON u.code = i.unit_code
        WHERE i.track_stock
          AND u.dimension IN ('mass', 'volume')
          AND round(x.grams / u.factor_to_base, 3) > 0;

        SELECT u.factor_to_base, i.track_stock, u.dimension = 'mass' INTO v_factor, v_track, v_mass
        FROM ingredients i JOIN units u ON u.code = i.unit_code
        WHERE i.id = s.ingredient_id;

        -- Only the latest feeding moves the starter stock and sets its price: a feeding logged afterwards for an
        -- earlier time does not.
        v_latest := NEW.fed_at >= (SELECT max(fed_at) FROM starter_feedings WHERE starter_id = NEW.starter_id);

        IF v_latest AND v_mass THEN
            v_cost := fn_starter_feeding_cost(NEW.id);
            IF v_cost IS NOT NULL THEN
                INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
                VALUES (s.ingredient_id, round(v_cost * 1000, 4), round(1000 / v_factor, 3),
                        coalesce((SELECT ks.currency FROM kitchen_settings ks WHERE ks.user_id = s.owner_id), 'EUR'),
                        (NEW.fed_at AT TIME ZONE 'UTC')::date)
                ON CONFLICT (ingredient_id, effective_from)
                DO UPDATE SET price = EXCLUDED.price, per_quantity = EXCLUDED.per_quantity, currency = EXCLUDED.currency;
            END IF;
        END IF;

        IF v_latest AND v_mass AND v_track THEN
            -- starter of the previous feeding that has not ripened yet never reaches the stock
            DELETE FROM stock_movements
            WHERE ingredient_id = s.ingredient_id AND movement_type = 'starter_ripened' AND available_at > NEW.fed_at;

            v_left := fn_stock_balance_at(s.ingredient_id, NEW.fed_at);
            IF v_left > 0 THEN
                INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id)
                VALUES (s.ingredient_id, 'starter_refed', -v_left, (NEW.fed_at AT TIME ZONE 'UTC')::date, NEW.id);
            END IF;

            v_at := coalesce(NEW.expected_peak_at, NEW.fed_at);
            v_ripe := round((NEW.flour_g + NEW.water_g + coalesce(NEW.starter_kept_g, 0)) * s.usable_share / v_factor, 3);
            IF v_ripe > 0 THEN
                INSERT INTO stock_movements (ingredient_id, movement_type, quantity, occurred_on, feeding_id, available_at)
                VALUES (s.ingredient_id, 'starter_ripened', v_ripe, (v_at AT TIME ZONE 'UTC')::date, NEW.id, v_at);
            END IF;
        END IF;
        RETURN NEW;
    END;
    $$;

    COMMENT ON FUNCTION trg_starter_feedings_ingredients_before() IS
        'Trigger: a feeding uses the usual flour and water of its starter unless others are named; they belong to the owner of the starter.';
    COMMENT ON FUNCTION trg_starter_feedings_stock() IS
        'Trigger: a feeding takes its flour and water from the stock, sets the price of the starter to what it was fed, returns the ripe starter that is left and adds the usable share at the expected peak.';

    GRANT EXECUTE ON FUNCTION fn_starter_feeding_cost(uuid) TO app_read;

    -- Starters fed before this migration get their price from their latest feeding, where a price is not set for that day.
    INSERT INTO ingredient_prices (ingredient_id, price, per_quantity, currency, effective_from)
    SELECT s.ingredient_id, round(c.cost * 1000, 4), round(1000 / u.factor_to_base, 3),
           coalesce((SELECT ks.currency FROM kitchen_settings ks WHERE ks.user_id = s.owner_id), 'EUR'),
           (f.fed_at AT TIME ZONE 'UTC')::date
    FROM starters s
    JOIN ingredients i ON i.id = s.ingredient_id
    JOIN units u ON u.code = i.unit_code AND u.dimension = 'mass'
    JOIN LATERAL (SELECT * FROM starter_feedings x WHERE x.starter_id = s.id ORDER BY x.fed_at DESC LIMIT 1) f ON true
    CROSS JOIN LATERAL (SELECT fn_starter_feeding_cost(f.id) AS cost) c
    WHERE c.cost IS NOT NULL
    ON CONFLICT (ingredient_id, effective_from) DO NOTHING;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124211347_StarterFeedingIngredients') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261124211347_StarterFeedingIngredients', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124232906_StarterFeedingReminder') THEN
    -- The reminder of a batch item with a starter is a reminder to feed it: it is due the time to peak of the starter
    -- before the first kneading stage (the Core API stores that lead in batch_items.starter_reminder_minutes), so the
    -- starter is ripe when kneading starts. Only the text of the reminder changes here.

    CREATE OR REPLACE PROCEDURE sp_build_batch_plan(p_batch_id uuid)
    LANGUAGE plpgsql
    AS $$
    DECLARE
        b          batches%ROWTYPE;
        ks         kitchen_settings%ROWTYPE;
        item       record;
        v_total    numeric := 0;
        v_complete boolean := true;
        v_cost     record;
        v_first    timestamptz;
        v_minor    integer;
        v_subtotal numeric;
        v_ok       boolean;
        v_currency character(3);
    BEGIN
        SELECT * INTO b FROM batches WHERE id = p_batch_id FOR UPDATE;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Batch % not found', p_batch_id USING ERRCODE = 'P0002';
        END IF;
        IF b.status <> 'planned' THEN
            RAISE EXCEPTION 'Only a planned batch can be (re)planned; batch % is %', p_batch_id, b.status USING ERRCODE = '55000';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM batch_items WHERE batch_id = p_batch_id) THEN
            RAISE EXCEPTION 'Batch % has no items', p_batch_id USING ERRCODE = '23514';
        END IF;

        SELECT * INTO ks FROM kitchen_settings WHERE user_id = b.owner_id;
        SELECT minor_units INTO v_minor FROM currencies WHERE code = b.currency;

        FOR item IN SELECT * FROM batch_items WHERE batch_id = p_batch_id ORDER BY sort_order FOR UPDATE LOOP
            DELETE FROM reminders WHERE batch_item_id = item.id AND status = 'scheduled';
            DELETE FROM batch_stages WHERE batch_item_id = item.id;

            INSERT INTO batch_stages (batch_item_id, recipe_stage_id, sort_order, name, stage_type_code,
                                      planned_min_minutes, planned_max_minutes, planned_start_at, planned_end_min_at, planned_end_max_at, status, extended_minutes, is_overridden)
            SELECT item.id, t.recipe_stage_id, t.sort_order, t.name, t.stage_type_code,
                   t.min_minutes, t.max_minutes, t.planned_start_at, t.planned_end_min_at, t.planned_end_max_at, 'pending', 0, false
            FROM fn_recipe_timeline(item.recipe_id, b.planned_start_at) t;

            UPDATE batch_items
            SET kneading_cycles  = CASE
                                       WHEN b.ingredient_scale = 1 THEN fn_kneading_cycles(item.recipe_id, item.loaves)
                                       WHEN ks.mixer_bowl_capacity_kg IS NULL THEN NULL
                                       ELSE greatest(1, ceil(fn_dough_weight_g(item.recipe_id, item.loaves) * b.ingredient_scale
                                                             / (ks.mixer_bowl_capacity_kg * 1000)))::int
                                   END,
                starter_amount_g = nullif(fn_starter_amount_g(item.recipe_id, item.loaves) * b.ingredient_scale, 0)
            WHERE id = item.id;

            IF b.ingredient_scale = 1 THEN
                SELECT * INTO v_cost FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) WHERE line_type = 'total';
                IF v_cost.is_complete AND v_cost.currency = b.currency THEN
                    v_total := v_total + v_cost.amount;
                ELSE
                    v_complete := false;
                END IF;
            ELSE
                SELECT coalesce(sum(CASE WHEN c.line_type = 'ingredient' THEN round(c.amount * b.ingredient_scale, v_minor) ELSE c.amount END)
                                    FILTER (WHERE c.line_type NOT IN ('loss', 'total')), 0),
                       bool_and(c.is_complete),
                       max(c.currency) FILTER (WHERE c.line_type = 'total')
                INTO v_subtotal, v_ok, v_currency
                FROM fn_recipe_cost(item.recipe_id, item.loaves, b.price_date) c;
                IF v_ok AND v_currency = b.currency THEN
                    v_total := v_total + v_subtotal + round(v_subtotal * coalesce(ks.loss_rate_percent, 0) / 100, v_minor);
                ELSE
                    v_complete := false;
                END IF;
            END IF;

            IF item.starter_reminder_minutes IS NOT NULL THEN
                SELECT min(planned_start_at) INTO v_first
                FROM batch_stages WHERE batch_item_id = item.id AND stage_type_code = 'mix';
                v_first := coalesce(v_first, b.planned_start_at);
                INSERT INTO reminders (owner_id, kind, starter_id, batch_item_id, due_at, channel, status, message, attempts)
                VALUES (b.owner_id, 'before_kneading', item.starter_id, item.id,
                        v_first - make_interval(mins => item.starter_reminder_minutes), 'push', 'scheduled',
                        format('Feed the starter%s now: it needs %s to be ripe when kneading starts',
                               coalesce(' "' || (SELECT s.name FROM starters s WHERE s.id = item.starter_id) || '"', ''),
                               CASE WHEN item.starter_reminder_minutes >= 60
                                    THEN trim_scale(round(item.starter_reminder_minutes / 60.0, 1))::text || ' h'
                                    ELSE item.starter_reminder_minutes::text || ' min' END), 0);
            END IF;
        END LOOP;

        UPDATE batches
        SET planned_ready_from = s.ready_from,
            planned_ready_to   = s.ready_to,
            total_cost         = CASE WHEN v_complete THEN v_total END
        FROM (SELECT max(bs.planned_end_min_at) AS ready_from, max(bs.planned_end_max_at) AS ready_to
              FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
              WHERE bi.batch_id = p_batch_id) s
        WHERE id = p_batch_id;
    END;
    $$;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20261124232906_StarterFeedingReminder') THEN
    INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261124232906_StarterFeedingReminder', '10.0.15');
    END IF;
END $EF$;
COMMIT;

